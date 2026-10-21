"""Question bank validation, review workflow and role/department boundaries on an isolated DB."""
import json
import urllib.error
import urllib.request
from password_reset_smoke import Web, main
from permission_integration import permission_checks
from staff_integration import staff_checks
from resources_integration import resource_checks


def question_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api', 'web', 'mongo', 'passed'))
    teacher, student, staff = Web(web.base), Web(web.base), Web(web.base)
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
    api(staff, '/api/auth/login', {'email':'staff@example.test','password':'StaffTest@123'})
    staff_id = state['browser_environment']['E2E_STAFF_ID']
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:{$each:["questions.view","questions.review"]}}})')

    def request(client, path, data, method='POST', expected=200):
        token = api(client, '/api/frontend/csrf')['requestToken']
        req = urllib.request.Request(web.base+path, json.dumps(data).encode(),
            {'Content-Type':'application/json','RequestVerificationToken':token}, method=method)
        try: response = client.opener.open(req, timeout=30)
        except urllib.error.HTTPError as error: response = error
        body = response.read().decode()
        assert response.status == expected, (path, response.status, expected, body[:300])
        return json.loads(body) if body else None

    subjects = api(teacher, '/api/questions/subjects')
    subject_id = next(x for x in subjects if x['code'] == 'SMTPTEST')['id']
    form = {'subjectId':subject_id, 'topic':'An toàn tài khoản', 'difficulty':'Medium',
        'content':'Biện pháp nào bảo vệ tài khoản tốt nhất?',
        'options':['Dùng mật khẩu mạnh','Chia sẻ mật khẩu','Tắt khóa màn hình','Dùng chung tài khoản'],
        'correctOption':0, 'explanation':'Mật khẩu mạnh và riêng biệt giúp giảm rủi ro.'}
    request(student, '/api/questions', form, expected=403)
    request(teacher, '/api/questions', dict(form, options=['Giống nhau','giống nhau']), expected=400)
    request(teacher, '/api/questions', dict(form, correctOption=5), expected=400)
    foreign_subject = mongo('print(d.Subjects.findOne({Code:"STAFFOTHER"})._id.toString())')
    request(teacher, '/api/questions', dict(form, subjectId=foreign_subject), expected=400)
    question = request(teacher, '/api/questions', form, expected=201)
    qid = question['id']
    assert question['state'] == 'Draft' and question['options'][0]['id'] == 'A' and question['correctOption'] == 0
    api(student, '/api/questions', expected=403)
    assert not any(x['id'] == qid for x in api(staff, '/api/questions')['items'])
    passed('question drafts validate assigned subjects, unique choices and answer bounds; students cannot read the answer bank')

    question = request(teacher, f'/api/questions/{qid}/submit', {'revision':question['revision']})
    request(teacher, f'/api/questions/{qid}/review', {'revision':question['revision'],'approve':True}, expected=403)
    request(web, f'/api/questions/{qid}/review', {'revision':question['revision']-1,'approve':True}, expected=409)
    request(staff, f'/api/questions/{qid}/review', {'revision':question['revision'],'approve':False}, expected=400)
    question = request(staff, f'/api/questions/{qid}/review', {'revision':question['revision'],'approve':False,'comment':'Cần làm rõ lựa chọn đúng'})
    assert question['state'] == 'Rejected' and question['reviewerName'] == 'Staff integration'
    question = request(teacher, f'/api/questions/{qid}', dict(form, revision=question['revision'], explanation='Mật khẩu mạnh, riêng biệt và MFA giúp bảo vệ tài khoản.'), method='PUT')
    assert question['state'] == 'Draft' and not question['reviewerName']
    question = request(teacher, f'/api/questions/{qid}/submit', {'revision':question['revision']})
    question = request(staff, f'/api/questions/{qid}/review', {'revision':question['revision'],'approve':True,'comment':'Đã kiểm tra nội dung và đáp án'})
    assert question['state'] == 'Approved'
    peer = api(web, '/api/users', {'fullName':'Question peer','email':'question-peer@example.test','password':'QuestionTest@123','role':'Teacher'}, expected=201)
    api(web, '/api/teachers/'+peer['id']+'/subjects', {'subjectIds':[subject_id]}, 'PUT', 204)
    peer_client = Web(web.base); api(peer_client, '/api/auth/login', {'email':peer['email'],'password':'QuestionTest@123'})
    api(peer_client, '/api/questions/'+qid, expected=404)
    question = request(teacher, f'/api/questions/{qid}/publish', {'revision':question['revision']})
    assert api(peer_client, '/api/questions/'+qid)['correctOption'] == 0
    passed('a different scoped reviewer is mandatory; rejected questions return to draft and approved questions require explicit publication')

    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$set:{DepartmentId:ObjectId("'+state['staff_other_department_id']+'")}})') if 'staff_other_department_id' in state else None
    cross = request(web, '/api/questions', dict(form, subjectId=foreign_subject, content='Câu hỏi khác khoa?'), expected=201)
    cross = request(web, f'/api/questions/{cross["id"]}/submit', {'revision':cross['revision']})
    request(staff, f'/api/questions/{cross["id"]}/review', {'revision':cross['revision'],'approve':True}, expected=403)
    own = request(web, '/api/questions', dict(form, content='Admin có tự duyệt được không?'), expected=201)
    own = request(web, f'/api/questions/{own["id"]}/submit', {'revision':own['revision']})
    request(web, f'/api/questions/{own["id"]}/review', {'revision':own['revision'],'approve':True}, expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+state['teacher_id']+'")},{$pull:{PermissionCodes:"questions.view"}})')
    api(teacher, '/api/questions/'+qid, expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+state['teacher_id']+'")},{$addToSet:{PermissionCodes:{$each:["questions.view","questions.manage"]}}})')
    question = request(teacher, f'/api/questions/{qid}/archive', {'revision':question['revision']})
    api(peer_client, '/api/questions/'+qid, expected=404)
    assert api(teacher, '/api/questions/'+qid)['isArchived']
    passed('runtime revocation, cross-department review, self-review and archive boundaries are enforced')
    state['browser_environment'].update(E2E_QUESTION_SUBJECT_ID=subject_id)


def extra_checks(state):
    staff_checks(state)
    # Preserve all prior resource coverage before exercising the next module.
    resource_checks(state)
    question_checks(state)


if __name__ == '__main__':
    main(lambda state: permission_checks(state, extra_checks))
