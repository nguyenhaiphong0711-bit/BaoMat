"""Teacher class-opening proposals, Admin decisions and private notifications."""
import json
import urllib.error
import urllib.request
from password_reset_smoke import Web


def course_opening_checks(state):
    api, web, mongo, passed = (state[k] for k in ('api','web','mongo','passed'))
    teacher, student = Web(web.base), Web(web.base)
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})

    def mutate(client, path, data, expected=200):
        token = api(client, '/api/frontend/csrf')['requestToken']
        request = urllib.request.Request(web.base+path, json.dumps(data).encode(),
            {'Content-Type':'application/json','RequestVerificationToken':token}, method='POST')
        try: response = client.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error: response = error
        body = response.read().decode()
        assert response.status == expected, (path,response.status,expected,body[:300])
        return json.loads(body) if body else None

    options = api(teacher, '/api/course-openings/options')
    subject = next(x for x in options['subjects'] if x['code']=='SMTPTEST')
    term = options['terms'][0]
    form = {'subjectId':subject['id'],'academicTermId':term['id'],'name':'Lớp kinh tế số đề xuất',
        'description':'Lớp do giảng viên đề xuất để Admin duyệt.','enrollmentCapacity':45}
    api(student, '/api/course-openings', expected=403)
    proposal = mutate(teacher, '/api/course-openings', form, 201)
    mutate(teacher, '/api/course-openings', form, 400)
    mutate(teacher, f'/api/course-openings/{proposal["id"]}/decision', {'approve':True}, 403)
    admin_notice = api(web, '/api/notifications')
    assert any(x['type']=='course-opening.created' and proposal['name'] in x['message'] for x in admin_notice['items'])
    mutate(web, f'/api/course-openings/{proposal["id"]}/decision', {'approve':False,'comment':''}, 400)
    mutate(web, f'/api/course-openings/{proposal["id"]}/decision', {'approve':False,'comment':'Cần điều chỉnh kế hoạch phòng học.'}, 204)
    assert api(teacher, '/api/course-openings')[0]['status']=='Rejected'
    assert any(x['type']=='course-opening.decided' for x in api(teacher, '/api/notifications')['items'])
    passed('teachers propose assigned-subject classes; only Admin decides and rejection requires a reason')

    approved = mutate(teacher, '/api/course-openings', dict(form,name='Lớp kinh tế số được duyệt'), 201)
    mutate(web, f'/api/course-openings/{approved["id"]}/decision', {'approve':True,'comment':'Đủ điều kiện mở lớp.'}, 204)
    mutate(web, f'/api/course-openings/{approved["id"]}/decision', {'approve':True}, 409)
    row = next(x for x in api(teacher, '/api/course-openings') if x['id']==approved['id'])
    assert row['status']=='Approved' and row['createdClassId']
    assert mongo('print(d.Classes.findOne({_id:ObjectId("'+row['createdClassId']+'")} ).TeacherId.toString())') == state['teacher_id']
    notices = api(teacher, '/api/notifications')
    unread = notices['unread']; notice = next(x for x in notices['items'] if x['link'].endswith(row['createdClassId']))
    mutate(teacher, f'/api/notifications/{notice["id"]}/read', {}, 204)
    assert api(teacher, '/api/notifications')['unread'] == unread-1
    assert all(x['type']!='course-opening.created' for x in api(student, '/api/notifications')['items'])
    passed('approval creates one scoped class and notifications remain private with read state')
    state['browser_environment']['E2E_OPENING_NAME'] = form['name']

