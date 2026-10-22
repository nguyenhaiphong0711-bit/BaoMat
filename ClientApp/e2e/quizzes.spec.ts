import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("quiz React: teacher assigns published questions and student receives server-graded result", async ({
  browser,
}) => {
  test.skip(
    !process.env.E2E_QUIZ_QUESTION_ID,
    "Requires the isolated quiz fixture.",
  );
  const teacherContext = await browser.newContext();
  const studentContext = await browser.newContext();
  const teacher = await teacherContext.newPage();
  const student = await studentContext.newPage();
  const errors: string[] = [];
  teacher.on("pageerror", (error) => errors.push(error.message));
  student.on("pageerror", (error) => errors.push(error.message));
  try {
    await login(teacher, "teacher@lms.com", "Teacher@123");
    await teacher
      .getByRole("link", { name: "Bài kiểm tra", exact: true })
      .click();
    await teacher
      .getByRole("link", { name: "+ Tạo bài kiểm tra", exact: true })
      .click();
    await teacher.getByLabel("Lớp học").selectOption({ label: "Regression class" });
    await teacher
      .getByLabel("Tiêu đề", { exact: true })
      .fill("React quiz workflow");
    await teacher
      .getByLabel("Hướng dẫn", { exact: true })
      .fill("Chọn đáp án đúng.");
    await teacher.getByRole("checkbox", { name: /Hai cộng hai/ }).check();
    await teacher
      .getByRole("button", { name: "Lưu bản nháp", exact: true })
      .click();
    await expect(
      teacher.getByRole("heading", {
        name: "React quiz workflow",
        exact: true,
      }),
    ).toBeVisible();
    const quizPath = new URL(teacher.url()).pathname;
    await teacher
      .getByRole("button", { name: "Công bố cho lớp", exact: true })
      .click();
    await expect(
      teacher.getByText("Đã công bố", { exact: true }),
    ).toBeVisible();

    await login(student, "student@lms.com", "Student@123");
    await student.goto(quizPath);
    await expect(
      student.getByText("Hai cộng hai bằng bao nhiêu?", { exact: true }),
    ).toHaveCount(0);
    await student
      .getByRole("button", { name: "Bắt đầu làm bài", exact: true })
      .click();
    await expect(
      student.getByText("Câu 1: Hai cộng hai bằng bao nhiêu?", { exact: true }),
    ).toBeVisible();
    await student.getByRole("radio", { name: "A. 4", exact: true }).check();
    await student.getByRole("button", { name: "Nộp bài", exact: true }).click();
    await expect(
      student.getByRole("link", { name: "Xem kết quả 10/10", exact: true }),
    ).toBeVisible();
    await student
      .getByRole("link", { name: "Xem kết quả 10/10", exact: true })
      .click();
    await expect(student.getByText(/Điểm:/)).toContainText("10/10");
    await expect(student.getByText(/Giải thích:.*2 \+ 2 = 4\./)).toBeVisible();
    await teacher.reload();
    await expect(teacher.getByText("10", { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    await teacherContext.close();
    await studentContext.close();
  }
});
