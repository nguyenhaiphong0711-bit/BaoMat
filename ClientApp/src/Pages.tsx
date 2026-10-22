import { useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { Alert, Loading } from "./ui";
import { api, errorMessage } from "./api";
import { can, useAuth } from "./auth";
import { useRemote } from "./hooks";
import type {
  Assignment,
  ClassRoom,
  ClassSummary,
  Department,
  Lesson,
  Subject,
} from "./types";

import { ArchiveButton, Files } from "./LearningPages";

function Heading({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow: string;
  title: string;
  description: string;
  children?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        <p className="muted">{description}</p>
      </div>
      {children}
    </div>
  );
}
function Empty({ message }: { message: string }) {
  return (
    <div className="empty">
      <span aria-hidden="true">▤</span>
      <h3>Chưa có dữ liệu</h3>
      <p className="muted">{message}</p>
    </div>
  );
}
function RemoteError({
  error,
  retry,
}: {
  error: string | null;
  retry: () => void;
}) {
  return error ? (
    <div>
      <Alert message={error} />
      <button className="button secondary" onClick={retry}>
        Thử lại
      </button>
    </div>
  ) : null;
}

function LessonCompletionButton({ lesson }: { lesson: Lesson }) {
  const [completed, setCompleted] = useState(lesson.isCompleted);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function toggle() {
    setPending(true);
    setError(null);
    try {
      await api(`/api/lessons/${lesson.id}/completion`, {
        method: completed ? "DELETE" : "PUT",
      });
      setCompleted((value) => !value);
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      setPending(false);
    }
  }
  return (
    <div className="actions">
      <button className="button secondary" disabled={pending} onClick={toggle}>
        {pending ? "Đang lưu…" : completed ? "Đánh dấu chưa hoàn thành" : "Đánh dấu đã hoàn thành"}
      </button>
      {completed && <span className="badge">Đã hoàn thành</span>}
      {error && <Alert message={error} />}
    </div>
  );
}
export function Dashboard() {
  const { user } = useAuth();
  const classes = useRemote<ClassRoom[]>(
    user && can(user, "classes.view") ? "/api/classes" : null,
  );
  const subjects = useRemote<Subject[]>("/api/subjects");
  if (!user) return null;
  return (
    <>
      <section className="welcome">
        <div>
          <span className="eyebrow">
            KHÔNG GIAN CỦA{" "}
            {user.role === "Admin"
              ? "QUẢN TRỊ VIÊN"
              : user.role === "Teacher"
                ? "GIẢNG VIÊN"
                : "SINH VIÊN"}
          </span>
          <h1>
            Chào {user.fullName.split(" ").at(-1)},<br />
            một ngày học tập mới!
          </h1>
          <p>Mọi lớp học, học liệu và hoạt động của bạn, cùng một nơi.</p>
          {can(user, "classes.view") && (
            <Link className="button" to="/classes">
              Khám phá lớp học →
            </Link>
          )}
        </div>
        <div className="welcome-art" aria-hidden="true">
          <span>LEARN</span>
          <div>✦</div>
          <small>
            THÊM TRI THỨC
            <br />
            THÊM CƠ HỘI
          </small>
        </div>
      </section>
      <div className="stats">
        <article className="panel stat">
          <span>
            Lớp{" "}
            {user.role === "Student"
              ? "đang tham gia"
              : "trong phạm vi quản lý"}
          </span>
          <strong>
            {classes.loading ? "…" : (classes.data?.length ?? "—")}
          </strong>
          <small>Được cập nhật từ tài khoản của bạn</small>
        </article>
        <article className="panel stat">
          <span>Môn học trong danh mục</span>
          <strong>
            {subjects.loading ? "…" : (subjects.data?.length ?? "—")}
          </strong>
          <small>Tra cứu mã môn, tên môn và khoa</small>
        </article>
        <article className="panel stat accent">
          <span>Tài khoản của bạn</span>
          <strong>
            {user.role === "Admin"
              ? "Quản trị"
              : user.role === "Teacher"
                ? "Giảng viên"
                : "Sinh viên"}
          </strong>
          <small>Quyền truy cập theo phân công</small>
        </article>
      </div>
      <RemoteError error={classes.error} retry={classes.reload} />
      <RemoteError error={subjects.error} retry={subjects.reload} />
      <Heading
        eyebrow="BẮT ĐẦU TỪ ĐÂY"
        title="Truy cập nhanh"
        description="Những điều bạn thường làm mỗi ngày."
      />
      <div className="quick-grid">
        {can(user, "classes.view") && (
          <Link className="panel quick" to="/classes">
            <span>▤</span>
            <h3>Lớp học</h3>
            <p>Xem bài học, bài tập và hoạt động của lớp.</p>
            <strong>Mở lớp học →</strong>
          </Link>
        )}
        <Link className="panel quick" to="/subjects">
          <span>▦</span>
          <h3>Danh mục môn học</h3>
          <p>Tìm môn theo tên, mã môn hoặc khoa.</p>
          <strong>Xem danh mục →</strong>
        </Link>
        {can(user, "schedule.view") && (
          <Link className="panel quick" to="/schedule">
            <span>◷</span>
            <h3>Lịch học</h3>
            <p>Theo dõi các buổi học và lịch giảng dạy.</p>
            <strong>Xem lịch →</strong>
          </Link>
        )}
      </div>
    </>
  );
}
const emptySubject = { code: "", name: "", description: "", departmentId: "" };
export function Subjects() {
  const { user } = useAuth();
  const subjects = useRemote<Subject[]>("/api/subjects");
  const departments = useRemote<Department[]>("/api/departments");
  const [params, setParams] = useSearchParams();
  const [editing, setEditing] = useState<Subject | "new" | null>(null);
  const [form, setForm] = useState(emptySubject);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const query = params.get("q") ?? "";
  const department = params.get("department") ?? "";
  const filtered = (subjects.data ?? []).filter(
    (s) =>
      (s.code + " " + s.name)
        .toLocaleLowerCase("vi")
        .includes(query.toLocaleLowerCase("vi")) &&
      (!department || s.departmentId === department),
  );
  const pageCount = Math.max(1, Math.ceil(filtered.length / 10));
  const page = Math.min(
    pageCount,
    Math.max(1, Number(params.get("page")) || 1),
  );
  const updateFilter = (name: string, value: string) => {
    const next = new URLSearchParams(params);
    value ? next.set(name, value) : next.delete(name);
    next.delete("page");
    setParams(next, { replace: true });
  };
  const start = (subject: Subject | null) => {
    setForm(
      subject
        ? {
            code: subject.code,
            name: subject.name,
            description: subject.description,
            departmentId: subject.departmentId,
          }
        : { ...emptySubject, departmentId: departments.data?.[0]?.id ?? "" },
    );
    setEditing(subject ?? "new");
    setError(null);
    setNotice(null);
  };
  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!editing) return;
    setBusy(true);
    setError(null);
    try {
      await api<Subject>(
        editing === "new" ? "/api/subjects" : `/api/subjects/${editing.id}`,
        { method: editing === "new" ? "POST" : "PUT", body: form },
      );
      setEditing(null);
      setNotice("Đã lưu môn học.");
      subjects.reload();
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  const remove = async (subject: Subject) => {
    if (
      !window.confirm(
        `Xóa môn “${subject.name}”? Môn đang được sử dụng sẽ không thể xóa.`,
      )
    )
      return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await api<void>(`/api/subjects/${subject.id}`, { method: "DELETE" });
      setNotice("Đã xóa môn học.");
      subjects.reload();
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <Heading
        eyebrow="DANH MỤC HỌC TẬP"
        title="Môn học"
        description="Tổ chức và tra cứu các môn học theo khoa."
      >
        {user?.role === "Admin" && (
          <button
            onClick={() => start(null)}
            disabled={busy || !departments.data?.length}
          >
            + Thêm môn học
          </button>
        )}
      </Heading>
      <Alert message={error} />
      {notice && (
        <div className="notice" role="status">
          {notice}
        </div>
      )}
      <RemoteError error={subjects.error} retry={subjects.reload} />
      <RemoteError error={departments.error} retry={departments.reload} />
      {editing && (
        <section className="panel editor">
          <h2>{editing === "new" ? "Thêm môn học" : "Chỉnh sửa môn học"}</h2>
          <form onSubmit={save}>
            <div className="form-grid">
              <div>
                <label htmlFor="subject-code">Mã môn</label>
                <input
                  id="subject-code"
                  value={form.code}
                  onChange={(e) => setForm({ ...form, code: e.target.value })}
                  required
                  minLength={2}
                  maxLength={20}
                  pattern="[A-Za-z0-9-]+"
                />
              </div>
              <div>
                <label htmlFor="subject-name">Tên môn</label>
                <input
                  id="subject-name"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  required
                  minLength={2}
                  maxLength={120}
                />
              </div>
            </div>
            <label htmlFor="subject-department">Khoa</label>
            <select
              id="subject-department"
              value={form.departmentId}
              onChange={(e) =>
                setForm({ ...form, departmentId: e.target.value })
              }
              required
            >
              <option value="">Chọn khoa</option>
              {departments.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.code} · {d.name}
                </option>
              ))}
            </select>
            <label htmlFor="subject-description">Mô tả</label>
            <textarea
              id="subject-description"
              value={form.description}
              onChange={(e) =>
                setForm({ ...form, description: e.target.value })
              }
              maxLength={1000}
              rows={3}
            />
            <div className="actions">
              <button disabled={busy}>
                {busy ? "Đang lưu…" : "Lưu môn học"}
              </button>
              <button
                className="button secondary"
                type="button"
                disabled={busy}
                onClick={() => setEditing(null)}
              >
                Hủy
              </button>
            </div>
          </form>
        </section>
      )}
      <section className="panel">
        <div className="filters">
          <label className="search" htmlFor="subject-search">
            <span>Tìm kiếm</span>
            <input
              id="subject-search"
              placeholder="Mã hoặc tên môn học…"
              value={query}
              onChange={(e) => updateFilter("q", e.target.value)}
            />
          </label>
          <label htmlFor="department-filter">
            Khoa
            <select
              id="department-filter"
              value={department}
              onChange={(e) => updateFilter("department", e.target.value)}
            >
              <option value="">Tất cả khoa</option>
              {departments.data?.map((d) => (
                <option value={d.id} key={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        {subjects.loading ? (
          <Loading />
        ) : filtered.length ? (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Mã môn</th>
                    <th>Môn học</th>
                    <th>Khoa</th>
                    {user?.role === "Admin" && <th>Thao tác</th>}
                  </tr>
                </thead>
                <tbody>
                  {filtered.slice((page - 1) * 10, page * 10).map((s) => (
                    <tr key={s.id}>
                      <td>
                        <span className="badge">{s.code}</span>
                      </td>
                      <td>
                        <strong>{s.name}</strong>
                        <small>{s.description}</small>
                      </td>
                      <td>
                        {departments.data?.find((d) => d.id === s.departmentId)
                          ?.name ?? "—"}
                      </td>
                      {user?.role === "Admin" && (
                        <td>
                          <div className="actions">
                            <button
                              className="button text"
                              disabled={busy}
                              onClick={() => start(s)}
                              aria-label={`Sửa ${s.name}`}
                            >
                              Sửa
                            </button>
                            <button
                              className="button text danger"
                              disabled={busy}
                              onClick={() => remove(s)}
                              aria-label={`Xóa ${s.name}`}
                            >
                              Xóa
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="pagination">
              <span>
                {filtered.length} môn học · Trang {page}/{pageCount}
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
                  disabled={page >= pageCount}
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
        ) : (
          !subjects.error && (
            <Empty
              message={
                query || department
                  ? "Không có môn học phù hợp với bộ lọc."
                  : "Môn học sẽ xuất hiện sau khi quản trị viên thêm vào danh mục."
              }
            />
          )
        )}
      </section>
    </>
  );
}
export function Classes() {
  const { user } = useAuth();
  const classes = useRemote<ClassRoom[]>(
    user && can(user, "classes.view") ? "/api/classes" : null,
  );
  const subjects = useRemote<Subject[]>("/api/subjects");
  const [params, setParams] = useSearchParams();
  const query = params.get("q") ?? "";
  if (!user) return null;
  if (!can(user, "classes.view"))
    return <Alert message="Bạn chưa được cấp quyền xem lớp học." />;
  const filtered = (classes.data ?? []).filter((c) =>
    c.name.toLocaleLowerCase("vi").includes(query.toLocaleLowerCase("vi")),
  );
  return (
    <>
      <Heading
        eyebrow="KẾT NỐI & HỌC TẬP"
        title={user.role === "Student" ? "Lớp của tôi" : "Lớp học"}
        description="Các lớp bạn được tham gia hoặc quản lý."
      >
        {user.role === "Admin" && (
          <Link className="button" to="/classes/new">
            + Tạo lớp học
          </Link>
        )}
      </Heading>
      <RemoteError error={classes.error} retry={classes.reload} />
      <RemoteError error={subjects.error} retry={subjects.reload} />
      <label className="class-search" htmlFor="class-search">
        Tìm lớp học
        <input
          id="class-search"
          value={query}
          placeholder="Nhập tên lớp…"
          onChange={(e) =>
            setParams(e.target.value ? { q: e.target.value } : {}, {
              replace: true,
            })
          }
        />
      </label>
      {classes.loading ? (
        <Loading />
      ) : filtered.length ? (
        <div className="class-grid">
          {filtered.map((c) => (
            <Link
              className="panel class-card"
              to={`/classes/${c.id}`}
              key={c.id}
            >
              <span className="class-icon">▤</span>
              <span className="badge">
                {subjects.data?.find((s) => s.id === c.subjectId)?.code ??
                  "MÔN HỌC"}
              </span>
              <h2>{c.name}</h2>
              <p>{c.description || "Khám phá bài học và hoạt động của lớp."}</p>
              <div className="class-footer">
                <span>
                  {user.role === "Student"
                    ? "Đã ghi danh"
                    : `${c.studentIds.length} học viên`}
                </span>
                <strong>Vào lớp →</strong>
              </div>
            </Link>
          ))}
        </div>
      ) : (
        !classes.error && (
          <section className="panel">
            <Empty
              message={
                query
                  ? "Không tìm thấy lớp phù hợp."
                  : "Lớp sẽ xuất hiện khi bạn được phân công hoặc ghi danh."
              }
            />
          </section>
        )
      )}
    </>
  );
}
export function ClassDetail() {
  const { user } = useAuth();
  const { id } = useParams();
  const classroom = useRemote<ClassSummary>(
    `/api/frontend/classes/${encodeURIComponent(id ?? "")}`,
  );
  const lessons = useRemote<Lesson[]>(
    user && can(user, "lessons.view")
      ? `/api/classes/${encodeURIComponent(id ?? "")}/lessons`
      : null,
  );
  const assignments = useRemote<Assignment[]>(
    user && can(user, "assignments.view")
      ? `/api/classes/${encodeURIComponent(id ?? "")}/assignments`
      : null,
  );
  if (!user) return null;
  if (classroom.loading) return <Loading />;
  if (classroom.error)
    return <RemoteError error={classroom.error} retry={classroom.reload} />;
  if (!classroom.data) return null;
  const c = classroom.data;
  return (
    <>
      <Link className="back-link" to="/classes">
        ← Danh sách lớp
      </Link>
      <Heading
        eyebrow={c.subjectName}
        title={c.name}
        description={c.description || "Không gian học tập của lớp."}
      >
        <a className="button secondary" href={`/Class/Details/${c.id}`}>
          Hồ sơ lớp ↗
        </a>
      </Heading>
      <section className="panel class-info">
        <span>
          Giảng viên <strong>{c.teacherName}</strong>
        </span>
        <span>
          Sĩ số{" "}
          <strong>
            {c.enrolledCount}/{c.enrollmentCapacity}
          </strong>
        </span>
        <a href={`/Schedule/Index?classId=${c.id}`}>Xem lịch lớp →</a>
      </section>
      <div className="content-grid">
        <section className="panel">
          <div className="section-heading">
            <h2>Bài học</h2>
            {user.role !== "Student" && can(user, "lessons.manage") && (
              <Link to={`/classes/${c.id}/lessons/new`}>+ Thêm bài học</Link>
            )}
          </div>
          <RemoteError error={lessons.error} retry={lessons.reload} />
          {lessons.loading ? (
            <Loading />
          ) : !can(user, "lessons.view") ? (
            <p className="muted">Bạn chưa được cấp quyền xem bài học.</p>
          ) : lessons.data?.length ? (
            lessons.data.map((l) => (
              <details className="lesson" key={l.id}>
                <summary>
                  {l.title}{" "}
                  {!l.isPublished && <span className="badge">Nháp</span>}
                </summary>
                <p className="plain-content">{l.content}</p>
                <Files files={l.attachments} />
                {user.role === "Student" && (
                  <LessonCompletionButton lesson={l} />
                )}
                {user.role !== "Student" && can(user, "lessons.manage") && (
                  <div className="actions">
                    <Link to={`/lessons/${l.id}/edit`}>Chỉnh sửa bài học</Link>
                    <ArchiveButton
                      kind="lessons"
                      id={l.id}
                      onArchived={lessons.reload}
                    />
                  </div>
                )}
              </details>
            ))
          ) : (
            !lessons.error && (
              <Empty message="Nội dung bài học sẽ xuất hiện tại đây." />
            )
          )}
        </section>
        <section className="panel">
          <div className="section-heading">
            <h2>Bài tập</h2>
            {user.role !== "Student" && can(user, "assignments.manage") && (
              <Link to={`/classes/${c.id}/assignments/new`}>
                + Thêm bài tập
              </Link>
            )}
          </div>
          <RemoteError error={assignments.error} retry={assignments.reload} />
          {assignments.loading ? (
            <Loading />
          ) : !can(user, "assignments.view") ? (
            <p className="muted">Bạn chưa được cấp quyền xem bài tập.</p>
          ) : assignments.data?.length ? (
            assignments.data.map((a) => (
              <article className="assignment" key={a.id}>
                <span className="badge">
                  {new Date(a.dueDate) < new Date() ? "Đã hết hạn" : "Đang mở"}
                  {!a.isPublished ? " · Nháp" : ""}
                </span>
                <h3>{a.title}</h3>
                <p className="plain-content">{a.description}</p>
                <Files files={a.attachments} />
                <small>
                  Hạn nộp: {new Date(a.dueDate).toLocaleString("vi-VN")}
                </small>
                <div className="actions">
                  {user.role === "Student" &&
                    can(user, "submissions.submit") && (
                      <Link
                        className="button secondary"
                        to={`/assignments/${a.id}`}
                      >
                        Bài nộp của tôi
                      </Link>
                    )}
                  {user.role !== "Student" &&
                    can(user, "submissions.review") && (
                      <Link to={`/assignments/${a.id}`}>Xem & chấm bài</Link>
                    )}
                  {user.role !== "Student" &&
                    can(user, "assignments.manage") && (
                      <>
                        <Link to={`/assignments/${a.id}/edit`}>Chỉnh sửa</Link>
                        <ArchiveButton
                          kind="assignments"
                          id={a.id}
                          onArchived={assignments.reload}
                        />
                      </>
                    )}
                </div>
              </article>
            ))
          ) : (
            !assignments.error && (
              <Empty message="Các bài tập của lớp sẽ xuất hiện tại đây." />
            )
          )}
        </section>
      </div>
    </>
  );
}
