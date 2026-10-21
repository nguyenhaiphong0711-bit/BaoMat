import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("role-scoped reports render student progress and department metrics without client scope parameters", async ({
  browser,
}) => {
  test.skip(
    !process.env.E2E_STAFF_EMAIL,
    "Requires the isolated report fixture.",
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
      .getByRole("link", { name: "Báo cáo học tập", exact: true })
      .click();
    await expect(student.getByText("Phạm vi: Tiến độ của tôi.")).toBeVisible();
    await expect(
      student.getByText("Điểm kiểm tra trung bình", { exact: true }),
    ).toBeVisible();
    await expect(
      student.getByText("VISIBLE OWN EVENT", { exact: true }),
    ).toBeVisible();
    await expect(student.getByRole("link", { name: "Xuất CSV" })).toBeVisible();
    await student.getByLabel("Từ ngày").fill("2999-01-01");
    await expect(student.getByText("VISIBLE OWN EVENT", { exact: true })).toHaveCount(0);

    await login(
      staff,
      process.env.E2E_STAFF_EMAIL!,
      process.env.E2E_STAFF_PASSWORD!,
    );
    await staff
      .getByRole("link", { name: "Báo cáo học tập", exact: true })
      .click();
    await expect(staff.getByText(/Phạm vi: Khoa /)).toBeVisible();
    await expect(
      staff.getByRole("main").getByText("Sinh viên trong khoa", { exact: true }),
    ).toBeVisible();
    await expect(
      staff.getByRole("heading", { name: "Yêu cầu hỗ trợ", exact: true }),
    ).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    await studentContext.close();
    await staffContext.close();
  }
});
