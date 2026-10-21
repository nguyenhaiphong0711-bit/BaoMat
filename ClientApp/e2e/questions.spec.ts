import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("question bank React: teacher drafts, scoped Staff reviews and teacher publishes", async ({ browser }) => {
  test.skip(!process.env.E2E_QUESTION_SUBJECT_ID, "Requires the isolated question fixture.");
  const teacherContext = await browser.newContext();
  const staffContext = await browser.newContext();
  const teacher = await teacherContext.newPage();
  const staff = await staffContext.newPage();
  const errors: string[] = [];
  teacher.on("pageerror", error => errors.push(error.message));
  staff.on("pageerror", error => errors.push(error.message));
  try {
    await login(teacher, "teacher@lms.com", "Teacher@123");
    await teacher.getByRole("link", { name: "Ngân hàng câu hỏi", exact: true }).click();
    await teacher.getByRole("link", { name: "+ Soạn câu hỏi", exact: true }).click();
    await teacher.getByLabel("Chủ đề", { exact: true }).fill("React và bảo mật");
    await teacher.getByLabel("Nội dung câu hỏi", { exact: true }).fill("Dữ liệu nhạy cảm nên được xử lý ở đâu?");
    const choices = teacher.getByLabel(/Lựa chọn/);
    await choices.nth(0).fill("Ở máy chủ có kiểm soát quyền");
    await choices.nth(1).fill("Trong localStorage công khai");
    await choices.nth(2).fill("Trong URL");
    await choices.nth(3).fill("Trong mã nguồn giao diện");
    await teacher.getByLabel("Giải thích đáp án").fill("Máy chủ phải kiểm tra danh tính và quyền truy cập.");
    await teacher.getByRole("button", { name: "Lưu bản nháp", exact: true }).click();
    await expect(teacher.getByText("Nháp · Trung bình", { exact: true })).toBeVisible();
    const path = new URL(teacher.url()).pathname;
    await teacher.getByRole("button", { name: "Gửi duyệt", exact: true }).click();
    await expect(teacher.getByText("Chờ duyệt · Trung bình", { exact: true })).toBeVisible();

    await login(staff, process.env.E2E_STAFF_EMAIL!, process.env.E2E_STAFF_PASSWORD!);
    await staff.goto(path);
    await staff.getByLabel("Nhận xét kiểm duyệt").fill("Nội dung và đáp án phù hợp.");
    await staff.getByRole("button", { name: "Phê duyệt", exact: true }).click();
    await expect(staff.getByText("Đã duyệt · Trung bình", { exact: true })).toBeVisible();
    await teacher.reload();
    await teacher.getByRole("button", { name: "Công bố", exact: true }).click();
    await expect(teacher.getByText("Đã công bố · Trung bình", { exact: true })).toBeVisible();
    await expect(teacher.getByText("Ở máy chủ có kiểm soát quyền — Đáp án đúng", { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    await teacherContext.close();
    await staffContext.close();
  }
});
