"""Role-scoped aggregate reports and privacy-safe learning events."""
import json
from password_reset_smoke import Web, main
from permission_integration import permission_checks
from staff_integration import staff_checks
from resources_integration import resource_checks
from questions_integration import question_checks
from quizzes_integration import quiz_checks
from support_integration import support_checks
from course_openings_integration import course_opening_checks
from operations_integration import operation_checks


def report_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api','web','mongo','passed'))
    teacher, student, staff = Web(web.base), Web(web.base), Web(web.base)
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
    api(staff, '/api/auth/login', {'email':'staff@example.test','password':'StaffTest@123'})
    staff_id = state['browser_environment']['E2E_STAFF_ID']
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:"reports.view"}})')
    outsider_id = mongo('print(d.Users.findOne({Email:"resource-peer@example.test"})._id.toString())')
    mongo('d.LearningEvents.insertOne({Type:"test.hidden",ActorId:ObjectId("'+outsider_id+'"),ActorRole:"Student",StudentId:ObjectId("'+outsider_id+'"),EntityType:"Test",EntityId:ObjectId(),Summary:"HIDDEN OUTSIDE EVENT",OccurredAt:new Date()})')
    mongo('d.LearningEvents.insertOne({Type:"test.admin",ActorId:ObjectId("'+state['student_id']+'"),ActorRole:"Student",StudentId:ObjectId("'+state['student_id']+'"),DepartmentId:ObjectId("'+state['general']['id']+'"),EntityType:"Test",EntityId:ObjectId(),Summary:"VISIBLE OWN EVENT",OccurredAt:new Date()})')

    student_report = api(student, '/api/reports/learning')
    assert student_report['scopeName'] == 'Tiến độ của tôi'
    assert 'VISIBLE OWN EVENT' in [x['summary'] for x in student_report['recentEvents']]
    assert 'HIDDEN OUTSIDE EVENT' not in [x['summary'] for x in student_report['recentEvents']]
    assert {x['key'] for x in student_report['metrics']} >= {'classes','lessonsCompleted','assignments','submissions','quizAverage','openSupport'}
    assert api(student, '/api/reports/learning?from=2999-01-01')['recentEvents'] == []
    csv_status, csv_body = student.request('/api/reports/learning.csv?from=2020-01-01', method='GET')
    assert csv_status == 200 and 'Chỉ số' in csv_body
    api(student, '/api/reports/learning?from=2025-02-01&to=2025-01-01', expected=400)
    teacher_report = api(teacher, '/api/reports/learning')
    assert teacher_report['scopeName'] == 'Các lớp đang phụ trách'
    assert all(x['summary'] != 'HIDDEN OUTSIDE EVENT' for x in teacher_report['recentEvents'])
    passed('student and teacher reports derive scope from identity and class ownership; foreign events never appear')

    staff_report = api(staff, '/api/reports/learning')
    assert staff_report['scopeName'].startswith('Khoa ')
    assert {x['key'] for x in staff_report['metrics']} >= {'students','classes','quizAttempts','quizAverage','openSupport'}
    assert sum(x['value'] for x in staff_report['supportStatuses']) >= 1
    admin_report = api(web, '/api/reports/learning')
    assert admin_report['scopeName'] == 'Toàn hệ thống'
    assert 'HIDDEN OUTSIDE EVENT' in [x['summary'] for x in admin_report['recentEvents']]
    body = json.dumps(admin_report).lower()
    assert all(secret not in body for secret in ('passwordhash','teachercomment','em cần hỗ trợ quyền truy cập','2 + 2 = 4'))
    assert int(mongo('print(d.LearningEvents.countDocuments({Type:"quiz.started",StudentId:ObjectId("'+state['student_id']+'")}))')) == 1
    passed('department and Admin reports aggregate allowed records; learning events contain no answers, support messages or credentials')

    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$pull:{PermissionCodes:"reports.view"}})')
    api(staff, '/api/reports/learning', expected=403)
    mongo('d.Users.updateOne({_id:ObjectId("'+staff_id+'")},{$addToSet:{PermissionCodes:"reports.view"}})')
    mongo('d.Departments.updateOne({_id:ObjectId("'+state['general']['id']+'")},{$set:{IsArchived:true}})')
    api(staff, '/api/reports/learning', expected=403)
    mongo('d.Departments.updateOne({_id:ObjectId("'+state['general']['id']+'")},{$set:{IsArchived:false}})')
    passed('report permission revocation and archived department scope apply immediately to existing sessions')


def extra_checks(state):
    staff_checks(state); resource_checks(state); question_checks(state); quiz_checks(state); support_checks(state); report_checks(state); course_opening_checks(state); operation_checks(state)


if __name__ == '__main__':
    main(lambda state: permission_checks(state, extra_checks))
