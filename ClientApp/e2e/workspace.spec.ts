import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /một ngày học tập mới/ }),
  ).toBeVisible();
}
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@lms.com";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Admin@123";

test("anonymous deep links redirect to login; wrong credentials show a useful error", async ({
  page,
}) => {
  await page.goto("/app/subjects");
  await expect(page).toHaveURL(/\/app\/login/);
  await page.getByLabel("Email tài khoản").fill("unknown@example.test");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("Wrong@123");
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "Email hoặc mật khẩu không đúng",
  );
});

test("Admin can create, edit, search and delete a subject; deep links survive reload", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await login(page, adminEmail, adminPassword);
  await page.getByRole("link", { name: "Môn học", exact: true }).click();
  await page.getByRole("button", { name: "+ Thêm môn học" }).click();
  const code = `R${Date.now().toString().slice(-10)}`;
  await page.getByLabel("Mã môn", { exact: true }).fill(code);
  await page
    .getByLabel("Tên môn", { exact: true })
    .fill("React integration subject");
  await page.getByRole("button", { name: "Lưu môn học", exact: true }).click();
  await expect(
    page.getByText("Đã lưu môn học.", { exact: true }),
  ).toBeVisible();
  await page.getByPlaceholder("Mã hoặc tên môn học…").fill(code);
  await expect(
    page.getByRole("row", { name: /React integration subject/ }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole("row", { name: /React integration subject/ }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Sửa React integration subject", exact: true })
    .click();
  await page
    .getByLabel("Tên môn", { exact: true })
    .fill("Updated React subject");
  await page.getByRole("button", { name: "Lưu môn học", exact: true }).click();
  await expect(
    page.getByRole("row", { name: /Updated React subject/ }),
  ).toBeVisible();
  page.once("dialog", (dialog) => dialog.accept());
  await page
    .getByRole("button", { name: "Xóa Updated React subject", exact: true })
    .click();
  await expect(
    page.getByText("Đã xóa môn học.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("row", { name: /Updated React subject/ }),
  ).toHaveCount(0);
  expect(errors).toEqual([]);
});

test("cookie session survives reload and React routes; logout revokes it", async ({
  page,
}) => {
  await login(page, adminEmail, adminPassword);
  await page.reload();
  await expect(
    page.getByRole("link", { name: "Phân quyền", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Phân quyền", exact: true }).click();
  await expect(page).toHaveURL(/\/app\/admin\/permissions/);
  await expect(page.getByRole("heading", { name: "Phân quyền" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { name: "Phân quyền" })).toBeVisible();
  const cookies = await page.context().cookies();
  expect(
    cookies.some(
      (cookie) => cookie.name.includes("Cookies") && cookie.httpOnly,
    ),
  ).toBeTruthy();
  expect(await page.evaluate(() => window.localStorage.length)).toBe(0);
  await page.getByRole("button", { name: "Đăng xuất ↗", exact: true }).click();
  await expect(page).toHaveURL(/\/app\/login/);
  await expect((await page.request.get("/api/auth/me")).status()).toBe(401);
});

test("Student UI hides Admin actions and the server rejects forged API writes", async ({
  page,
}) => {
  await login(page, "student@lms.com", "Student@123");
  await expect(
    page.getByRole("link", { name: "Phân quyền", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Môn học", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "+ Thêm môn học" }),
  ).toHaveCount(0);
  const status = await page.evaluate(async () => {
    const token = (await fetch("/api/frontend/csrf").then((r) => r.json())) as {
      requestToken: string;
    };
    const departments = (await fetch("/api/departments").then((r) =>
      r.json(),
    )) as { id: string }[];
    return (
      await fetch("/api/subjects", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token.requestToken,
        },
        body: JSON.stringify({
          departmentId: departments[0]?.id,
          code: "FORGED",
          name: "Forged subject",
          description: "",
        }),
      })
    ).status;
  });
  expect(status).toBe(403);
});

test("browser API writes require CSRF and new login cannot be called without it", async ({
  page,
}) => {
  await login(page, adminEmail, adminPassword);
  expect(
    (
      await page.request.post("/api/frontend/login", {
        data: { email: adminEmail, password: adminPassword },
      })
    ).status(),
  ).toBe(400);
  const status = await page.evaluate(
    async () => (await fetch("/api/auth/logout", { method: "POST" })).status,
  );
  expect(status).toBe(400);
  expect((await page.request.get("/api/auth/me")).status()).toBe(200);
});

test("class lessons and assignments load and invalid IDs cannot expose another class", async ({
  page,
}) => {
  test.skip(
    !process.env.E2E_CLASS_ID,
    "Requires an isolated seeded class from the integration runner.",
  );
  await login(page, "teacher@lms.com", "Teacher@123");
  await page.goto(`/app/classes/${process.env.E2E_CLASS_ID}`);
  await expect(
    page.getByRole("heading", { name: "Regression class", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Bài học", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("Test lesson", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Allowed edit", exact: true }),
  ).toBeVisible();
  await page.goto("/app/classes/000000000000000000000001");
  await expect(page.getByRole("alert")).toBeVisible();
});

test("mobile navigation is usable and the page has no horizontal overflow", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, adminEmail, adminPassword);
  await page.getByRole("button", { name: "☰ Menu" }).click();
  await page.getByRole("link", { name: "Môn học", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Môn học", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});

test("failed OTP delivery is visible and does not move to the reset form", async ({
  page,
}) => {
  test.skip(
    process.env.E2E_SMTP_FAILURE !== "true",
    "Requires the local rejecting SMTP integration server.",
  );
  await page.goto("/app/forgot-password");
  await page.getByLabel("Email tài khoản").fill("teacher@lms.com");
  await page.getByRole("button", { name: "Gửi mã OTP", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Hiện chưa thể gửi");
  await expect(page.getByLabel("Mã OTP 6 chữ số")).toHaveCount(0);
});

test("legacy FilePond upload and revert work with the shared CSRF configuration", async ({
  page,
}) => {
  test.skip(
    !process.env.E2E_ASSIGNMENT_ID,
    "Requires the isolated assignment fixture.",
  );
  await login(page, "student@lms.com", "Student@123");
  await page.goto(
    `/Submission/Submit?assignmentId=${process.env.E2E_ASSIGNMENT_ID}`,
  );
  await expect(page.locator(".filepond--root")).toBeVisible();
  const upload = page.waitForResponse(
    (r) =>
      r.url().includes("/Upload/SubmissionFile") &&
      r.request().method() === "POST",
  );
  await page
    .locator('input[type="file"]')
    .setInputFiles({
      name: "legacy-upload.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("legacy browser upload"),
    });
  const result = await upload;
  expect(result.status()).toBe(200);
  expect(await result.text()).toMatch(/^[a-f0-9]{24}$/);
  const reverted = page.waitForResponse(
    (r) =>
      r.url().includes("/Upload/Revert") && r.request().method() === "DELETE",
  );
  await page.locator(".filepond--action-revert-item-processing").click();
  expect((await reverted).status()).toBe(204);
});
