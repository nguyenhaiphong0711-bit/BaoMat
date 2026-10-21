export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}
interface Options extends Omit<RequestInit, "body"> {
  body?: unknown;
}
export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const { body, ...request } = options;
  const headers = new Headers(request.headers);
  if (body !== undefined) headers.set("Content-Type", "application/json");
  if (!["GET", "HEAD"].includes((request.method ?? "GET").toUpperCase())) {
    // Fetch per mutation so the token is bound to the current identity after login/logout.
    const token = await fetch("/api/frontend/csrf", {
      credentials: "same-origin",
      cache: "no-store",
    });
    if (!token.ok)
      throw new ApiError(
        token.status,
        "Không thể xác thực biểu mẫu. Vui lòng tải lại trang.",
      );
    const data: { requestToken: string } = await token.json();
    headers.set("RequestVerificationToken", data.requestToken);
  }
  const response = await fetch(path, {
    ...request,
    headers,
    credentials: "same-origin",
    cache: "no-store",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    if (response.status === 401 && path !== "/api/frontend/login")
      window.dispatchEvent(new Event("session-expired"));
    let message =
      response.status === 403
        ? "Bạn không có quyền thực hiện thao tác này."
        : "Không thể hoàn thành yêu cầu. Vui lòng thử lại.";
    try {
      const error: {
        title?: string;
        detail?: string;
        errors?: Record<string, string[]>;
      } = await response.json();
      message = error.errors
        ? Object.values(error.errors).flat().join(" ")
        : (error.detail ?? error.title ?? message);
    } catch {
      /* Preserve the safe fallback for non-JSON server/proxy errors. */
    }
    throw new ApiError(response.status, message);
  }
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}
export const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : "Có lỗi xảy ra. Vui lòng thử lại.";
