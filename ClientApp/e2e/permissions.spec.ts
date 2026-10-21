import { test, expect } from "@playwright/test";

test("Admin permission form selects prerequisites and clears dependent permissions without changing the database before Save", async ({
  page,
}) => {
  test.skip(
    !process.env.E2E_TEACHER_ID,
    "Requires an isolated permission fixture.",
  );
  await page.goto("/app/login");
  await page
    .getByLabel("Email tài khoản", { exact: true })
    .fill("admin@lms.com");
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.E2E_ADMIN_PASSWORD ?? "Admin@123");
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /một ngày học tập mới/ }),
  ).toBeVisible();
  await page.goto(`/Permission/Index?userId=${process.env.E2E_TEACHER_ID}`);
  const permission = (code: string) =>
    page.locator(`input[name="Assignment.PermissionCodes"][value="${code}"]`);
  await expect(permission("tuition.pay")).toHaveCount(0);
  await expect(permission("submissions.submit")).toHaveCount(0);
  await expect(permission("lessons.manage")).toBeChecked();
  await permission("lessons.view").uncheck();
  await expect(permission("lessons.manage")).not.toBeChecked();
  await permission("lessons.manage").check();
  await expect(permission("lessons.view")).toBeChecked();
  await expect(permission("classes.view")).toBeChecked();
  await permission("classes.view").uncheck();
  await expect(permission("lessons.manage")).not.toBeChecked();
  await expect(permission("lessons.view")).not.toBeChecked();
  await expect(permission("submissions.review")).not.toBeChecked();
  await permission("submissions.review").check();
  await expect(permission("assignments.view")).toBeChecked();
  await expect(permission("classes.view")).toBeChecked();
  await page.reload();
  await expect(permission("lessons.manage")).toBeChecked();
});
