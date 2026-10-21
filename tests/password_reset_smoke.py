"""Integration regression: real app + MongoDB + trusted local TLS SMTP.

Build first, then run:
python3 tests/password_reset_smoke.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
Requires openssl, Docker, and the local baomat-mongo container. No real email is sent.
"""
import argparse
import email
import email.policy
import html
import http.cookiejar
import json
import os
from pathlib import Path
import queue
import re
import socket
import socketserver
import ssl
import sys
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone


class SMTP(socketserver.StreamRequestHandler):
    def handle(self):
        if self.server.implicit_tls:
            self.connection = self.server.tls.wrap_socket(self.connection, server_side=True)
            self.rfile = self.connection.makefile('rb')
            self.wfile = self.connection.makefile('wb')
        def reply(line):
            self.wfile.write((line + '\r\n').encode())
            self.wfile.flush()
        reply('220 localhost test SMTP')
        while line := self.rfile.readline():
            command = line.decode().strip().upper()
            if command.startswith(('EHLO', 'HELO')):
                reply('250-localhost\r\n250-STARTTLS\r\n250 AUTH PLAIN')
            elif command == 'STARTTLS':
                reply('220 Ready for TLS')
                self.connection = self.server.tls.wrap_socket(self.connection, server_side=True)
                self.rfile = self.connection.makefile('rb')
                self.wfile = self.connection.makefile('wb')
            elif command.startswith('AUTH'):
                self.server.auth_attempts += 1
                if self.server.reject == 'timeout':
                    time.sleep(5)
                    return
                reply('535 5.7.8 Authentication rejected' if self.server.reject else '235 2.7.0 Authenticated')
            elif command.startswith(('MAIL FROM', 'RCPT TO', 'RSET')):
                reply('250 OK')
            elif command == 'DATA':
                reply('354 End with dot')
                lines = []
                while (line := self.rfile.readline()) not in (b'.\r\n', b''):
                    lines.append(line)
                self.server.messages.put(b''.join(lines))
                reply('250 Queued')
            elif command == 'QUIT':
                if self.server.drop_quit:
                    return
                reply('221 Bye')
                return
            else:
                reply('250 OK')


class SMTPServer(socketserver.ThreadingTCPServer):
    def handle_error(self, request, client_address):
        # Peer disconnects are expected when deliberately testing invalid TLS.
        if not isinstance(sys.exc_info()[1], (ConnectionError, ssl.SSLError)):
            super().handle_error(request, client_address)


