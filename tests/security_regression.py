"""Run baseline and security integration checks against an isolated local database.
Usage: python3 tests/security_regression.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
Requires the prerequisites documented by password_reset_smoke.py. No real email is sent.
"""
from concurrent.futures import ThreadPoolExecutor
import email
import email.policy
import html
import json
from pathlib import Path
import re
import socket
import subprocess
import threading
import time
import urllib.parse
import urllib.request
import urllib.error

from password_reset_smoke import Web, main


def security_checks(state):
    api, web, mongo, passed = (state[k] for k in ('api', 'web', 'mongo', 'passed'))
    cid, aid, teacher_id, student_id = (state[k] for k in ('cid', 'aid', 'teacher_id', 'student_id'))
    owner, teacher = state['student_client'], state['teacher_client']

    def login(address, password='Regression@123'):
        client = Web(web.base)
        api(client, '/api/auth/login', {'email': address, 'password': password})
        return client

    def csrf(client, page):
        status, body = client.request(page)
        assert status == 200, (page, status)
        return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)[1])

    def post_form(client, path, values, token_page='/Permission/Index'):
        values = dict(values, __RequestVerificationToken=csrf(client, token_page))
        request = urllib.request.Request(web.base+path, urllib.parse.urlencode(values, doseq=True).encode())
        return client.opener.open(request, timeout=30)

    t2 = api(web, '/api/users', {'fullName':'Other teacher','email':'teacher2@example.test',
        'password':'Regression@123','role':'Teacher'}, expected=201)
    s2 = api(web, '/api/users', {'fullName':'Other student','email':'student2@example.test',
        'password':'Regression@123','role':'Student'}, expected=201)
    other_teacher, other_student = login(t2['email']), login(s2['email'])
    anonymous = Web(web.base)
    api(anonymous, '/api/users', expected=401)
    for client in (owner, teacher, other_student, other_teacher):
        api(client, '/api/users', expected=403)
        api(client, '/api/activity-logs', expected=403)
        api(client, '/api/users', {'fullName':'Injected Admin','email':'inject@example.test',
            'password':'Regression@123','role':'Admin'}, expected=403)
    passed('all non-Admin accounts cannot read users/logs or create an Admin')
    assert web.request('/Permission/Assign', {'Assignment.UserId':s2['id']})[0] == 400
    api(web, '/api/users', {'fullName':'Duplicate','email':s2['email'],
        'password':'Regression@123','role':'Admin'}, expected=409)
    password_user = api(web, '/api/users', {'fullName':'Password history user','email':'password@example.test',
        'password':'First@123','role':'Student'}, expected=201)
    password_client = login(password_user['email'], 'First@123')
    api(password_client, '/api/frontend/change-password', {'currentPassword':'Wrong@123',
        'newPassword':'Second@123','confirmPassword':'Second@123'}, expected=400)
    api(password_client, '/api/frontend/change-password', {'currentPassword':'First@123',
        'newPassword':'Second@123','confirmPassword':'Second@123'})
    api(password_client, '/api/auth/me', expected=401)
    password_client = login(password_user['email'], 'Second@123')
    api(password_client, '/api/frontend/change-password', {'currentPassword':'Second@123',
        'newPassword':'First@123','confirmPassword':'First@123'}, expected=400)
    history_count = mongo('print(d.Users.findOne({_id:ObjectId("'+password_user['id']+'")} ).PasswordHistoryHashes.length)')
    assert history_count == '1'
    audit_count = mongo('print(d.ActivityLogs.countDocuments({UserId:ObjectId("'+password_user['id']+'"),Action:"PASSWORD_CHANGE"}))')
    assert audit_count == '3'
    passed('authenticated password change verifies current password, records audit and prevents reuse')

    api(Web(web.base), '/api/auth/login', {'email':'{"$ne":null}','password':'anything'}, expected=400)
    passed('structured NoSQL injection payload is rejected as an invalid scalar email')
    passed('MVC writes require anti-forgery tokens; duplicate account email is rejected')

    for client in (other_teacher, other_student):
        api(client, '/api/classes/'+cid, expected=403)
        api(client, '/api/assignments/'+aid, expected=403)
        api(client, '/api/assignments/'+aid+'/submissions', expected=403)
        api(client, '/api/submissions/'+state['submission']['id']+'/grade', {'grade':10}, 'PUT', 403)
    passed('unassigned teacher and unenrolled student cannot access another class or grade its work')

    # Enroll the second student legitimately; this still must not grant review privileges.
    classroom = state['classroom']
    api(web, '/api/classes/'+cid, {'name':classroom['name'], 'subjectId':classroom['subjectId'],
        'teacherId':teacher_id,'studentIds':[student_id,s2['id']]}, 'PUT')
    assert api(owner, '/api/classes/'+cid)['studentIds'] == [student_id]
    api(other_student, '/api/assignments/'+aid+'/submission', {'content':'Second student answer'}, 'PUT')
    passed('student class response exposes only their own membership identifier')

    # Stage and attach a real GridFS upload using the student MVC flow.
    token = csrf(owner, '/Submission/Submit?assignmentId='+aid)
    boundary = 'LmsSecurityRegressionBoundary'
    multipart = (f'--{boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{token}\r\n'
        f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="private.txt"\r\n'
        f'Content-Type: text/plain\r\n\r\nprivate submission\r\n--{boundary}--\r\n').encode()
    request = urllib.request.Request(web.base+'/Upload/SubmissionFile?assignmentId='+aid, multipart,
        {'Content-Type':'multipart/form-data; boundary='+boundary})
    with owner.opener.open(request, timeout=30) as response:
        file_id = response.read().decode()
        assert response.status == 200 and re.fullmatch('[a-f0-9]{24}',file_id)
    fake_boundary = 'LmsFakePdfBoundary'
    fake_pdf = (f'--{fake_boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{token}\r\n'
        f'--{fake_boundary}\r\nContent-Disposition: form-data; name="file"; filename="malware.pdf"\r\n'
        f'Content-Type: application/pdf\r\n\r\nMZ executable content\r\n--{fake_boundary}--\r\n').encode()
    fake_request = urllib.request.Request(web.base+'/Upload/SubmissionFile?assignmentId='+aid, fake_pdf,
        {'Content-Type':'multipart/form-data; boundary='+fake_boundary})
    try:
        owner.opener.open(fake_request, timeout=30)
        raise AssertionError('Renamed executable was accepted as PDF')
    except urllib.error.HTTPError as error:
        assert error.status == 400
    passed('upload signature validation rejects content renamed to an allowed extension')
    with post_form(owner, '/Submission/Submit?assignmentId='+aid,
        {'Content':'Answer with file','UploadedFileIds':file_id}, '/Submission/Submit?assignmentId='+aid) as response:
        assert response.status == 200
    assert owner.request('/File/Download/'+file_id) == (200,'private submission')
    assert teacher.request('/File/Download/'+file_id) == (200,'private submission')
    # Deliberately emulate legacy/misconfigured cross-role grants in the disposable DB.
    mongo('d.Users.updateOne({_id:ObjectId("'+s2['id']+'")},{$addToSet:{PermissionCodes:{$each:["submissions.review","assignments.manage","schedule.manage"]}}})')
    api(other_student, '/api/submissions/'+state['submission']['id']+'/grade', {'grade':10}, 'PUT', 403)
    for client in (other_student, other_teacher):
        _, body = client.request('/File/Download/'+file_id)
        assert 'private submission' not in body
        api(client, '/api/assignments/'+aid+'/submissions', expected=403)
    passed('real uploaded submission is private even when a student has legacy Teacher grants')
    status, body = owner.request('/Class/Details/'+cid)
    assert status == 200 and s2['email'] not in body
    passed('class roster does not expose classmates email addresses to students')

    # Exercise the actual nested permission form and immediate cookie refresh.
    profile = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+teacher_id+'")},{PermissionCodes:1,DepartmentId:1,TeachingSubjectIds:1})))'))
    grants = profile['PermissionCodes']
    def assign_teacher(codes):
        with post_form(web, '/Permission/Assign', {'Assignment.UserId':teacher_id,
            'Assignment.DepartmentId':profile['DepartmentId'],
            'Assignment.TeachingSubjectIds':profile['TeachingSubjectIds'],
            'Assignment.PermissionCodes':codes}) as response:
            assert response.status == 200
    assign_teacher([g for g in grants if g != 'assignments.manage'])
    stored = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+teacher_id+'")}).PermissionCodes))'))
    assert 'assignments.manage' not in stored, 'Permission form did not bind/save'
    api(teacher, '/api/assignments/'+aid, {'title':'Unauthorized edit','description':'x',
        'dueDate':state['future'].isoformat(),'isPublished':True}, 'PUT', 403)
    assert owner.request('/api/assignments/'+aid)[0] == 200
    assign_teacher(grants)
    api(teacher, '/api/assignments/'+aid, {'title':'Allowed edit','description':'x',
        'dueDate':state['future'].isoformat(),'isPublished':True}, 'PUT')
    passed('Admin permission form saves correctly; revocation and regrant apply without re-login')

    # Existing Student roster IDs cannot confer Teacher ownership after a role change.
    original = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+student_id+'")}).PermissionCodes))'))
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$set:{Role:"Teacher",PermissionCodes:'+json.dumps(grants)+'}})')
    api(owner, '/api/auth/me', expected=401)
    changed_role = login('student@lms.com','Student@123')
    api(changed_role, '/api/classes/'+cid, expected=403)
    api(changed_role, '/api/classes/'+cid+'/assignments', {'title':'Escalated','description':'x',
        'dueDate':state['future'].isoformat()}, expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+student_id+'")},{$set:{Role:"Student",PermissionCodes:'+json.dumps(original)+'}})')
    api(changed_role, '/api/auth/me', expected=401)
    passed('role changes invalidate old cookies; stale student roster never grants Teacher ownership')

    # Deactivation must also invalidate an already authenticated session.
    api(web, '/api/users/'+s2['id']+'/toggle-active', {}, 'POST')
    api(other_student, '/api/auth/me', expected=401)
    passed('deactivated account loses its existing session immediately')

    # Concurrent wrong-password requests must atomically hit the configured lockout.
    mongo('d.Users.updateOne({_id:ObjectId("'+t2['id']+'")},{$set:{FailedLoginAttempts:0,LockoutUntil:null}})')
    barrier = threading.Barrier(8)
    def wrong(_):
        client = Web(web.base)
        barrier.wait(timeout=10)
        return client.request('/api/auth/login',{'email':t2['email'],'password':'Wrong@123'},True)[0]
    with ThreadPoolExecutor(max_workers=8) as pool:
        assert list(pool.map(wrong,range(8))) == [401]*8
    locked = json.loads(mongo('print(JSON.stringify(d.Users.findOne({_id:ObjectId("'+t2['id']+'")}).LockoutUntil))'))
    assert locked is not None
    api(Web(web.base), '/api/auth/login', {'email':t2['email'],'password':'Regression@123'}, expected=401)
    passed('eight concurrent failed logins trigger lockout and cannot overwrite roles or grants')

    # Resetting the password cannot delete the independent rate-limit history.
    smtp = state['smtp']
    smtp.reject = False
    reset_passwords = ['HistoryOne@123', 'HistoryTwo@123', 'HistoryThree@123', 'HistoryFour@123']
    for next_password in reset_passwords:  # The baseline already delivered one successful Admin OTP.
        mongo('d.PasswordResetRequestLimits.updateMany({},{$set:{LastRequestedAt:new Date(Date.now()-61000)}})')
        client = Web(web.base)
        assert client.form('/Auth/ForgotPassword',{'Email':'admin@lms.com'})[0] == 200
        msg = email.message_from_bytes(smtp.messages.get(timeout=5),policy=email.policy.default)
        code = re.search(r'\b\d{6}\b',msg.get_body(preferencelist=('plain',)).get_content())[0]
        assert client.form('/Auth/ResetPassword',{'Email':'admin@lms.com','Code':code,
            'NewPassword':next_password,'ConfirmPassword':next_password})[0] == 200
    mongo('d.PasswordResetRequestLimits.updateMany({},{$set:{LastRequestedAt:new Date(Date.now()-61000)}})')
    assert Web(web.base).form('/Auth/ForgotPassword',{'Email':'admin@lms.com'})[0] == 200
    assert smtp.messages.empty(), 'Sixth email exceeded the hourly quota'
    count = mongo('print(d.PasswordResetRequestLimits.findOne().Count)')
    assert count == '5'
    passed('successful resets preserve the hourly OTP quota; sixth email is blocked')

    # Production handler must return a sanitized 500, not fail re-execution with 404.
    with socket.socket() as probe:
        probe.bind(('127.0.0.1',0)); port = probe.getsockname()[1]
    environment = dict(state['environment'], ASPNETCORE_ENVIRONMENT='Production',
        ASPNETCORE_URLS=f'http://127.0.0.1:{port}', Vnpay__TmnCode='', Vnpay__HashSecret='')
    with open(state['folder']/'production.log','w+') as log:
        app = subprocess.Popen([state['args'].dotnet,state['args'].dll],cwd=state['repo'],env=environment,
            stdout=log,stderr=subprocess.STDOUT)
        try:
            client = Web(f'http://127.0.0.1:{port}')
            for _ in range(100):
                try:
                    if client.request('/Auth/Login')[0] == 200:break
                except OSError:time.sleep(.2)
            else:raise AssertionError('Production startup timed out')
            status, body = client.request('/Vnpay/Ipn?vnp_SecureHash=00')
            assert status == 500 and json.loads(body)['status'] == 500
            assert 'Exception' not in body and 'HashSecret' not in body
            log.flush();log.seek(0)
            assert 'exception handler configured' not in log.read().lower()
            passed('Production exceptions return sanitized errors through a real handler')
        finally:
            app.terminate();app.wait(timeout=10)


    bootstrap_db = state['database'] + '_production'
    environment['MongoDb__DatabaseName'] = bootstrap_db
    environment['Bootstrap__AdminEmail'] = 'bootstrap@example.test'
    environment['Bootstrap__AdminPassword'] = 'BootstrapRegression@123'
    with open(state['folder']/'bootstrap.log','w+') as log:
        app = subprocess.Popen([state['args'].dotnet,state['args'].dll],cwd=state['repo'],env=environment,
            stdout=log,stderr=subprocess.STDOUT)
        try:
            client = Web(f'http://127.0.0.1:{port}')
            for _ in range(100):
                try:
                    if client.request('/Auth/Login')[0] == 200:break
                except OSError:time.sleep(.2)
            else:raise AssertionError('Production bootstrap startup timed out')
            count = mongo('print(db.getSiblingDB("'+bootstrap_db+'").Users.countDocuments({}))')
            assert count == '1'
            assert mongo('print(db.getSiblingDB("'+bootstrap_db+'").Users.countDocuments({Email:"admin@lms.com"}))') == '0'
            passed('empty Production database creates only the configured Admin; no default demo credentials')
        finally:
            app.terminate();app.wait(timeout=10)
            mongo('db.getSiblingDB("'+bootstrap_db+'").dropDatabase()')

    return 'HistoryFour@123'


if __name__ == '__main__':
    main(security_checks)
