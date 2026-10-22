import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("teacher class-opening workflow and personal notifications render from scoped APIs", async ({ browser }) => {
  test.skip(!process.env.E2E_OPENING_NAME, "Requires the isolated workflow fixture.");
  const teacherContext = await browser.newContext();
  const adminContext = await browser.newContext();
  const teacher = await teacherContext.newPage();
  const admin = await adminContext.newPage();
  try {
    await login(teacher, "teacher@lms.com", "Teacher@123");
    await teacher.getByRole("link", { name: "Đề xuất mở lớp", exact: true }).click();
    await expect(teacher.getByRole("heading", { name: "Đề xuất mở lớp", exact: true })).toBeVisible();
    await expect(teacher.getByText(process.env.E2E_OPENING_NAME!, { exact: true })).toBeVisible();
    expect(await teacher.getByLabel("Môn học").locator("option").count()).toBeGreaterThan(1);
    await teacher.getByRole("link", { name: "Thông báo", exact: true }).click();
    await expect(teacher.getByRole("heading", { name: "Thông báo", exact: true })).toBeVisible();
    await expect(teacher.getByText(/Đề xuất mở lớp/).first()).toBeVisible();

    await login(admin, "admin@lms.com", "Integration@123");
    await admin.getByRole("link", { name: "Đề xuất mở lớp", exact: true }).click();
    await expect(admin.getByText("Lớp kinh tế số được duyệt", { exact: true })).toBeVisible();
    await expect(admin.getByRole("article").filter({ hasText: "Lớp kinh tế số được duyệt" })
      .getByText("Giảng viên: Trần Minh Quân", { exact: true })).toBeVisible();
  } finally {
    await teacherContext.close(); await adminContext.close();
  }
});
