import { useState } from "react";
import type { FormEvent } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { api, ApiError } from "./api";
import { can, useAuth } from "./auth";
import { useAction, useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { Attachment, ResourceItem, ResourcePage, Subject } from "./types";

type ResourceSubject = Pick<Subject, "id" | "code" | "name">;

const states: Record<string, string> = {
  Draft: "Nháp",
  PendingReview: "Chờ duyệt",
  Approved: "Đã duyệt",
  Rejected: "Cần chỉnh sửa",
  Published: "Đã công bố",
  Archived: "Đã lưu trữ",
};
function ResourceFiles({ resource }: { resource: ResourceItem }) {
  return (
    <ul>
      {resource.files.map((file) => (
        <li key={file.id}>
          <a
            href={`/api/resources/${resource.id}/versions/${resource.version}/files/${file.id}`}
          >
            {file.fileName}
          </a>
          <small> ({Math.ceil(file.length / 1024)} KB)</small>
        </li>
      ))}
    </ul>
  );
}
export function Resources() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const query = params.get("q") ?? "";
  const category = params.get("category") ?? "";
  const state = params.get("state") ?? "";
  const license = params.get("license") ?? "";
  const access = params.get("access") ?? "";
  const page = Math.min(
    100000,
    Math.max(1, Math.floor(Number(params.get("page")) || 1)),
  );
  const allowed = !!user && can(user, "resources.view");
  const result = useRemote<ResourcePage>(
    allowed
      ? `/api/resources?q=${encodeURIComponent(query)}&category=${encodeURIComponent(category)}&license=${encodeURIComponent(license)}&access=${encodeURIComponent(access)}&state=${encodeURIComponent(state)}&page=${page}`
      : null,
  );
  const filter = (name: string, value: string) => {
    const next = new URLSearchParams(params);
    next.delete("page");
    value ? next.set(name, value) : next.delete(name);
    setParams(next, { replace: true });
  };
  if (!allowed)
    return (
      <Alert message="Bạn chưa được cấp quyền xem kho học liệu. Vui lòng liên hệ Admin." />
    );
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">TÀI NGUYÊN DÙNG CHUNG</span>
          <h1>Kho học liệu</h1>
          <p className="muted">
            Tài liệu theo môn học, có nguồn và điều kiện sử dụng.
          </p>
        </div>
        {user && can(user, "resources.manage") && (
          <Link className="button" to="/resources/new">
            + Thêm học liệu
          </Link>
        )}
      </div>
      <section className="panel">
        <div className="filters">
          <label htmlFor="resource-search">
            Tên, tác giả hoặc mô tả
            <input
              id="resource-search"
              maxLength={120}
              value={query}
              onChange={(e) => filter("q", e.target.value)}
            />
          </label>
          <label htmlFor="resource-category-filter">
            Loại học liệu
            <input
              id="resource-category-filter"
              maxLength={80}
              value={category}
              onChange={(e) => filter("category", e.target.value)}
              placeholder="Bài giảng, tài liệu tham khảo…"
            />
          </label>
          <label htmlFor="resource-state-filter">
            Trạng thái
            <select
              id="resource-state-filter"
              value={state}
              onChange={(e) => filter("state", e.target.value)}
            >
              <option value="">Tất cả được phép xem</option>
              {Object.entries(states).map(([key, name]) => (
                <option key={key} value={key}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Giấy phép
            <input maxLength={160} value={license} onChange={(e) => filter("license", e.target.value)} placeholder="CC BY…" />
          </label>
          <label>
            Phạm vi truy cập
            <select value={access} onChange={(e) => filter("access", e.target.value)}>
              <option value="">Tất cả</option>
              <option value="Subjects">Theo môn</option>
              <option value="School">Trong trường</option>
            </select>
          </label>
        </div>
        <Alert message={result.error} />
        {result.error && <button onClick={result.reload}>Thử lại</button>}
        {result.loading ? (
          <Loading />
        ) : (
          result.data && (
            <>
              {result.data.items.length ? (
                result.data.items.map((r) => (
                  <article className="assignment" key={r.id}>
                    <span className="badge">
                      {states[r.isArchived ? "Archived" : r.state]} · Phiên bản{" "}
                      {r.version}
                    </span>
                    <h2>
                      <Link to={`/resources/${r.id}`}>{r.title}</Link>
                    </h2>
                    <p>{r.description}</p>
                    <small>
                      {r.category} · {r.authorName} ·{" "}
                      {r.subjectNames.join(", ")}
                    </small>
                  </article>
                ))
              ) : (
                <p className="empty">
                  Chưa có học liệu phù hợp với quyền truy cập và bộ lọc.
                </p>
              )}
              <div className="pagination">
                <span>
                  {result.data.total} tài liệu · Trang {page}
                </span>
                <div className="actions">
                  <button
                    className="button secondary"
                    disabled={page <= 1}
                    onClick={() => {
                      const next = new URLSearchParams(params);
                      next.set("page", String(page - 1));
                      setParams(next);
                    }}
                  >
                    Trước
                  </button>
                  <button
                    className="button secondary"
                    disabled={page * 12 >= result.data.total}
                    onClick={() => {
                      const next = new URLSearchParams(params);
                      next.set("page", String(page + 1));
                      setParams(next);
                    }}
                  >
                    Sau
                  </button>
                </div>
              </div>
            </>
          )
        )}
      </section>
    </>
  );
}
export function ResourceDetail() {
  const { user } = useAuth();
  const { resourceId = "", version } = useParams();
  const result = useRemote<ResourceItem>(
    user && can(user, "resources.view")
      ? `/api/resources/${resourceId}${version ? `/versions/${version}` : ""}`
      : null,
  );
  const action = useAction();
  const [comment, setComment] = useState("");
  if (!user || !can(user, "resources.view"))
    return <Alert message="Bạn chưa được cấp quyền xem kho học liệu." />;
  if (result.loading) return <Loading />;
  if (result.error) return <Alert message={result.error} />;
  if (!result.data) return null;
  const r = result.data;
  const change = (name: string, approve = false) => {
    if (
      name === "archive" &&
      !window.confirm("Lưu trữ tài liệu? Người học sẽ không còn xem/tải được.")
    )
      return;
    void action.run(
      (signal) =>
        api<ResourceItem>(`/api/resources/${r.id}/${name}`, {
          method: "POST",
          signal,
          body: { revision: r.revision, approve, comment },
        }),
      () => {
        setComment("");
        result.reload();
      },
    );
  };
  return (
    <>
      <Link className="back-link" to="/resources">
        ← Kho học liệu
      </Link>
      <section className="panel editor">
        <span className="badge">
          {states[r.isArchived ? "Archived" : r.state]} · Phiên bản {r.version}
        </span>
        <h1>{r.title}</h1>
        <p className="plain-content">{r.description}</p>
        <p>
          <strong>Loại:</strong> {r.category} · <strong>Tác giả:</strong>{" "}
          {r.authorName}
        </p>
        <p>
          <strong>Môn học:</strong> {r.subjectNames.join(", ")}
        </p>
        <p>
          <strong>Nguồn:</strong> {r.source}
        </p>
        <p>
          <strong>Quyền sử dụng / giấy phép:</strong> {r.license}
        </p>
        <p className="plain-content">
          <strong>Điều kiện sử dụng:</strong> {r.usageTerms}
        </p>
        <p>
          <strong>Quyền truy cập:</strong>{" "}
          {r.access === "School"
            ? "Các tài khoản trong trường có quyền xem kho"
            : "Theo môn học được tham gia/phân công"}
        </p>
        <ResourceFiles resource={r} />
        {r.externalUrl && (
          <a href={r.externalUrl} target="_blank" rel="noopener noreferrer">
            Mở đường dẫn học liệu ↗
          </a>
        )}
        {r.reviewComment && (
          <div className="notice">Nhận xét kiểm duyệt: {r.reviewComment}</div>
        )}
        <Alert message={action.error} />
        <div className="actions">
          {r.canEdit && (
            <Link className="button secondary" to={`/resources/${r.id}/edit`}>
              Chỉnh sửa / tạo phiên bản
            </Link>
          )}
          {r.canSubmit && (
            <button disabled={action.busy} onClick={() => change("submit")}>
              Gửi duyệt
            </button>
          )}
          {r.canPublish && (
            <button disabled={action.busy} onClick={() => change("publish")}>
              Công bố
            </button>
          )}
          {r.canArchive && (
            <button
              className="button text danger"
              disabled={action.busy}
              onClick={() => change("archive")}
            >
              Lưu trữ học liệu
            </button>
          )}
        </div>
        {r.canReview && (
          <div className="editor">
            <label htmlFor="resource-review-comment">Nhận xét kiểm duyệt</label>
            <textarea
              id="resource-review-comment"
              maxLength={2000}
              rows={3}
              value={comment}
              disabled={action.busy}
              onChange={(e) => setComment(e.target.value)}
            />
            <div className="actions">
              <button
                disabled={action.busy}
                onClick={() => change("review", true)}
              >
                Phê duyệt
              </button>
              <button
                className="button secondary"
                disabled={action.busy}
                onClick={() => change("review", false)}
              >
                Yêu cầu chỉnh sửa
              </button>
            </div>
          </div>
        )}
        {r.history.length > 0 && (
          <>
            <h2>Lịch sử phiên bản</h2>
            <ul>
              {r.history.map((v) => (
                <li key={v.number}>
                  <Link to={`/resources/${r.id}/versions/${v.number}`}>
                    Phiên bản {v.number}: {v.title}
                  </Link>{" "}
                  · {states[v.state]}
                  {v.reviewerName && (
                    <small>
                      {" "}
                      · Duyệt bởi {v.reviewerName} lúc{" "}
                      {v.reviewedAt
                        ? new Date(v.reviewedAt).toLocaleString("vi-VN")
                        : "—"}
                      : {v.reviewComment}
                    </small>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
      </section>
    </>
  );
}

export function ResourceEditor() {
  const { resourceId } = useParams();
  const { user } = useAuth();
  const allowed =
    !!user &&
    can(user, "resources.manage") &&
    (user.role === "Admin" || user.role === "Teacher");
  const existing = useRemote<ResourceItem>(
    allowed && resourceId ? `/api/resources/${resourceId}` : null,
  );
  const subjects = useRemote<ResourceSubject[]>(
    allowed ? "/api/resources/subjects" : null,
  );
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền soạn học liệu." />;
  if (existing.loading || subjects.loading) return <Loading />;
  if (existing.error || subjects.error)
    return <Alert message={existing.error ?? subjects.error} />;
  if (resourceId && !existing.data?.canEdit)
    return (
      <Alert message="Không thể sửa phiên bản này. Tài liệu chờ duyệt cần được xử lý trước khi chỉnh sửa." />
    );
  return (
    <ResourceForm
      key={resourceId ?? "new"}
      resource={existing.data}
      subjects={subjects.data ?? []}
    />
  );
}
function ResourceForm({
  resource,
  subjects,
}: {
  resource: ResourceItem | null;
  subjects: ResourceSubject[];
}) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const action = useAction();
  const [form, setForm] = useState({
    title: resource?.title ?? "",
    description: resource?.description ?? "",
    category: resource?.category ?? "Bài giảng",
    authorName: resource?.authorName ?? user?.fullName ?? "",
    source: resource?.source ?? "",
    license: resource?.license ?? "",
    usageTerms: resource?.usageTerms ?? "",
    access: resource?.access ?? "Subjects",
    externalUrl: resource?.externalUrl ?? "",
    subjectIds:
      resource?.subjectIds.filter((id) => subjects.some((s) => s.id === id)) ??
      ([] as string[]),
  });
  const [retained, setRetained] = useState(resource?.files ?? []);
  const [selected, setSelected] = useState<File[]>([]);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(
      async (signal) => {
        const uploaded: Attachment[] = [];
        try {
          for (const file of selected) {
            const data = new FormData();
            data.append("file", file);
            uploaded.push(
              await api<Attachment>("/api/resources/files", {
                method: "POST",
                body: data,
                signal,
              }),
            );
          }
          return await api<ResourceItem>(
            resource ? `/api/resources/${resource.id}` : "/api/resources",
            {
              method: resource ? "PUT" : "POST",
              signal,
              body: {
                ...form,
                revision: resource?.revision ?? 0,
                fileIds: Array.from(
                  new Set([
                    ...retained.map((f) => f.id),
                    ...uploaded.map((f) => f.id),
                  ]),
                ),
              },
            },
          );
        } catch (error) {
          // A lost/aborted response does not prove the server rejected the save.
          // Keep files in that case so a late commit cannot reference a deleted upload.
          if (
            error instanceof ApiError &&
            error.status >= 400 &&
            error.status < 500
          )
            await Promise.allSettled(
              uploaded.map((file) =>
                api<void>("/Upload/Revert", {
                  method: "DELETE",
                  body: file.id,
                }),
              ),
            );
          throw error;
        }
      },
      (saved) => navigate(`/resources/${saved.id}`),
    );
  };
  return (
    <>
      <Link
        className="back-link"
        to={resource ? `/resources/${resource.id}` : "/resources"}
      >
        ← Quay lại
      </Link>
      <section className="panel editor">
        <h1>{resource ? "Tạo phiên bản học liệu mới" : "Thêm học liệu"}</h1>
        <p className="muted">
          Lưu ở trạng thái nháp. Bản đã công bố được giữ nguyên đến khi bản mới
          được duyệt và công bố.
        </p>
        <Alert message={action.error} />
        {!subjects.length && (
          <Alert message="Bạn chưa có môn được phân công. Liên hệ Admin trước khi thêm học liệu." />
        )}
        <form onSubmit={submit}>
          <fieldset disabled={action.busy || !subjects.length}>
            <label htmlFor="resource-title">Tên học liệu</label>
            <input
              id="resource-title"
              required
              maxLength={200}
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
            <label htmlFor="resource-description">Mô tả</label>
            <textarea
              id="resource-description"
              rows={4}
              maxLength={10000}
              value={form.description}
              onChange={(e) =>
                setForm({ ...form, description: e.target.value })
              }
            />
            <label htmlFor="resource-category">Loại học liệu</label>
            <input
              id="resource-category"
              required
              maxLength={80}
              value={form.category}
              onChange={(e) => setForm({ ...form, category: e.target.value })}
            />
            <label htmlFor="resource-author">Tác giả</label>
            <input
              id="resource-author"
              required
              maxLength={160}
              value={form.authorName}
              onChange={(e) => setForm({ ...form, authorName: e.target.value })}
            />
            <label htmlFor="resource-source">Nguồn tài liệu</label>
            <input
              id="resource-source"
              required
              maxLength={1000}
              value={form.source}
              onChange={(e) => setForm({ ...form, source: e.target.value })}
            />
            <label htmlFor="resource-license">Giấy phép / quyền sử dụng</label>
            <input
              id="resource-license"
              required
              maxLength={160}
              value={form.license}
              onChange={(e) => setForm({ ...form, license: e.target.value })}
              placeholder="Ví dụ: CC BY 4.0 hoặc sử dụng nội bộ có phép"
            />
            <label htmlFor="resource-terms">Điều kiện sử dụng</label>
            <textarea
              id="resource-terms"
              required
              rows={3}
              maxLength={2000}
              value={form.usageTerms}
              onChange={(e) => setForm({ ...form, usageTerms: e.target.value })}
            />
            <label htmlFor="resource-access">Quyền truy cập</label>
            <select
              id="resource-access"
              value={form.access}
              onChange={(e) =>
                setForm({
                  ...form,
                  access: e.target.value as "School" | "Subjects",
                })
              }
            >
              <option value="Subjects">Theo môn học</option>
              <option value="School">
                Trong trường — các tài khoản có quyền xem kho
              </option>
            </select>
            <fieldset>
              <legend>Môn học liên kết (chọn một hoặc nhiều môn)</legend>
              {subjects.map((s) => (
                <label className="check" key={s.id}>
                  <input
                    type="checkbox"
                    checked={form.subjectIds.includes(s.id)}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        subjectIds: e.target.checked
                          ? [...form.subjectIds, s.id]
                          : form.subjectIds.filter((id) => id !== s.id),
                      })
                    }
                  />
                  {s.code} · {s.name}
                </label>
              ))}
            </fieldset>
            <label htmlFor="resource-url">
              Đường dẫn học liệu (http/https, nếu có)
            </label>
            <input
              id="resource-url"
              type="url"
              maxLength={2000}
              value={form.externalUrl}
              onChange={(e) =>
                setForm({ ...form, externalUrl: e.target.value })
              }
            />
            {resource && (
              <ResourceFiles resource={{ ...resource, files: retained }} />
            )}
            {retained.map((file) => (
              <button
                className="button text danger"
                type="button"
                key={file.id}
                onClick={() =>
                  setRetained(retained.filter((f) => f.id !== file.id))
                }
              >
                Bỏ tệp {file.fileName}
              </button>
            ))}
            <label htmlFor="resource-files">
              Thêm tệp (tối đa 5 tệp, 25 MB/tệp)
            </label>
            <input
              id="resource-files"
              type="file"
              multiple
              onChange={(e) => {
                const next = Array.from(e.target.files ?? []);
                const invalid =
                  next.length + retained.length > 5 ||
                  next.some((f) => f.size > 25 * 1024 * 1024);
                e.target.setCustomValidity(
                  invalid ? "Tối đa 5 tệp, mỗi tệp không quá 25 MB." : "",
                );
                if (invalid) e.target.reportValidity();
                setSelected(invalid ? [] : next);
              }}
            />
            <button disabled={!form.subjectIds.length}>
              {action.busy ? "Đang lưu…" : "Lưu bản nháp"}
            </button>
          </fieldset>
        </form>
      </section>
    </>
  );
}
