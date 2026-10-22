"""Operational health, safe AI fallback and a small concurrent read smoke test."""
import concurrent.futures
import http.server
import json
import socket
import ssl
import subprocess
import threading
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

    class OpenAiStub(http.server.BaseHTTPRequestHandler):
        request_body = None
        def do_POST(self):
            length = int(self.headers.get('Content-Length', '0'))
            OpenAiStub.request_body = json.loads(self.rfile.read(length))
            answer = json.dumps({'content':'Nguyên tắc nào giới hạn quyền đúng mức cần thiết?',
                'options':['Đặc quyền tối thiểu','Toàn quyền','Không xác thực','Dùng chung tài khoản'],
                'correctOption':0,'explanation':'Mỗi tài khoản chỉ nhận quyền cần cho nhiệm vụ.'}, ensure_ascii=False)
            body = json.dumps({'id':'resp_test','output':[{'type':'message','content':[
                {'type':'output_text','text':answer}]}]}).encode()
            self.send_response(200); self.send_header('Content-Type','application/json')
            self.send_header('Content-Length',str(len(body))); self.end_headers(); self.wfile.write(body)
        def log_message(self, *_): pass

    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0)); ai_port = probe.getsockname()[1]
    stub = http.server.ThreadingHTTPServer(('127.0.0.1', ai_port), OpenAiStub)
    tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    tls.load_cert_chain(state['folder']/'cert.pem', state['folder']/'key.pem')
    stub.socket = tls.wrap_socket(stub.socket, server_side=True)
    threading.Thread(target=stub.serve_forever, daemon=True).start()
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0)); app_port = probe.getsockname()[1]
    ai_environment = dict(state['environment'], ASPNETCORE_URLS=f'http://127.0.0.1:{app_port}',
        Ai__Enabled='true', Ai__Endpoint=f'https://localhost:{ai_port}/v1/responses',
        Ai__ApiKey='integration-test-key', Ai__Model='gpt-test')
    with open(state['folder']/'ai-app.log','w+') as log:
        app = subprocess.Popen([state['args'].dotnet,state['args'].dll], cwd=state['repo'],
            env=ai_environment,stdout=log,stderr=subprocess.STDOUT)
        try:
            ai_web = Web(f'http://127.0.0.1:{app_port}')
            for _ in range(100):
                try:
                    if ai_web.request('/health/live')[0] == 200: break
                except OSError: time.sleep(.2)
            else: raise AssertionError('AI integration app startup timed out')
            api(ai_web, '/api/auth/login', {'email':'teacher@lms.com','password':'Teacher@123'})
            token = api(ai_web, '/api/frontend/csrf')['requestToken']
            request = urllib.request.Request(ai_web.base+'/api/questions/ai-draft',json.dumps(payload).encode(),
                {'Content-Type':'application/json','RequestVerificationToken':token},method='POST')
            response = ai_web.opener.open(request,timeout=30)
            created = json.loads(response.read())
            assert response.status == 201 and created['state'] == 'Draft' and created['aiAssisted']
            assert OpenAiStub.request_body['text']['format']['type'] == 'json_schema'
            assert mongo('print(d.LearningEvents.countDocuments({Type:"question.ai-draft",EntityId:ObjectId("'+created['id']+'" )}))') == '1'
            mongo('d.Questions.deleteOne({_id:ObjectId("'+created['id']+'")});d.LearningEvents.deleteMany({EntityId:ObjectId("'+created['id']+'")})')
            passed('OpenAI Responses structured output creates only an audited question draft')
        finally:
            app.terminate(); app.wait(timeout=10); stub.shutdown(); stub.server_close()

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

    student_dashboard = api(student, '/api/portal/student/dashboard')
    assert 'courses' in student_dashboard and 'tuitionItems' in student_dashboard
    assert isinstance(api(student, '/api/portal/student/registrations'), list)
    api(teacher, '/api/portal/student/dashboard', expected=403)
    assert isinstance(api(web, '/api/portal/admin/terms'), list)
    assert isinstance(api(web, '/api/portal/admin/programs'), list)
    assert isinstance(api(web, '/api/portal/admin/registration-reviews'), list)
    permissions = api(web, '/api/portal/admin/permissions')
    assert {'users','permissions','departments','programs','subjects'} <= permissions.keys()
    api(student, '/api/portal/admin/activity-logs', expected=403)
    passed('React portal APIs expose student/admin workflows and enforce role boundaries')

    limited = Web(web.base)
    statuses = []
    for _ in range(70):
        status, _ = limited.form('/Auth/Login',
            {'Email':'rate-limit@example.test','Password':'Invalid@123','RememberMe':'false'})
        statuses.append(status)
        if status == 429: break
    assert statuses[-1] == 429 and all(x in (200,429) for x in statuses)
    passed('per-IP login limiter rejects excessive requests without authenticating')
