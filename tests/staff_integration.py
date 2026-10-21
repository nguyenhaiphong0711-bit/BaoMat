"""Department-scoped Staff access, combined with the existing permission and browser regressions."""
import html
import json
import re
import urllib.parse
import urllib.request
from password_reset_smoke import Web, main
from permission_integration import permission_checks


def staff_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api', 'web', 'mongo', 'passed'))
    password = 'StaffTest@123'
    created = api(web, '/api/users', {'fullName':'Staff integration', 'email':'staff@example.test',
        'password':password, 'role':'Staff'}, expected=201)
    sid = created['id']
    staff = Web(web.base)
    api(staff, '/api/auth/login', {'email':created['email'], 'password':password})
    assert set(api(staff, '/api/auth/me')['permissionCodes']) == {'staff.students.view', 'staff.progress.view'}
    api(staff, '/api/staff/students', expected=403)
    general = state['general']['id']
    other = api(web, '/api/departments', {'code':'STAFFOTHER','name':'Other department'}, expected=201)
    outsider = api(web, '/api/users', {'fullName':'Outside staff scope','email':'outside-scope@example.test','password':password,'role':'Student'}, expected=201)
    mongo('d.Users.updateOne({_id:ObjectId("'+outsider['id']+'")},{$set:{DepartmentId:ObjectId("'+other['id']+'")}})')
    def assign_scope(department, codes=('staff.students.view','staff.progress.view')):
        status, body = web.request('/Permission/Index?userId='+sid)
        assert status == 200
        token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body)[1])
        data=urllib.parse.urlencode({'Assignment.UserId':sid,'Assignment.DepartmentId':department,
            'Assignment.PermissionCodes':list(codes),'__RequestVerificationToken':token},doseq=True).encode()
        with web.opener.open(urllib.request.Request(web.base+'/Permission/Assign',data),timeout=30) as response:
            assert response.status == 200
    assign_scope(general)
    directory = api(staff, '/api/staff/students')
    assert state['student_id'] in [s['id'] for s in directory['items']]
    assert outsider['id'] not in [s['id'] for s in directory['items']]
    assert len(directory['items']) <= 20
    for item in directory['items']:
        assert set(item) == {'id','fullName','email','programName','currentSemester','isActive'}
    api(staff, '/api/staff/students/'+outsider['id']+'/progress', expected=404)
    api(staff, '/api/staff/students/000000000000000000000001/progress', expected=404)
    api(staff, '/api/staff/students/not-an-id/progress', expected=404)
    api(staff, '/api/staff/students?page=0', expected=400)
    assert api(staff, '/api/staff/students?q='+urllib.parse.quote('.*'))['total'] == 0
    # Client-supplied department parameters must not expand the server's scope.
    forged = api(staff, '/api/staff/students?departmentId='+other['id'])
    assert outsider['id'] not in [s['id'] for s in forged['items']]
    passed('Staff requires an assigned active department; directory/search/pagination cannot reveal students from another department')

    # A student may attend a cross-department class: Staff must not receive its details in reports.
    teacher = api(web, '/api/users', {'fullName':'Other department teacher','email':'staff-other-teacher@example.test','password':password,'role':'Teacher'}, expected=201)
    subject = api(web, '/api/subjects', {'departmentId':other['id'],'code':'STAFFOTHER','name':'Other department subject'}, expected=201)
    api(web, '/api/teachers/'+teacher['id']+'/subjects', {'subjectIds':[subject['id']]}, 'PUT', 204)
    classroom = api(web, '/api/classes', {'name':'Hidden cross-department class','subjectId':subject['id'],
        'teacherId':teacher['id'],'studentIds':[state['student_id']]}, expected=201)
    before = int(mongo('print(d.TuitionInvoices.countDocuments({}))'))
    report = api(staff, '/api/staff/students/'+state['student_id']+'/progress')
    assert any(c['className']=='Regression class' for c in report['courses'])
    assert all(c['className']!='Hidden cross-department class' for c in report['courses'])
    assert int(mongo('print(d.TuitionInvoices.countDocuments({}))')) == before
    body = json.dumps(report)
    assert all(secret not in body for secret in ('passwordHash','teacherComment','attachments','lockoutUntil','Content'))
    passed('Staff reports include only department classes and summary grades; no submission content, files or invoice side effects')

    for path in ('/api/users', '/api/activity-logs', '/api/classes', '/api/assignments/'+state['aid']+'/submissions'):
        api(staff, path, expected=403)
    api(staff, '/api/submissions/'+state['submission']['id']+'/grade', {'grade':10}, 'PUT', 403)
    api(staff, '/api/classes/'+state['cid']+'/lessons', {'title':'Forged','content':'x'}, expected=403)
    for role, password_for_role in [('teacher','Teacher@123'),('student','Student@123')]:
        client=Web(web.base)
        api(client,'/api/auth/login',{'email':role+'@lms.com','password':password_for_role})
        api(client,'/api/staff/students',expected=403)
    # Legacy injected teacher rights and old student roster membership cannot expand Staff capabilities.
    mongo('d.Users.updateOne({_id:ObjectId("'+sid+'")},{$addToSet:{PermissionCodes:{$each:["lessons.manage","classes.view","submissions.review"]}}})')
    assert set(api(staff, '/api/auth/me')['permissionCodes']) == {'staff.students.view','staff.progress.view'}
    passed('Staff cannot grade/manage classes/users; teachers/students cannot call Staff APIs even with forged grants')

    assign_scope(general, ('staff.students.view',))
    api(staff, '/api/staff/students/'+state['student_id']+'/progress', expected=403)
    assign_scope(other['id'])
    assert outsider['id'] in [s['id'] for s in api(staff, '/api/staff/students')['items']]
    api(staff, '/api/staff/students/'+state['student_id']+'/progress', expected=404)
    assign_scope('')
    api(staff, '/api/staff/students', expected=403)
    assign_scope(general)
    mongo('d.Departments.updateOne({_id:ObjectId("'+general+'")},{$set:{IsArchived:true}})')
    api(staff, '/api/staff/students', expected=403)
    mongo('d.Departments.updateOne({_id:ObjectId("'+general+'")},{$set:{IsArchived:false}})')
    passed('revoked Staff permissions, reassignment, missing scope and archived departments apply to existing sessions immediately')
    state['browser_environment']={'E2E_STAFF_EMAIL':created['email'],'E2E_STAFF_PASSWORD':password,
        'E2E_STAFF_ID':sid,'E2E_STAFF_STUDENT_ID':state['student_id'],'E2E_STAFF_OUTSIDER_ID':outsider['id']}


if __name__ == '__main__':
    main(lambda state: permission_checks(state, staff_checks))