class Web:
    def __init__(self, base):
        self.base = base
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, data=None, json_body=False, method=None):
        headers = {}
        if data is not None:
            if json_body:
                data = json.dumps(data).encode()
                headers['Content-Type'] = 'application/json'
            else:
                data = urllib.parse.urlencode(data).encode()
        try:
            response = self.opener.open(urllib.request.Request(self.base + path, data, headers, method=method), timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        return response.status, html.unescape(response.read().decode())

    def form(self, path, data):
        status, body = self.request(path)
        assert status == 200, (path, status)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        assert token, 'Missing CSRF token'
        return self.request(path, dict(data, __RequestVerificationToken=html.unescape(token[1])))


def main(extra_checks=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--dll', required=True)
    parser.add_argument('--implicit-tls', action='store_true', help='Exercise SMTPS on port 465')
    parser.add_argument('--missing-config', action='store_true')
    parser.add_argument('--untrusted-tls', action='store_true')
    parser.add_argument('--postman', action='store_true', help='Run the existing Postman collection via Newman')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    database = 'LMS_Reset_Test_' + uuid.uuid4().hex
    def mongo(expression):
        return subprocess.check_output(['docker', 'exec', 'baomat-mongo', 'mongosh', '--quiet',
                                       '--eval', f'const d=db.getSiblingDB("{database}");' + expression], text=True).strip()
    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1',
                        '-keyout', str(folder/'key.pem'), '-out', str(folder/'cert.pem'),
                        '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost',
                        '-addext', 'basicConstraints=critical,CA:TRUE'], check=True, capture_output=True)
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(folder/'cert.pem', folder/'key.pem')
        with SMTPServer(('127.0.0.1', 465 if args.implicit_tls else 0), SMTP) as smtp:
            smtp.daemon_threads = True
            smtp.implicit_tls = args.implicit_tls
            smtp.drop_quit = False
            smtp.auth_attempts = 0
            smtp.tls, smtp.reject, smtp.messages = context, not args.untrusted_tls, queue.Queue()
            threading.Thread(target=smtp.serve_forever, daemon=True).start()
            with socket.socket() as probe:
                probe.bind(('127.0.0.1', 0))
                port = probe.getsockname()[1]
            environment = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development',
                               ASPNETCORE_URLS=f'http://127.0.0.1:{port}',
                               MongoDb__ConnectionString='mongodb://127.0.0.1:27017',
                               MongoDb__DatabaseName=database, DemoData__Enabled='false',
                               Email__Host='localhost', Email__Port=str(smtp.server_address[1]),
                               Email__Username='admin@lms.com', Email__SenderEmail='admin@lms.com',
                               Email__AppPassword='local-test-only', Email__TimeoutSeconds='3',
                               SSL_CERT_FILE=str(folder/'cert.pem'))
            if args.missing_config:
                environment['Email__Username'] = ''
                environment['Email__AppPassword'] = ''
            if args.untrusted_tls:
                environment['SSL_CERT_FILE'] = '/etc/ssl/certs/ca-certificates.crt'
            with open(folder/'app.log', 'w+') as log:
                app = subprocess.Popen([args.dotnet, args.dll], cwd=repo, env=environment,
                                       stdout=log, stderr=subprocess.STDOUT)
                try:
                    web = Web(f'http://127.0.0.1:{port}')
                    for _ in range(100):
                        if app.poll() is not None:
                            raise AssertionError('Application exited during startup')
                        try:
                            if web.request('/Auth/Login')[0] == 200:
                                break
                        except OSError:
                            time.sleep(.2)
                    else:
                        raise AssertionError('Application startup timed out')
                    checks = []
                    def passed(name):
                        checks.append(name)
                        print('PASS', name)
                    status, body = web.form('/Auth/ForgotPassword', {'Email': 'admin@lms.com'})
                    assert status == 503 and 'Hiện chưa thể gửi' in body, status
                    assert mongo('print(d.PasswordResetTokens.countDocuments({}))') == '0'
                    if args.missing_config:
                        assert web.form('/Auth/ForgotPassword', {'Email': 'unknown@example.test'})[0] == 503
                        passed('missing SMTP configuration fails consistently without generating OTP')
                        return
                    if args.untrusted_tls:
                        assert smtp.auth_attempts == 0 and smtp.messages.empty()
                        passed('untrusted SMTP TLS certificate rejected; no verification bypass')
                        return
                    passed('SMTP authentication failure is visible; unsent OTP removed')
                    smtp.reject = 'timeout'
                    assert web.form('/Auth/ForgotPassword', {'Email': 'admin@lms.com'})[0] == 503
                    assert mongo('print(d.PasswordResetTokens.countDocuments({}))') == '0'
                    passed('SMTP timeout returns an error and releases OTP reservation')
                    smtp.reject = False
                    smtp.drop_quit = True
                    status, body = web.form('/Auth/ForgotPassword', {'Email': 'admin@lms.com'})
                    assert status == 200 and 'name="Code"' in body, status
                    message = email.message_from_bytes(smtp.messages.get(timeout=5), policy=email.policy.default)
                    code = re.search(r'\b\d{6}\b', message.get_body(preferencelist=('plain',)).get_content())[0]
                    assert message['To'] == 'admin@lms.com'
                    assert mongo('print(d.PasswordResetTokens.findOne().DeliveryPending)') == 'false'
                    passed(('trusted SMTPS 465' if args.implicit_tls else 'trusted STARTTLS') + ' delivers six-digit OTP')
                    web.form('/Auth/ForgotPassword', {'Email': 'admin@lms.com'})
                    assert smtp.messages.empty()
                    passed('cooldown prevents duplicate email')
                    passed('QUIT disconnect failure does not invalidate an accepted email')
                    smtp.drop_quit = False
                    old_hash = mongo('print(d.PasswordResetTokens.findOne().CodeHash)')
                    mongo('d.PasswordResetTokens.updateOne({},{$set:{LastSentAt:new Date(Date.now()-61000)}});d.PasswordResetRequestLimits.updateOne({},{$set:{LastRequestedAt:new Date(Date.now()-61000)}})')
                    smtp.reject = True
                    assert web.form('/Auth/ForgotPassword', {'Email': 'admin@lms.com'})[0] == 503
                    assert mongo('print(d.PasswordResetTokens.findOne().CodeHash)') == old_hash
                    passed('failed resend preserves previous delivered OTP')
                    old_session = Web(web.base)
                    assert old_session.request('/api/auth/login', {'email': 'admin@lms.com', 'password': 'Admin@123'}, True)[0] == 200
                    status, invalid_body = web.form('/Auth/ResetPassword', {'Email': 'admin@lms.com',
                        'Code': '000000' if code != '000000' else '111111',
                        'NewPassword': 'Integration@123', 'ConfirmPassword': 'Integration@123'})
                    assert 'Mã OTP không hợp lệ' in invalid_body
                    assert mongo('print(d.PasswordResetTokens.findOne().FailedAttempts)') == '1'
                    passed('incorrect OTP is rejected and counted')
                    status, _ = web.form('/Auth/ResetPassword', {'Email': 'admin@lms.com', 'Code': code,
                        'NewPassword': 'Integration@123', 'ConfirmPassword': 'Integration@123'})
                    assert status == 200
                    assert mongo('print(d.PasswordResetTokens.countDocuments({}))') == '0'
                    status, body = web.request('/api/auth/login', {'email': 'admin@lms.com', 'password': 'Integration@123'}, True)
                    assert status == 200 and json.loads(body)['role'] == 'Admin', status
                    passed('delivered OTP resets password; new password authenticates')
                    assert old_session.request('/api/auth/me')[0] == 401
                    passed('password reset invalidates existing sessions')
                    status, body = web.form('/Auth/ResetPassword', {'Email': 'admin@lms.com', 'Code': code,
                        'NewPassword': 'Another@123', 'ConfirmPassword': 'Another@123'})
                    assert 'Mã OTP không hợp lệ' in body
                    passed('OTP cannot be reused')
                    paths = ['/Dashboard/Index', '/User/Index', '/Class/Index', '/Subject/Index', '/Department/Index', '/AcademicProgram/Index',
                             '/AcademicTerm/Index', '/Permission/Index', '/ActivityLog/Index', '/TeachingAssignment/TeacherSubjects',
                             '/Schedule/Index', '/Schedule/AvailabilityRequests', '/CourseRegistrationAdmin/Index',
                             '/api/auth/me', '/api/users', '/api/subjects', '/api/departments', '/api/classes',
                             '/api/activity-logs', '/api/schedule/sessions', '/api/schedule/availabilities']
                    for path in paths:
                        status, _ = web.request(path)
                        assert status == 200, (path, status)
                    passed(f'{len(paths)} Admin pages and API endpoints')
                    for role, password, pages in [
                        ('teacher', 'Teacher@123', ['/Dashboard/Index', '/Class/Index', '/Schedule/Index', '/Schedule/Availability']),
                        ('student', 'Student@123', ['/Dashboard/Index', '/Class/Index', '/Schedule/Index', '/CourseRegistration/Index',
                                                 '/StudentAcademic/Progress', '/StudentAcademic/Tuition'])]:
                        client = Web(web.base)
                        assert client.request('/api/auth/login', {'email':role+'@lms.com', 'password':password}, True)[0] == 200
                        for path in pages:
                            assert client.request(path)[0] == 200, (role, path)
                        assert client.request('/api/users')[0] == 403
                        passed(role + ' navigation and Admin API access denied')
                    def api(client, path, data=None, method=None, expected=200):
                        status, body = client.request(path, data, True, method)
                        assert status == expected, (method, path, status)
                        return json.loads(body) if body else None
                    users = api(web, '/api/users')
                    teacher_id = next(u['id'] for u in users if u['role'] == 'Teacher')
                    student_id = next(u['id'] for u in users if u['role'] == 'Student')
                    general = next(d for d in api(web, '/api/departments') if d['code'] == 'GEN')
                    subject = api(web, '/api/subjects', {'departmentId': general['id'], 'code': 'SMTPTEST',
                        'name': 'Regression subject', 'description': 'Integration test'}, expected=201)
                    subjects = api(web, '/api/teachers/'+teacher_id+'/subjects')
                    api(web, '/api/teachers/'+teacher_id+'/subjects',
                        {'subjectIds': [s['id'] for s in subjects]+[subject['id']]}, 'PUT', 204)
                    classroom = api(web, '/api/classes', {'name': 'Regression class', 'subjectId': subject['id'],
                        'teacherId': teacher_id, 'studentIds': [student_id]}, expected=201)
                    cid = classroom['id']
                    api(web, '/api/subjects/'+subject['id'], method='DELETE', expected=409)
                    api(web, '/api/classes/'+cid+'/lessons',
                        {'title': 'Test lesson', 'content': 'Test content', 'isPublished': True}, expected=201)
                    future = datetime.now(timezone.utc)+timedelta(days=2)
                    assignment = api(web, '/api/classes/'+cid+'/assignments',
                        {'title': 'Test assignment', 'description': 'Test content',
                         'dueDate': future.isoformat(), 'isPublished': True}, expected=201)
                    student_client = Web(web.base)
                    api(student_client, '/api/auth/login', {'email': 'student@lms.com', 'password':'Student@123'})
                    aid = assignment['id']
                    submission = api(student_client, '/api/assignments/'+aid+'/submission',
                        {'content': 'Integration submission'}, 'PUT')
                    teacher_client = Web(web.base)
                    api(teacher_client, '/api/auth/login', {'email':'teacher@lms.com', 'password':'Teacher@123'})
                    api(teacher_client, '/api/submissions/'+submission['id']+'/grade',
                        {'grade':8.5, 'teacherComment':'Reviewed'}, 'PUT')
                    assert api(student_client, '/api/assignments/'+aid+'/submission')['grade'] == 8.5
                    passed('subject/class/lesson/assignment creation, submission, grading and delete constraints')
                    start = datetime.now(timezone.utc)+timedelta(days=1)
                    session = api(web, '/api/schedule/sessions', {'classId':cid, 'subjectId':subject['id'],
                        'startAt':start.isoformat(), 'endAt':(start+timedelta(hours=1)).isoformat(),
                        'capacity':10}, expected=201)
                    api(student_client, '/api/schedule/sessions/'+session['id']+'/enrollment', {}, 'POST', 204)
                    assert api(student_client, '/api/schedule/sessions/'+session['id'])['isEnrolled']
                    passed('schedule creation and student enrollment')
                    if extra_checks is not None:
                        extra_checks(locals())
                    if args.postman:
                        result_path = str(folder/'newman.json')
                        result = subprocess.run(['npm', 'exec', '--yes', '--package=newman@6.2.3', '--',
                            'newman', 'run', str(repo/'postman/LMS.postman_collection.json'),
                            '--env-var', 'baseUrl='+web.base, '--env-var', 'adminPassword=Integration@123',
                            '--reporters', 'json', '--reporter-json-export', result_path],
                            capture_output=True, text=True)
                        report = json.loads(Path(result_path).read_text())['run']
                        print('Postman stats:', json.dumps(report['stats']))
                        for failure in report['failures']:
                            print('FAIL', failure.get('source', {}).get('name'), failure['error']['message'])
                        assert result.returncode == 0 and not report['failures'], 'Postman collection failed'
                        passed('existing Postman collection')
                    print(f'{len(checks)} regression groups passed')
                except Exception:
                    log.flush()
                    log.seek(0)
                    details = log.read()
                    print('Exception types:', sorted(set(re.findall(r'[A-Za-z.]+Exception', details))))
                    raise
                finally:
                    app.terminate()
                    try:
                        app.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        app.kill(); app.wait()
                    mongo('d.dropDatabase()')
                    smtp.shutdown()


if __name__ == '__main__':
    main()
