"""Exercise the existing security suite and React learning workflows against an isolated database.
Usage: python tests/learning_integration.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
CHROMIUM_PATH can select a locally installed browser.
"""
import os
import subprocess
from password_reset_smoke import main
from security_regression import security_checks


def learning_checks(state):
    security_checks(state)
    environment = dict(os.environ, E2E_BASE_URL=state['web'].base,
        E2E_ADMIN_PASSWORD='Integration@123', E2E_CLASS_ID=state['cid'],
        E2E_ASSIGNMENT_ID=state['aid'], E2E_SMTP_FAILURE='true')
    state['smtp'].reject = True
    subprocess.run(['npm', 'run', 'test:e2e'], cwd=state['repo']/'ClientApp', env=environment, check=True)
    state['passed']('Chromium: React learning workflow, attachments, grading, deadline and server permissions')


if __name__ == '__main__':
    main(learning_checks)
