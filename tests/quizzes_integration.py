"""Quiz creation, answer secrecy, attempt ownership and server-side grading on an isolated DB."""
import datetime
import json
import urllib.error
import urllib.request
from password_reset_smoke import Web, main
from permission_integration import permission_checks
from staff_integration import staff_checks
from resources_integration import resource_checks
from questions_integration import question_checks


def quiz_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api','web','mongo','passed'))
    teacher, student, staff = Web(web.base), Web(web.base), Web(web.base)
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
    api(staff, '/api/auth/login', {'email':'staff@example.test','password':'StaffTest@123'})

    def request(client, path, data=None, method='POST', expected=200):
        token = api(client, '/api/frontend/csrf')['requestToken']
        raw = json.dumps(data or {}).encode()
        req = urllib.request.Request(web.base+path, raw,
            {'Content-Type':'application/json','RequestVerificationToken':token}, method=method)
        try: response = client.opener.open(req, timeout=30)
        except urllib.error.HTTPError as error: response = error
        body = response.read().decode()
        assert response.status == expected, (path,response.status,expected,body[:400])
        return json.loads(body) if body else None

    subject_id = state['browser_environment']['E2E_QUESTION_SUBJECT_ID']
    qform = {'subjectId':subject_id,'topic':'Kiểm tra tự động','difficulty':'Easy',
        'content':'Hai cộng hai bằng bao nhiêu?','options':['4','3','5','22'],'correctOption':0,
        'explanation':'2 + 2 = 4.'}
    question = request(teacher, '/api/questions', qform, expected=201)
    question = request(teacher, f'/api/questions/{question["id"]}/submit', {'revision':question['revision']})
    question = request(staff, f'/api/questions/{question["id"]}/review', {'revision':question['revision'],'approve':True})
    question = request(teacher, f'/api/questions/{question["id"]}/publish', {'revision':question['revision']})
    now = datetime.datetime.now(datetime.timezone.utc)
    form = {'classId':state['cid'],'title':'Bài kiểm tra bảo mật','instructions':'Chọn một đáp án.',
        'durationMinutes':30,'opensAt':(now-datetime.timedelta(minutes=1)).isoformat(),
        'closesAt':(now+datetime.timedelta(hours=1)).isoformat(),'questionIds':[question['id']]}
    options = api(teacher, '/api/quizzes/options?classId='+state['cid'])
    assert question['id'] in [q['id'] for q in options['questions']]
    request(teacher, '/api/quizzes', dict(form, questionIds=[]), expected=400)
    request(teacher, '/api/quizzes', dict(form, questionIds=[question['id'],question['id']]), expected=400)
    request(teacher, '/api/quizzes', dict(form, closesAt=(now-datetime.timedelta(minutes=2)).isoformat()), expected=400)
    quiz = request(teacher, '/api/quizzes', form, expected=201)
    qid = quiz['quiz']['id']
    api(student, '/api/quizzes/'+qid, expected=404)
    request(student, f'/api/quizzes/{qid}/attempts', expected=403)
    passed('quiz drafts accept only published questions from the class subject and are hidden from students')

    quiz = request(teacher, f'/api/quizzes/{qid}/publish', {'revision':quiz['quiz']['revision']})
    detail = api(student, '/api/quizzes/'+qid)
    serialized = json.dumps(detail).lower()
    assert 'correctoption' not in serialized and question['explanation'].lower() not in serialized and detail['canStart']
    request(teacher, f'/api/quizzes/{qid}/attempts', expected=403)
    outside = Web(web.base); api(outside, '/api/auth/login', {'email':'resource-peer@example.test','password':'LibraryTest@123'})
    api(outside, '/api/quizzes/'+qid, expected=404)
    request(outside, f'/api/quizzes/{qid}/attempts', expected=403)
    attempt = request(student, f'/api/quizzes/{qid}/attempts')
    assert attempt['submittedAt'] is None and attempt['questions'][0]['correctOption'] is None and attempt['questions'][0]['explanation'] == ''
    assert request(student, f'/api/quizzes/{qid}/attempts')['id'] == attempt['id']
    api(outside, '/api/quizzes/attempts/'+attempt['id'], expected=404)
    passed('published quiz metadata never exposes answers; only enrolled students can start and repeated starts reuse one attempt')

    request(student, f'/api/quizzes/attempts/{attempt["id"]}/submit', {'answers':[9]}, expected=400)
    result = request(student, f'/api/quizzes/attempts/{attempt["id"]}/submit', {'answers':[0]})
    assert result['score'] == 10 and result['questions'][0]['correctOption'] == 0 and result['questions'][0]['explanation']
    repeated = request(student, f'/api/quizzes/attempts/{attempt["id"]}/submit', {'answers':[1]})
    assert repeated['score'] == 10 and repeated['answers'] == [0]
    teacher_view = api(teacher, '/api/quizzes/'+qid)
    assert teacher_view['attempts'][0]['studentId'] == state['student_id'] and teacher_view['attempts'][0]['score'] == 10
    timeout_user = api(web, '/api/users', {'fullName':'Quiz timeout student','email':'quiz-timeout@example.test',
        'password':'QuizTimeout@123','role':'Student'}, expected=201)
    mongo('d.Classes.updateOne({_id:ObjectId("'+state['cid']+'")},{$addToSet:{StudentIds:ObjectId("'+timeout_user['id']+'")}})')
    timeout_client = Web(web.base); api(timeout_client, '/api/auth/login', {'email':timeout_user['email'],'password':'QuizTimeout@123'})
    expired = request(timeout_client, f'/api/quizzes/{qid}/attempts')
    mongo('d.QuizAttempts.updateOne({_id:ObjectId("'+expired['id']+'")},{$set:{ExpiresAt:new Date(0)}})')
    mongo('d.Users.updateOne({_id:ObjectId("'+timeout_user['id']+'")},{$pull:{PermissionCodes:"quizzes.attempt"}})')
    expired = api(timeout_client, '/api/quizzes/attempts/'+expired['id'])
    assert expired['submittedAt'] and expired['score'] == 0
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$pull:{PermissionCodes:"quizzes.view"}})')
    api(student, '/api/quizzes', expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+state['student_id']+'")},{$addToSet:{PermissionCodes:{$each:["quizzes.view","quizzes.attempt"]}}})')
    request(teacher, f'/api/quizzes/{qid}/archive', {'revision':quiz['quiz']['revision']-1}, expected=409)
    quiz = request(teacher, f'/api/quizzes/{qid}/archive', {'revision':quiz['quiz']['revision']})
    api(student, '/api/quizzes/'+qid, expected=404)
    assert api(student, '/api/quizzes/attempts/'+attempt['id'])['score'] == 10
    passed('answers are validated and graded once on the server; results survive archive while runtime permission revocation applies immediately')
    state['browser_environment'].update(E2E_QUIZ_CLASS_ID=state['cid'], E2E_QUIZ_QUESTION_ID=question['id'])


def extra_checks(state):
    staff_checks(state)
    resource_checks(state)
    question_checks(state)
    quiz_checks(state)


if __name__ == '__main__':
    main(lambda state: permission_checks(state, extra_checks))
