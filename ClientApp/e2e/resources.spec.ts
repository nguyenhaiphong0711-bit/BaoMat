import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

async function login(page: Page, email: string, password: string) {
  await page.goto("/app/login");
  await page.getByLabel("Email tài khoản", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập →", exact: true }).click();
  await expect(page.getByRole("heading", { name: /Chào/ })).toBeVisible();
}

test("shared library React: two subjects, files, Staff review, publication and a new draft that preserves the published version", async ({
  browser,
}) => {
  test.skip(
    !process.env.E2E_RESOURCE_SUBJECT_ID,
    "Requires the isolated resource fixture.",
  );
  const teacherContext = await browser.newContext();
  const studentContext = await browser.newContext();
  const staffContext = await browser.newContext();
  const teacher = await teacherContext.newPage();
  const student = await studentContext.newPage();
  const staff = await staffContext.newPage();
  const errors: string[] = [];
  for (const page of [teacher, student, staff])
    page.on("pageerror", (e) => errors.push(e.message));
  try {
    await login(teacher, "teacher@lms.com", "Teacher@123");
    await teacher
      .getByRole("link", { name: "Kho học liệu", exact: true })
      .click();
    await teacher
      .getByRole("link", { name: "+ Thêm học liệu", exact: true })
      .click();
    await teacher
      .getByLabel("Tên học liệu", { exact: true })
      .fill("React shared resource");
    await teacher
      .getByLabel("Mô tả", { exact: true })
      .fill("Shared file in two subjects");
    await teacher
      .getByLabel("Nguồn tài liệu", { exact: true })
      .fill("Teacher course notes");
    await teacher
      .getByLabel("Giấy phép / quyền sử dụng", { exact: true })
      .fill("CC BY 4.0");
    await teacher
      .getByLabel("Điều kiện sử dụng", { exact: true })
      .fill("Ghi nguồn và tác giả.");
    await teacher.getByRole("checkbox", { name: /SMTPTEST/ }).check();
    await teacher.getByRole("checkbox", { name: /SHAREDSECOND/ }).check();
    await teacher.getByLabel(/Thêm tệp/).setInputFiles({
      name: "react-library.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("React shared learning file"),
    });
    await teacher
      .getByRole("button", { name: "Lưu bản nháp", exact: true })
      .click();
    await expect(
      teacher.getByRole("heading", {
        name: "React shared resource",
        exact: true,
      }),
    ).toBeVisible();
    const id = new URL(teacher.url()).pathname.split("/").at(-1)!;
    const resourcePath = `/app/resources/${id}`;
    await login(student, "student@lms.com", "Student@123");
    await student.goto(resourcePath);
    await expect(student.getByRole("alert")).toBeVisible();
    await teacher
      .getByRole("button", { name: "Gửi duyệt", exact: true })
      .click();
    await expect(
      teacher.getByText("Chờ duyệt · Phiên bản 1", { exact: true }),
    ).toBeVisible();
    await login(
      staff,
      process.env.E2E_STAFF_EMAIL!,
      process.env.E2E_STAFF_PASSWORD!,
    );
    await staff.goto(resourcePath);
    await staff
      .getByLabel("Nhận xét kiểm duyệt")
      .fill("Đã kiểm tra nguồn và giấy phép.");
    await staff.getByRole("button", { name: "Phê duyệt", exact: true }).click();
    await expect(
      staff.getByText("Đã duyệt · Phiên bản 1", { exact: true }),
    ).toBeVisible();
    await teacher.reload();
    await teacher.getByRole("button", { name: "Công bố", exact: true }).click();
    await expect(
      teacher.getByText("Đã công bố · Phiên bản 1", { exact: true }),
    ).toBeVisible();
    await student.reload();
    await expect(
      student.getByRole("heading", {
        name: "React shared resource",
        exact: true,
      }),
    ).toBeVisible();
    const download = await student
      .getByRole("link", { name: "react-library.txt", exact: true })
      .getAttribute("href");
    expect((await student.request.get(download!)).status()).toBe(200);
    await expect(
      student.getByRole("heading", { name: "Lịch sử phiên bản", exact: true }),
    ).toHaveCount(0);
    await teacher
      .getByRole("link", { name: "Chỉnh sửa / tạo phiên bản", exact: true })
      .click();
    await teacher
      .getByLabel("Tên học liệu", { exact: true })
      .fill("React shared revised");
    await teacher
      .getByRole("button", { name: "Lưu bản nháp", exact: true })
      .click();
    await expect(
      teacher.getByText("Nháp · Phiên bản 2", { exact: true }),
    ).toBeVisible();
    await student.reload();
    await expect(
      student.getByRole("heading", {
        name: "React shared resource",
        exact: true,
      }),
    ).toBeVisible();
    expect(
      (await student.request.get(`/api/resources/${id}/versions/2`)).status(),
    ).toBe(404);
    await expect(
      teacher.getByRole("link", {
        name: "Phiên bản 1: React shared resource",
        exact: true,
      }),
    ).toBeVisible();
    await teacher
      .getByRole("link", { name: "Chỉnh sửa / tạo phiên bản", exact: true })
      .click();
    await teacher
      .getByLabel(/Thêm tệp/)
      .setInputFiles({
        name: "network-extra.txt",
        mimeType: "text/plain",
        buffer: Buffer.from("Keep uncertain upload safe"),
      });
    let reverts = 0;
    teacher.on("request", (request) => {
      if (request.url().includes("/Upload/Revert")) reverts++;
    });
    await teacher.route(`**/api/resources/${id}`, async (route) => {
      if (route.request().method() === "PUT") await route.abort("failed");
      else await route.continue();
    });
    await teacher
      .getByRole("button", { name: "Lưu bản nháp", exact: true })
      .click();
    await expect(teacher.getByRole("alert")).toBeVisible();
    expect(reverts).toBe(0);
    await teacher.unroute(`**/api/resources/${id}`);
    await teacher
      .getByRole("link", { name: "← Quay lại", exact: true })
      .click();
    await expect(
      teacher.getByText("Nháp · Phiên bản 2", { exact: true }),
    ).toBeVisible();
    teacher.once("dialog", (d) => d.accept());
    await teacher
      .getByRole("button", { name: "Lưu trữ học liệu", exact: true })
      .click();
    await expect(
      teacher.getByText("Đã lưu trữ · Phiên bản 2", { exact: true }),
    ).toBeVisible();
    expect((await student.request.get(download!)).status()).toBe(404);
    expect(errors).toEqual([]);
  } finally {
    await teacherContext.close();
    await studentContext.close();
    await staffContext.close();
  }
});
