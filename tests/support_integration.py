"""Learner support ownership, department scope, assignment and conversation workflow."""
import json
import urllib.error
import urllib.request
from password_reset_smoke import Web, main
from permission_integration import permission_checks
from staff_integration import staff_checks
from resources_integration import resource_checks
from questions_integration import question_checks
from quizzes_integration import quiz_checks


def support_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api','web','mongo','passed'))
    student, teacher, staff = Web(web.base), Web(web.base), Web(web.base)
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(staff, '/api/auth/login', {'email':'staff@example.test','password':'StaffTest@123'})
    staff_id = state['browser_environment']['E2E_STAFF_ID']
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:{$each:["support.view","support.manage"]}}})')

    def request(client, path, data=None, method='POST', expected=200):
        token = api(client, '/api/frontend/csrf')['requestToken']
        req = urllib.request.Request(web.base+path, json.dumps(data or {}).encode(),
            {'Content-Type':'application/json','RequestVerificationToken':token}, method=method)
        try: response = client.opener.open(req, timeout=30)
        except urllib.error.HTTPError as error: response = error
        body = response.read().decode()
        assert response.status == expected, (path,response.status,expected,body[:400])
        return json.loads(body) if body else None

    form = {'subject':'Không truy cập được tài liệu','category':'Học tập','priority':'High',
        'content':'Em cần hỗ trợ quyền truy cập.','studentId':staff_id,
        'departmentId':mongo('print(d.Departments.findOne({Code:"STAFFOTHER"})._id.toString())'),
        'status':'Closed','assignedStaffId':staff_id}
    request(teacher, '/api/support', form, expected=403)
    ticket = request(student, '/api/support', form, expected=201)
    tid = ticket['id']
    assert ticket['status'] == 'Open' and ticket['studentName'] == 'Nguyễn Hải Phong'
    assert ticket['departmentName'] != 'Other department' and ticket['assignedStaffId'] is None
    listed = api(student, '/api/support')['items'][0]
    assert listed['messages'] == []
    outsider = Web(web.base); api(outsider, '/api/auth/login', {'email':'resource-peer@example.test','password':'LibraryTest@123'})
    api(outsider, '/api/support/'+tid, expected=404)
    passed('ticket identity, department and state come from the authenticated student; list summaries omit conversation content')

    outside_staff = api(web, '/api/users', {'fullName':'Outside support staff','email':'support-outside@example.test',
        'password':'SupportTest@123','role':'Staff'}, expected=201)
    other_department = mongo('print(d.Departments.findOne({Code:"STAFFOTHER"})._id.toString())')
    mongo('d.Users.updateOne({_id:ObjectId("'+outside_staff['id']+'")},{$set:{DepartmentId:ObjectId("'+other_department+'")}})')
    outside_staff_client = Web(web.base); api(outside_staff_client, '/api/auth/login', {'email':outside_staff['email'],'password':'SupportTest@123'})
    api(outside_staff_client, '/api/support/'+tid, expected=404)
    assert not api(outside_staff_client, '/api/support')['items']
    staff_view = api(staff, '/api/support/'+tid)
    eligible = api(staff, f'/api/support/{tid}/staff')
    assert staff_id in [x['id'] for x in eligible] and outside_staff['id'] not in [x['id'] for x in eligible]
    request(staff, '/api/support/'+tid, {'revision':staff_view['revision'],'status':'InProgress','assignedStaffId':outside_staff['id']}, method='PUT', expected=403)
    ticket = request(staff, f'/api/support/{tid}/messages', {'revision':staff_view['revision'],'content':'Cán bộ đang kiểm tra quyền truy cập.'})
    assert ticket['status'] == 'InProgress' and ticket['assignedStaffId'] == staff_id
    request(student, f'/api/support/{tid}/messages', {'revision':staff_view['revision'],'content':'Tin nhắn từ màn hình cũ'}, expected=409)
    passed('only active Staff in the ticket department can see, claim or reply; stale conversation updates are rejected')

    ticket = request(student, f'/api/support/{tid}/messages', {'revision':ticket['revision'],'content':'Em đã cung cấp thêm thông tin.'})
    ticket = request(staff, '/api/support/'+tid, {'revision':ticket['revision'],'status':'Resolved','assignedStaffId':staff_id}, method='PUT')
    assert ticket['status'] == 'Resolved'
    ticket = request(student, f'/api/support/{tid}/messages', {'revision':ticket['revision'],'content':'Vấn đề vẫn còn xảy ra.'})
    assert ticket['status'] == 'Open'
    ticket = request(staff, '/api/support/'+tid, {'revision':ticket['revision'],'status':'Closed','assignedStaffId':staff_id}, method='PUT')
    request(student, f'/api/support/{tid}/messages', {'revision':ticket['revision'],'content':'Không thể gửi sau khi đóng'}, expected=400)
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$pull:{PermissionCodes:"support.manage"}})')
    assert api(staff, '/api/support/'+tid)['canManage'] is False
    request(staff, '/api/support/'+tid, {'revision':ticket['revision'],'status':'Open'}, method='PUT', expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:"support.manage"}})')
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$pull:{PermissionCodes:"support.view"}})')
    api(student, '/api/support/'+tid, expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$addToSet:{PermissionCodes:{$each:["support.view","support.create"]}}})')
    passed('student replies reopen resolved tickets, closed tickets reject messages and runtime permission revocation applies immediately')
    state['browser_environment'].update(E2E_SUPPORT_TICKET_ID=tid)


def extra_checks(state):
    staff_checks(state); resource_checks(state); question_checks(state); quiz_checks(state); support_checks(state)


if __name__ == '__main__':
    main(lambda state: permission_checks(state, extra_checks))
