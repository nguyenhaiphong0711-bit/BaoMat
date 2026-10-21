import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, role: "teacher" | "student") {
  await page.goto("/app/login");
  await page
    .getByLabel("Email tài khoản", { exact: true })
    .fill(`${role}@lms.com`);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(role === "teacher" ? "Teacher@123" : "Student@123");
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /một ngày học tập mới/ }),
  ).toBeVisible();
}
async function write(page: Page, url: string, method: string, data: object) {
  const token = await (await page.request.get("/api/frontend/csrf")).json();
  return page.request.fetch(url, {
    method,
    data,
    headers: { RequestVerificationToken: token.requestToken },
  });
}

test("React learning lifecycle: draft, publish, files, submit, grade, resubmit and archive with server permissions", async ({
  browser,
}) => {
  test.skip(
    !process.env.E2E_CLASS_ID,
    "Requires an isolated integration database.",
  );
  const teacherContext = await browser.newContext();
  const studentContext = await browser.newContext();
  const teacher = await teacherContext.newPage();
  const student = await studentContext.newPage();
  const errors: string[] = [];
  teacher.on("pageerror", (e) => errors.push(e.message));
  student.on("pageerror", (e) => errors.push(e.message));
  try {
    await login(teacher, "teacher");
    const classPath = `/app/classes/${process.env.E2E_CLASS_ID}`;
    await teacher.goto(classPath);
    await teacher
      .getByRole("link", { name: "+ Thêm bài học", exact: true })
      .click();
    await teacher
      .getByLabel("Tiêu đề", { exact: true })
      .fill("React lifecycle lesson");
    await teacher
      .getByLabel("Nội dung bài học")
      .fill("Nội dung nháp <script>alert(1)</script>");
    const createdLesson = teacher.waitForResponse(
      (r) => r.request().method() === "POST" && r.url().endsWith("/lessons"),
    );
    await teacher
      .getByRole("button", { name: "Lưu bài học", exact: true })
      .click();
    const lesson = await (await createdLesson).json();
    await expect(
      teacher.getByText("React lifecycle lesson", { exact: false }),
    ).toBeVisible();
    await login(student, "student");
    await student.goto(classPath);
    await expect(
      student.getByText("React lifecycle lesson", { exact: false }),
    ).toHaveCount(0);
    expect(
      (await student.request.get(`/api/lessons/${lesson.id}`)).status(),
    ).toBe(403);
    await teacher.getByText("React lifecycle lesson", { exact: false }).click();
    await teacher
      .getByRole("link", { name: "Chỉnh sửa bài học", exact: true })
      .last()
      .click();
    await teacher.getByLabel("Công bố cho sinh viên").check();
    await teacher
      .getByRole("button", { name: "Lưu bài học", exact: true })
      .click();
    await expect(teacher).toHaveURL(new RegExp(`${classPath}$`));
    await student.reload();
    await student.getByText("React lifecycle lesson", { exact: false }).click();
    await expect(
      student.getByText("Nội dung nháp <script>alert(1)</script>", {
        exact: true,
      }),
    ).toBeVisible();

    await teacher
      .getByRole("link", { name: "+ Thêm bài tập", exact: true })
      .click();
    await teacher
      .getByLabel("Tiêu đề", { exact: true })
      .fill("React full workflow");
    await teacher.getByLabel("Yêu cầu bài tập").fill("Nộp tệp hoặc nội dung.");
    await teacher
      .getByLabel("Hạn nộp", { exact: true })
      .fill("2030-10-25T14:30");
    await teacher.getByLabel(/Thêm tệp đính kèm/).setInputFiles({
      name: "instructions.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("Assignment instructions"),
    });
    await teacher.getByLabel("Công bố cho sinh viên").check();
    const createdAssignment = teacher.waitForResponse(
      (r) =>
        r.request().method() === "POST" && r.url().endsWith("/assignments"),
    );
    await teacher
      .getByRole("button", { name: "Lưu bài tập", exact: true })
      .click();
    const assignment = await (await createdAssignment).json();
    await expect(
      teacher.getByRole("heading", { name: "React full workflow" }),
    ).toBeVisible();
    expect(assignment.attachments).toHaveLength(1);
    await teacher.goto(`/app/assignments/${assignment.id}/edit`);
    await expect(teacher.getByLabel("Hạn nộp", { exact: true })).toHaveValue(
      "2030-10-25T14:30",
    );
    await teacher
      .getByLabel("Yêu cầu bài tập")
      .fill("Nộp bài, xem điểm và nhận xét.");
    await teacher
      .getByRole("button", { name: "Lưu bài tập", exact: true })
      .click();
    await expect(teacher).toHaveURL(new RegExp(`${classPath}$`));

    await student.goto(`/app/assignments/${assignment.id}`);
    await expect(
      student.getByRole("link", { name: "instructions.txt" }),
    ).toBeVisible();
    const link = await student
      .getByRole("link", { name: "instructions.txt" })
      .getAttribute("href");
    expect((await student.request.get(link!)).status()).toBe(200);
    await student.getByLabel(/Thêm tệp đính kèm/).setInputFiles({
      name: "answer.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("Student answer"),
    });
    await student.getByRole("button", { name: "Nộp bài", exact: true }).click();
    await expect(student.getByRole("status")).toContainText("Đã nộp lúc");
    const submission = await (
      await student.request.get(`/api/assignments/${assignment.id}/submission`)
    ).json();
    expect(submission.attachments).toHaveLength(1);
    expect(
      (
        await student.request.get(
          `/api/assignments/${assignment.id}/submissions`,
        )
      ).status(),
    ).toBe(403);
    expect(
      (
        await write(student, `/api/submissions/${submission.id}/grade`, "PUT", {
          grade: 10,
        })
      ).status(),
    ).toBe(403);
    expect(
      (
        await write(
          student,
          `/api/assignments/${assignment.id}/submission`,
          "PUT",
          {
            content: "Forged file",
            uploadedFileIds: assignment.attachments[0].id,
          },
        )
      ).status(),
    ).toBe(400);
    expect(
      (
        await write(
          student,
          `/api/assignments/${assignment.id}/submission`,
          "PUT",
          { content: "", uploadedFileIds: "" },
        )
      ).status(),
    ).toBe(400);

    await teacher.goto(`/app/assignments/${assignment.id}`);
    await expect(
      teacher.getByRole("link", { name: "answer.txt" }),
    ).toBeVisible();
    await teacher.getByLabel("Điểm (0–10)").fill("8.5");
    await teacher.getByLabel("Nhận xét", { exact: true }).fill("Bài làm tốt");
    await teacher
      .getByRole("button", { name: "Lưu điểm", exact: true })
      .click();
    await expect(teacher.getByText("Điểm hiện tại: 8.5/10")).toBeVisible();
    await student.reload();
    await expect(student.getByRole("status")).toContainText("8.5");
    await expect(student.getByRole("status")).toContainText("Bài làm tốt");
    await student.getByLabel("Nội dung trả lời").fill("Bổ sung câu trả lời");
    await student
      .getByRole("button", { name: "Nộp lại bài", exact: true })
      .click();
    await expect(student.getByRole("status")).toContainText("Chưa chấm");
    await expect(student.getByRole("status")).not.toContainText("Bài làm tốt");
    expect(
      (
        await (
          await student.request.get(
            `/api/assignments/${assignment.id}/submission`,
          )
        ).json()
      ).attachments,
    ).toHaveLength(1);

    // Expired assignments remain readable, but the API and form both reject another submission.
    const dueDate = new Date(Date.now() - 60000).toISOString();
    expect(
      (
        await write(teacher, `/api/assignments/${assignment.id}`, "PUT", {
          title: assignment.title,
          description: "Expired",
          dueDate,
          isPublished: true,
        })
      ).status(),
    ).toBe(200);
    await student.reload();
    await expect(
      student.getByRole("button", { name: "Nộp lại bài", exact: true }),
    ).toBeDisabled();
    expect(
      (
        await write(
          student,
          `/api/assignments/${assignment.id}/submission`,
          "PUT",
          { content: "Late" },
        )
      ).status(),
    ).toBe(409);

    await teacher.goto(classPath);
    teacher.once("dialog", (d) => d.accept());
    await teacher
      .locator("article.assignment")
      .filter({
        has: teacher.getByRole("heading", { name: "React full workflow" }),
      })
      .getByRole("button", { name: "Lưu trữ", exact: true })
      .click();
    await expect(
      teacher.getByRole("heading", { name: "React full workflow" }),
    ).toHaveCount(0);
    expect(
      (await student.request.get(`/api/assignments/${assignment.id}`)).status(),
    ).toBe(403);
    expect(errors).toEqual([]);
  } finally {
    await teacherContext.close();
    await studentContext.close();
  }
});

test("leaving a page cancels its pending request and reopening starts with fresh data", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await login(page, "teacher");
  let first = true;
  let release!: () => void;
  const hold = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/subjects", async (route) => {
    if (first) {
      first = false;
      await hold;
      await route.abort().catch(() => {});
    } else {
      await route.continue();
    }
  });
  const pending = page.waitForRequest((r) => r.url().endsWith("/api/subjects"));
  await page.getByRole("link", { name: "Môn học", exact: true }).click();
  const request = await pending;
  const cancelled = page.waitForEvent("requestfailed", (r) => r === request);
  await page.getByRole("link", { name: "Lớp học", exact: true }).click();
  await cancelled;
  release();
  await page.getByRole("link", { name: "Môn học", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Môn học", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("table")).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
  expect(errors).toEqual([]);
});
