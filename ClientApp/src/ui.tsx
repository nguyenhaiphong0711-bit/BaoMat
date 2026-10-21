export function Alert({ message }: { message: string | null | undefined }) {
  return message ? (
    <div className="alert" role="alert">
      {message}
    </div>
  ) : null;
}
export function Loading() {
  return (
    <div className="loading" role="status">
      Đang tải dữ liệu…
    </div>
  );
}
