"""Operational health, safe AI fallback and a small concurrent read smoke test."""
import concurrent.futures
import json
import time
import urllib.error
import urllib.request
import urllib.parse
from password_reset_smoke import Web


def operation_checks(state):
    api, web, mongo, passed = (state[key] for key in ('api', 'web', 'mongo', 'passed'))
    assert Web(web.base).request('/health/live')[0] == 200
    assert Web(web.base).request('/health/ready')[0] == 200
    response = urllib.request.urlopen(web.base + '/health/live')
    assert response.headers['X-Frame-Options'] == 'DENY'
    assert response.headers['X-Content-Type-Options'] == 'nosniff'
    assert 'camera=()' in response.headers['Permissions-Policy']
    assert "frame-ancestors 'none'" in response.headers['Content-Security-Policy']
    assert response.headers['X-Request-Id']
    passed('anonymous liveness and MongoDB readiness probes are healthy')

    teacher, student = Web(web.base), Web(web.base)
    api(teacher, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
    api(student, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
    subject_id = state['browser_environment']['E2E_QUESTION_SUBJECT_ID']
    payload = {'subjectId':subject_id, 'topic':'Kiểm soát truy cập', 'difficulty':'Medium',
        'learningObjective':'Nhận biết nguyên tắc phân quyền tối thiểu'}

    def ai_request(client, expected):
        token = api(client, '/api/frontend/csrf')['requestToken']
        request = urllib.request.Request(web.base + '/api/questions/ai-draft', json.dumps(payload).encode(),
            {'Content-Type':'application/json','RequestVerificationToken':token}, method='POST')
        try: response = client.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error: response = error
        assert response.status == expected, (response.status, expected, response.read().decode()[:300])

    before = mongo('print(d.Questions.countDocuments({AiAssisted:true}))')
    ai_request(teacher, 503)
    ai_request(student, 403)
    assert mongo('print(d.Questions.countDocuments({AiAssisted:true}))') == before
    passed('disabled AI fails closed, creates no draft and remains unavailable to students')

    paths = ('/health/ready', '/api/classes', '/api/reports/learning')
    def worker(_):
        client = Web(web.base)
        started = time.monotonic()
        api(client, '/api/auth/login', {'email':'student@lms.com','password':'Student@123'})
        statuses = [client.request(paths[index % len(paths)])[0] for index in range(12)]
        return statuses, time.monotonic() - started

    started = time.monotonic()
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        results = list(pool.map(worker, range(8)))
    statuses = [status for result, _ in results for status in result]
    assert len(statuses) == 96 and all(status == 200 for status in statuses)
    assert time.monotonic() - started < 30
    passed('96 concurrent authenticated and health reads complete without errors')

    limited = Web(web.base)
    statuses = []
    for _ in range(70):
        status, _ = limited.form('/Auth/Login',
            {'Email':'rate-limit@example.test','Password':'Invalid@123','RememberMe':'false'})
        statuses.append(status)
        if status == 429: break
    assert statuses[-1] == 429 and all(x in (200,429) for x in statuses)
    passed('per-IP login limiter rejects excessive requests without authenticating')
