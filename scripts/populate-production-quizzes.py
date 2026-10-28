"""Populate a few linked demonstration quizzes through the production API.

The script never connects to MongoDB directly. It creates a temporary reviewer,
uses the same question review workflow as the UI, publishes the questions, then
creates and publishes quizzes. The temporary reviewer is locked at the end.

Usage:
  python scripts/populate-production-quizzes.py --apply
"""
import argparse
import datetime as dt
import getpass
import http.cookiejar
import json
import secrets
import string
import urllib.error
import urllib.request


class Api:
    def __init__(self, base):
        self.base = base.rstrip("/")
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, data=None, method=None, csrf=False, expected=(200,)):
        headers = {"Accept": "application/json"}
        if csrf:
            token = self.request("/api/frontend/csrf")["requestToken"]
            headers["RequestVerificationToken"] = token
        raw = None
        if data is not None:
            raw = json.dumps(data).encode()
            headers["Content-Type"] = "application/json"
        try:
            response = self.opener.open(urllib.request.Request(
                self.base + path, raw, headers, method=method), timeout=90)
        except urllib.error.HTTPError as error:
            response = error
        body = response.read()
        try:
            result = json.loads(body) if body else None
        except json.JSONDecodeError:
            result = body.decode(errors="replace")[:500]
        if response.status not in expected:
            raise RuntimeError(f"{method or 'GET'} {path}: HTTP {response.status}: {result}")
        return result

    def login(self, email, password):
        return self.request("/api/auth/login", {"email": email, "password": password}, "POST")


def strong_password():
    alphabet = string.ascii_letters + string.digits + "!@#$%"
    return "Tmp!" + "".join(secrets.choice(alphabet) for _ in range(20)) + "9aA"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default="https://baomatt.onrender.com")
    parser.add_argument("--admin-email", default="admin@lms.com")
    parser.add_argument("--count", type=int, default=3, choices=range(1, 6))
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if not args.apply:
        raise SystemExit("Chưa thay đổi production. Chạy lại với --apply sau khi kiểm tra đúng URL.")

    password = getpass.getpass(f"Mật khẩu {args.admin_email}: ")
    author = Api(args.base_url)
    identity = author.login(args.admin_email, password)
    if identity.get("role") != "Admin":
        raise SystemExit("Tài khoản phải có vai trò Admin.")

    classes = author.request("/api/classes")
    subjects = {item["id"]: item for item in author.request("/api/subjects")}
    quizzes = author.request("/api/quizzes")
    existing_classes = {item["classId"] for item in quizzes if not item.get("isArchived")}
    targets = [item for item in classes
               if item["subjectId"] in subjects and item["id"] not in existing_classes][:args.count]
    if not targets:
        print("Không có lớp trống quiz phù hợp; production đã có quiz hoặc chưa có lớp/môn liên kết.")
        return

    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d%H%M%S")
    reviewer_email = f"quiz-reviewer-{stamp}@example.test"
    reviewer_password = strong_password()
    reviewer_user = author.request("/api/users", {
        "fullName": "Cán bộ kiểm duyệt quiz tạm thời",
        "email": reviewer_email,
        "password": reviewer_password,
        "role": "Admin"
    }, "POST", expected=(201,))
    reviewer = Api(args.base_url)
    created = []
    try:
        reviewer.login(reviewer_email, reviewer_password)
        now = dt.datetime.now(dt.timezone.utc)
        for index, classroom in enumerate(targets, 1):
            subject = subjects[classroom["subjectId"]]
            form = {
                "revision": 0,
                "subjectId": subject["id"],
                "topic": "Kiến thức nền tảng",
                "difficulty": "Medium",
                "content": f"Nội dung nào phản ánh đúng mục tiêu cốt lõi của học phần {subject['name']}?",
                "options": [
                    "Vận dụng kiến thức để phân tích tình huống thực tế",
                    "Chỉ ghi nhớ thuật ngữ mà không cần hiểu",
                    "Bỏ qua dữ liệu và bằng chứng liên quan",
                    "Dùng chung một kết luận cho mọi tình huống"
                ],
                "correctOption": 0,
                "explanation": "Học phần hướng tới hiểu và vận dụng kiến thức trong bối cảnh thực tế."
            }
            question = author.request("/api/questions", form, "POST", csrf=True, expected=(201,))
            question = author.request(f"/api/questions/{question['id']}/submit",
                                      {"revision": question["revision"], "approve": False, "comment": ""},
                                      "POST", csrf=True)
            question = reviewer.request(f"/api/questions/{question['id']}/review",
                                        {"revision": question["revision"], "approve": True,
                                         "comment": "Đã kiểm tra nội dung, đáp án và phạm vi môn học."},
                                        "POST", csrf=True)
            question = author.request(f"/api/questions/{question['id']}/publish",
                                      {"revision": question["revision"], "approve": False, "comment": ""},
                                      "POST", csrf=True)
            quiz = author.request("/api/quizzes", {
                "revision": 0,
                "classId": classroom["id"],
                "title": f"Ôn tập {subject['name']} · {index:02d}",
                "instructions": "Chọn phương án phù hợp nhất. Kết quả được chấm tự động sau khi nộp.",
                "durationMinutes": 15,
                "opensAt": (now - dt.timedelta(hours=1)).isoformat(),
                "closesAt": (now + dt.timedelta(days=30)).isoformat(),
                "questionIds": [question["id"]]
            }, "POST", csrf=True, expected=(201,))
            quiz = author.request(f"/api/quizzes/{quiz['quiz']['id']}/publish",
                                  {"revision": quiz["quiz"]["revision"]}, "POST", csrf=True)
            created.append((classroom["name"], quiz["quiz"]["title"]))
            print(f"Đã tạo: {quiz['quiz']['title']} → {classroom['name']}")
    finally:
        author.request(f"/api/users/{reviewer_user['id']}/toggle-active", {}, "POST")
        reviewer_password = ""
        password = ""

    print(f"Hoàn tất {len(created)} quiz. Tài khoản reviewer tạm đã bị khóa.")


if __name__ == "__main__":
    main()
