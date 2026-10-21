"""Full backend security regression + actual Chromium + one-command Vite startup + published runtime.
Build and publish first; pass --dotnet, --dll and optionally --postman as in security_regression.py.
Set E2E_PUBLISH_DIR to the output of dotnet publish. CHROMIUM_PATH can select a local browser.
"""
import os
from pathlib import Path
import re
import socket
import subprocess
import time

from password_reset_smoke import Web, main
from security_regression import security_checks


def extra_checks(state):
    security_checks(state)
    repo, passed = state['repo'], state['passed']
    state['smtp'].reject = True
    environment = dict(os.environ, E2E_BASE_URL=state['web'].base, E2E_ADMIN_PASSWORD='Integration@123',
        E2E_CLASS_ID=state['cid'], E2E_ASSIGNMENT_ID=state['aid'], E2E_SMTP_FAILURE='true')
    subprocess.run(['npm','run','test:e2e'],cwd=repo/'ClientApp',env=environment,check=True)
    passed('Chromium: React authentication, CRUD, shared Razor session, permissions, CSRF, class content, mobile and OTP errors')
    state['smtp'].reject = False
    # Verify actual dotnet run launches and owns Vite without a second terminal.
    with socket.socket() as probe:
        probe.bind(('127.0.0.1',0)); backend_port=probe.getsockname()[1]
    with socket.socket() as probe:
        probe.bind(('127.0.0.1',0)); vite_port=probe.getsockname()[1]
    dev_environment = dict(state['environment'], Frontend__UseDevServer='true',
        Frontend__DevServerPort=str(vite_port), ASPNETCORE_URLS=f'http://127.0.0.1:{backend_port}')
    with open(state['folder']/'vite-start.log','w+') as log:
        app = subprocess.Popen([state['args'].dotnet,'run','--project',str(repo/'LMS.csproj'),
            '--no-build','--no-restore','--no-launch-profile','-c','Release',
            '--artifacts-path',str(Path(state['args'].dll).parents[3])],cwd=repo,env=dev_environment,stdout=log,stderr=subprocess.STDOUT)
        try:
            client = Web(f'http://127.0.0.1:{backend_port}')
            for _ in range(150):
                if app.poll() is not None:
                    log.flush();log.seek(0);raise AssertionError('dotnet run failed: '+log.read())
                try:
                    status,body=client.request('/app/')
                    if status==200 and '@vite/client' in body:break
                except OSError:time.sleep(.2)
            else:raise AssertionError('Vite startup timed out')
            assert client.request('/app/src/main.tsx')[0]==200
            assert client.request('/api/auth/me')[0]==401
            dev_browser_env = dict(environment,E2E_BASE_URL=client.base)
            subprocess.run(['npm','run','test:e2e','--','--grep','cookie session'],cwd=repo/'ClientApp',env=dev_browser_env,check=True)
            passed('one dotnet run starts ASP.NET + Vite and serves working React through the same origin')
        finally:
            app.terminate()
            try:app.wait(timeout=15)
            except subprocess.TimeoutExpired:app.kill();app.wait()
        # No Vite process may remain listening after the host stops.
        for _ in range(30):
            try:
                with socket.create_connection(('127.0.0.1',vite_port),timeout=.2):pass
                time.sleep(.1)
            except OSError:break
        else:raise AssertionError('Vite did not stop with dotnet run')
        passed('stopping dotnet run also stops its Vite child process')
    published = os.environ.get('E2E_PUBLISH_DIR')
    if not published:raise AssertionError('Set E2E_PUBLISH_DIR to verify the published package')
    with socket.socket() as probe:
        probe.bind(('127.0.0.1',0)); port=probe.getsockname()[1]
    production_env = dict(state['environment'], ASPNETCORE_ENVIRONMENT='Production',
        ASPNETCORE_URLS=f'http://127.0.0.1:{port}', PATH='/no-node-runtime')
    with open(state['folder']/'publish.log','w+') as log:
        app = subprocess.Popen([state['args'].dotnet,str(Path(published)/'LMS.dll')],
            cwd=published,env=production_env,stdout=log,stderr=subprocess.STDOUT)
        try:
            client = Web(f'http://127.0.0.1:{port}')
            for _ in range(100):
                if app.poll() is not None:
                    log.flush();log.seek(0);raise AssertionError('Published app failed: '+log.read())
                try:
                    status,body=client.request('/app/classes/deep-link')
                    if status==200 and 'id="root"' in body:break
                except OSError:time.sleep(.2)
            else:raise AssertionError('Published startup timed out')
            assets=re.findall(r'(?:src|href)="(/app/assets/[^"]+)"',body)
            assert len(assets)>=2
            for asset in assets:assert client.request(asset)[0]==200
            assert client.request('/app/nonexistent.js')[0]==404
            assert client.request('/api/auth/me')[0]==401
            passed('published ASP.NET serves React assets and deep links in Production without Node.js')
        finally:
            app.terminate();app.wait(timeout=10)


if __name__=='__main__':
    main(extra_checks)
