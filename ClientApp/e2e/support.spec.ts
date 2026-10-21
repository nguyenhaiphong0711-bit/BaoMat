import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("learner support React: student opens a ticket and scoped Staff resolves the conversation", async ({
  browser,
}) => {
  test.skip(
    !process.env.E2E_STAFF_EMAIL,
    "Requires the isolated Staff fixture.",
  );
  const studentContext = await browser.newContext();
  const staffContext = await browser.newContext();
  const student = await studentContext.newPage();
  const staff = await staffContext.newPage();
  const errors: string[] = [];
  student.on("pageerror", (error) => errors.push(error.message));
  staff.on("pageerror", (error) => errors.push(error.message));
  try {
    await login(student, "student@lms.com", "Student@123");
    await student
      .getByRole("link", { name: "Hỗ trợ người học", exact: true })
      .click();
    await student
      .getByRole("link", { name: "+ Gửi yêu cầu", exact: true })
      .click();
    await student
      .getByLabel("Tiêu đề", { exact: true })
      .fill("Cần hỗ trợ từ React");
    await student
      .getByLabel("Nội dung", { exact: true })
      .fill("Em không mở được nội dung bài học.");
    await student
      .getByRole("button", { name: "Gửi yêu cầu", exact: true })
      .click();
    await expect(
      student.getByRole("heading", {
        name: "Cần hỗ trợ từ React",
        exact: true,
      }),
    ).toBeVisible();
    const ticketPath = new URL(student.url()).pathname;

    await login(
      staff,
      process.env.E2E_STAFF_EMAIL!,
      process.env.E2E_STAFF_PASSWORD!,
    );
    await staff.goto(ticketPath);
    await staff.getByLabel("Trả lời").fill("Cán bộ đã cấp lại quyền truy cập.");
    await staff
      .getByRole("button", { name: "Gửi trả lời", exact: true })
      .click();
    await expect(
      staff.getByText("Đang xử lý · Ưu tiên Bình thường", { exact: true }),
    ).toBeVisible();
    await staff.getByLabel("Trạng thái").selectOption("Resolved");
    await staff
      .getByRole("button", { name: "Cập nhật xử lý", exact: true })
      .click();
    await expect(
      staff.getByText("Đã giải quyết · Ưu tiên Bình thường", { exact: true }),
    ).toBeVisible();

    await student.reload();
    await expect(
      student.getByText("Cán bộ đã cấp lại quyền truy cập.", { exact: true }),
    ).toBeVisible();
    await student.getByLabel("Trả lời").fill("Em đã mở được, cảm ơn cán bộ.");
    await student
      .getByRole("button", { name: "Gửi trả lời", exact: true })
      .click();
    await expect(
      student.getByText("Mới · Ưu tiên Bình thường", { exact: true }),
    ).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    await studentContext.close();
    await staffContext.close();
  }
});
