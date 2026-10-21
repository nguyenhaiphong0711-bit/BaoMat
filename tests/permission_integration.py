"""Permission migration, runtime revocation and UI dependencies on an isolated Mongo database.
Uses the same prerequisites as learning_integration.py. Never connects to the user's live database.
"""
import json
import html
import re
import urllib.parse
import urllib.request
import os
import subprocess
from password_reset_smoke import Web, main
from security_regression import security_checks


def permission_checks(state, extra_checks=None):
    security_checks(state)
    mongo, api, web, passed = (state[key] for key in ('mongo', 'api', 'web', 'passed'))
    # The security suite resets the Admin password, invalidating its earlier cookie.
    api(web, '/api/auth/login', {'email': 'admin@lms.com', 'password': 'Integration@123'})
    student_id, teacher_id = state['student_id'], state['teacher_id']
    student = Web(web.base)
    api(student, '/api/auth/login', {'email': 'student@lms.com', 'password': 'Student@123'})
    original = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+student_id+'")}).PermissionCodes))'))
    # Editing Mongo grants bypasses the UI: authorization must still use definitions, role and dependencies.
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$set:{PermissionCodes:["classes.view","lessons.manage","lessons.view","submissions.review","missing.code","lessons.view"]}})')
    identity = api(student, '/api/auth/me')
    assert set(identity['permissionCodes']) == {'classes.view', 'lessons.view'}, identity
    api(student, '/api/classes/'+state['cid']+'/lessons', {'title':'Forbidden', 'content':'x'}, expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$set:{PermissionCodes:["lessons.view","lessons.manage"]}})')
    assert api(student, '/api/auth/me')['permissionCodes'] == []
    api(student, '/api/lessons/000000000000000000000001', expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$set:{PermissionCodes:'+json.dumps(original)+'}})')
    passed('runtime permissions reject unknown/cross-role grants and missing prerequisites even after direct Mongo edits')

    # A custom definition may restrict a role but can never expand controller role/ownership access.
    mongo('d.Permissions.insertOne({Code:"test.restricted",Name:"Teacher-only custom permission",Category:"Test",Description:"",ControllerName:"",ActionName:"",IsSystem:false,AllowedRoles:["Teacher"],RequiredCodes:[]})')
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$addToSet:{PermissionCodes:"test.restricted"}})')
    assert 'test.restricted' not in api(student, '/api/auth/me')['permissionCodes']
    passed('database AllowedRoles excludes custom permission grants for the wrong role')

    # Deliberately create a legacy document without flags/permission fields; snapshot-only migration must repair it.
    legacy_id = mongo('const r=d.Users.insertOne({Email:"legacy-permission@example.test",Role:"Student",IsActive:true});print(r.insertedId.toString())')
    revoked_id = mongo('const r=d.Users.insertOne({Email:"revoked-permission@example.test",Role:"Student",IsActive:true,PermissionsInitialized:true,StudentPortalPermissionsInitialized:true,PermissionCodes:[]});print(r.insertedId.toString())')
    custom_id = mongo('const r=d.Users.insertOne({Email:"custom-permission@example.test",Role:"Teacher",IsActive:true,PermissionsInitialized:true,StudentPortalPermissionsInitialized:false,PermissionCodes:["test.restricted","classes.view","lessons.manage","tuition.pay","missing.code","classes.view"]});print(r.insertedId.toString())')
    def sync():
        result = subprocess.run([state['args'].dotnet, state['args'].dll, '--sync-permissions'],
            cwd=state['repo'], env=state['environment'], capture_output=True, text=True)
        assert result.returncode == 0, result.stdout+result.stderr
    sync()
    def grants(uid):
        return json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+uid+'")}).PermissionCodes))'))
    assert grants(revoked_id) == [], 'Migration regranted revoked permissions'
    assert set(grants(custom_id)) == {'test.restricted', 'classes.view'}
    assert 'tuition.pay' in grants(legacy_id) and 'classes.view' in grants(legacy_id)
    assert 'test.restricted' not in grants(student_id)
    records = int(mongo('print(d.PermissionMigrations.countDocuments({Applied:true}))'))
    assert records > 0
    definitions = json.loads(mongo('print(JSON.stringify(d.Permissions.find({IsSystem:true},{_id:0,Code:1,AllowedRoles:1,RequiredCodes:1}).toArray()))'))
    assert len(definitions) == 31
    assert next(p for p in definitions if p['Code']=='lessons.manage')['RequiredCodes'] == ['lessons.view']
    assert next(p for p in definitions if p['Code']=='tuition.pay')['AllowedRoles'] == ['Student']
    sync()
    assert int(mongo('print(d.PermissionMigrations.countDocuments({Applied:true}))')) == records
    assert grants(revoked_id) == []
    passed('database migration fixes legacy/invalid grants, preserves valid custom grants and revocations, records snapshots and is idempotent')

    # Dependency validation must reject forged forms rather than silently granting extra rights.
    teacher = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+teacher_id+'")},{DepartmentId:1,TeachingSubjectIds:1,PermissionCodes:1})))'))
    def post_form(client, path, values):
        status, body = client.request('/Permission/Index')
        assert status == 200
        token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)[1])
        data = urllib.parse.urlencode(dict(values, __RequestVerificationToken=token), doseq=True).encode()
        return client.opener.open(urllib.request.Request(web.base+path, data), timeout=30)
    with post_form(web, '/Permission/Assign', {'Assignment.UserId':teacher_id,
        'Assignment.DepartmentId':teacher['DepartmentId'],
        'Assignment.TeachingSubjectIds':teacher['TeachingSubjectIds'],
        'Assignment.PermissionCodes':['lessons.manage']}) as response:
        assert response.status == 200
    assert grants(teacher_id) == teacher['PermissionCodes']
    passed('forged permission form cannot save manage rights without prerequisites')

    if extra_checks is not None:
        extra_checks(state)

    state['smtp'].reject = True
    environment = dict(os.environ, E2E_BASE_URL=web.base, E2E_ADMIN_PASSWORD='Integration@123',
        E2E_CLASS_ID=state['cid'], E2E_ASSIGNMENT_ID=state['aid'], E2E_SMTP_FAILURE='true', E2E_TEACHER_ID=teacher_id)
    environment.update(state.get('browser_environment', {}))
    subprocess.run(['npm', 'run', 'test:e2e'], cwd=state['repo']/'ClientApp', env=environment, check=True)
    passed('Chromium: permission form dependencies and all existing React/learning workflows')


if __name__ == '__main__':
    main(permission_checks)
