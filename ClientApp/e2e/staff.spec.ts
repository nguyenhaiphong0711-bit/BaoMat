import { test, expect } from "@playwright/test";

test("Staff React shows only assigned department students and read-only progress; Admin can configure Staff scope", async ({
  page,
  browser,
}) => {
  test.skip(
    !process.env.E2E_STAFF_EMAIL,
    "Requires the isolated Staff fixture.",
  );
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/app/login");
  await page
    .getByLabel("Email tài khoản", { exact: true })
    .fill(process.env.E2E_STAFF_EMAIL!);
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.E2E_STAFF_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /Chào Staff integration/ }),
  ).toBeVisible();
  await expect(page.getByText("Cán bộ quản lý", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Phân quyền", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: "Lớp học", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("link", { name: "Sinh viên trong khoa", exact: true })
    .click();
  await expect(
    page.getByRole("row", { name: /student@lms.com/ }),
  ).toBeVisible();
  await expect(
    page.getByText("outside-scope@example.test", { exact: true }),
  ).toHaveCount(0);
  await page.getByLabel("Tìm theo tên hoặc email").fill("student@lms.com");
  await expect(
    page.getByRole("row", { name: /student@lms.com/ }),
  ).toBeVisible();
  await page
    .getByRole("row", { name: /student@lms.com/ })
    .getByRole("link", { name: "Xem tiến trình" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Nguyễn Hải Phong", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("cell", { name: "Regression class", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Hidden cross-department class", { exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Lưu điểm", exact: true }),
  ).toHaveCount(0);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Nguyễn Hải Phong", exact: true }),
  ).toBeVisible();
  await page.goto(`/app/staff/students/${process.env.E2E_STAFF_OUTSIDER_ID}`);
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Outside staff scope" }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Đăng xuất ↗", exact: true }).click();
  await page
    .getByLabel("Email tài khoản", { exact: true })
    .fill("admin@lms.com");
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.E2E_ADMIN_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /một ngày học tập mới/ }),
  ).toBeVisible();
  await page.goto(`/Permission/Index?userId=${process.env.E2E_STAFF_ID}`);
  await expect(
    page.getByLabel("Khoa được giao quản lý", { exact: true }),
  ).toBeVisible();
  await expect(
    page.locator('input[value="staff.students.view"]'),
  ).toBeChecked();
  await expect(
    page.locator('input[value="staff.progress.view"]'),
  ).toBeChecked();
  await expect(page.locator('input[value="lessons.manage"]')).toHaveCount(0);
  const staffSession = await browser.newContext();
  try {
    const csrf = await (
      await staffSession.request.get("/api/frontend/csrf")
    ).json();
    const login = await staffSession.request.post("/api/frontend/login", {
      data: {
        email: process.env.E2E_STAFF_EMAIL,
        password: process.env.E2E_STAFF_PASSWORD,
      },
      headers: { RequestVerificationToken: csrf.requestToken },
    });
    expect(login.status()).toBe(200);
    const scope = page.getByLabel("Khoa được giao quản lý", { exact: true });
    const department = await scope.inputValue();
    await scope.selectOption("");
    await page
      .getByRole("button", { name: "Lưu quyền và hồ sơ", exact: true })
      .click();
    await expect(
      page.getByLabel("Khoa được giao quản lý", { exact: true }),
    ).toHaveValue("");
    expect(
      (await staffSession.request.get("/api/staff/students")).status(),
    ).toBe(403);
    await page
      .getByLabel("Khoa được giao quản lý", { exact: true })
      .selectOption(department);
    await page
      .getByRole("button", { name: "Lưu quyền và hồ sơ", exact: true })
      .click();
    await expect(
      page.getByLabel("Khoa được giao quản lý", { exact: true }),
    ).toHaveValue(department);
    expect(
      (await staffSession.request.get("/api/staff/students")).status(),
    ).toBe(200);
  } finally {
    await staffSession.close();
  }
  expect(errors).toEqual([]);
});
