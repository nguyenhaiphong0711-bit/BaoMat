import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "./api";
import { can, useAuth } from "./auth";
import { useAction, useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { Assignment, Attachment, Lesson, Submission } from "./types";

export function Files({ files }: { files: Attachment[] }) {
  return (
    <ul>
      {files.map((file) => (
        <li key={file.id}>
          <a href={`/File/Download/${file.id}`}>{file.fileName}</a>
          <small> ({Math.ceil(file.length / 1024)} KB)</small>
        </li>
      ))}
    </ul>
  );
}

// New files stay local until Save. The server checks ownership of every uploaded ID.
function FileInput({
  existing,
  selected,
  onSelect,
}: {
  existing: Attachment[];
  selected: File[];
  onSelect: (files: File[]) => void;
}) {
  return (
    <>
      <Files files={existing} />
      <label htmlFor="attachments">
        Thêm tệp đính kèm (tối đa 5 tệp, 25 MB/tệp)
      </label>
      <input
        id="attachments"
        type="file"
        multiple
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          if (
            files.length + existing.length > 5 ||
            files.some((f) => f.size > 25 * 1024 * 1024)
          ) {
            e.target.setCustomValidity(
              "Tối đa 5 tệp, mỗi tệp không quá 25 MB.",
            );
            e.target.reportValidity();
            onSelect([]);
          } else {
            e.target.setCustomValidity("");
            onSelect(files);
          }
        }}
      />
      {selected.length > 0 && (
        <p className="muted">
          Đã chọn {selected.length} tệp mới. Tệp chỉ được tải lên khi lưu.
        </p>
      )}
    </>
  );
}

async function saveWithFiles<T>(
  url: string,
  method: string,
  body: object,
  uploadUrl: string,
  existing: Attachment[],
  files: File[],
  signal: AbortSignal,
): Promise<T> {
  const uploaded: string[] = [];
  try {
    for (const file of files) {
      const form = new FormData();
      form.append("file", file);
      uploaded.push(
        await api<string>(uploadUrl, {
          method: "POST",
          body: form,
          responseType: "text",
          signal,
        }),
      );
    }
    return await api<T>(url, {
      method,
      signal,
      body: {
        ...body,
        uploadedFileIds: [...existing.map((f) => f.id), ...uploaded].join(","),
      },
    });
  } catch (error) {
    // Revert only staged uploads, never files already attached to a saved document.
    await Promise.allSettled(
      uploaded.map((id) =>
        api<void>("/Upload/Revert", {
          method: "DELETE",
          body: id,
        }),
      ),
    );
    throw error;
  }
}

function Back({ classId }: { classId: string }) {
  return (
    <Link className="back-link" to={`/classes/${classId}`}>
      ← Quay lại lớp học
    </Link>
  );
}

export function LessonEditor() {
  const { classId = "", lessonId } = useParams();
  const { user } = useAuth();
  const lesson = useRemote<Lesson>(
    lessonId ? `/api/lessons/${lessonId}` : null,
  );
  if (!user || user.role === "Student" || !can(user, "lessons.manage"))
    return <Alert message="Bạn không có quyền quản lý bài học." />;
  if (lesson.loading) return <Loading />;
  if (lesson.error) return <Alert message={lesson.error} />;
  if (lessonId && !lesson.data) return null;
  return (
    <LessonForm
      key={lessonId ?? classId}
      classId={lesson.data?.classId ?? classId}
      lesson={lesson.data}
    />
  );
}
function LessonForm({
  classId,
  lesson,
}: {
  classId: string;
  lesson: Lesson | null;
}) {
  const navigate = useNavigate();
  const action = useAction();
  const [files, setFiles] = useState<File[]>([]);
  const [existing, setExisting] = useState(lesson?.attachments ?? []);
  const [form, setForm] = useState({
    title: lesson?.title ?? "",
    content: lesson?.content ?? "",
    isPublished: lesson?.isPublished ?? false,
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void action.run(
      (signal) =>
        saveWithFiles<Lesson>(
          lesson
            ? `/api/lessons/${lesson.id}`
            : `/api/classes/${classId}/lessons`,
          lesson ? "PUT" : "POST",
          form,
          `/Upload/LessonAttachment?classId=${classId}`,
          existing,
          files,
          signal,
        ),
      () => navigate(`/classes/${classId}`),
    );
  };
  return (
    <>
      <Back classId={classId} />
      <section className="panel editor">
        <h1>{lesson ? "Chỉnh sửa bài học" : "Thêm bài học"}</h1>
        <Alert message={action.error} />
        <form onSubmit={submit}>
          <fieldset disabled={action.busy}>
            <label htmlFor="title">Tiêu đề</label>
            <input
              id="title"
              required
              maxLength={200}
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
            <label htmlFor="content">Nội dung bài học</label>
            <textarea
              id="content"
              required
              rows={12}
              maxLength={20000}
              value={form.content}
              onChange={(e) => setForm({ ...form, content: e.target.value })}
            />
            <FileInput
              existing={existing}
              selected={files}
              onSelect={setFiles}
            />
            {existing.map((file) => (
              <button
                key={file.id}
                type="button"
                className="button text danger"
                onClick={() =>
                  setExisting(existing.filter((f) => f.id !== file.id))
                }
              >
                Bỏ tệp {file.fileName}
              </button>
            ))}
            <label className="check">
              <input
                type="checkbox"
                checked={form.isPublished}
                onChange={(e) =>
                  setForm({ ...form, isPublished: e.target.checked })
                }
              />{" "}
              Công bố cho sinh viên
            </label>
            <button>{action.busy ? "Đang lưu…" : "Lưu bài học"}</button>
          </fieldset>
        </form>
      </section>
    </>
  );
}

export function AssignmentEditor() {
  const { classId = "", assignmentId } = useParams();
  const { user } = useAuth();
  const assignment = useRemote<Assignment>(
    assignmentId ? `/api/assignments/${assignmentId}` : null,
  );
  if (!user || user.role === "Student" || !can(user, "assignments.manage"))
    return <Alert message="Bạn không có quyền quản lý bài tập." />;
  if (assignment.loading) return <Loading />;
  if (assignment.error) return <Alert message={assignment.error} />;
  if (assignmentId && !assignment.data) return null;
  return (
    <AssignmentForm
      key={assignmentId ?? classId}
      classId={assignment.data?.classId ?? classId}
      assignment={assignment.data}
    />
  );
}
function localDate(value: string) {
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}
function AssignmentForm({
  classId,
  assignment,
}: {
  classId: string;
  assignment: Assignment | null;
}) {
  const navigate = useNavigate();
  const action = useAction();
  const [files, setFiles] = useState<File[]>([]);
  const [existing, setExisting] = useState(assignment?.attachments ?? []);
  const [form, setForm] = useState({
    title: assignment?.title ?? "",
    description: assignment?.description ?? "",
    dueDate: assignment ? localDate(assignment.dueDate) : "",
    isPublished: assignment?.isPublished ?? false,
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void action.run(
      (signal) =>
        saveWithFiles<Assignment>(
          assignment
            ? `/api/assignments/${assignment.id}`
            : `/api/classes/${classId}/assignments`,
          assignment ? "PUT" : "POST",
          { ...form, dueDate: new Date(form.dueDate).toISOString() },
          `/Upload/AssignmentAttachment?classId=${classId}`,
          existing,
          files,
          signal,
        ),
      () => navigate(`/classes/${classId}`),
    );
  };
  return (
    <>
      <Back classId={classId} />
      <section className="panel editor">
        <h1>{assignment ? "Chỉnh sửa bài tập" : "Thêm bài tập"}</h1>
        <Alert message={action.error} />
        <form onSubmit={submit}>
          <fieldset disabled={action.busy}>
            <label htmlFor="title">Tiêu đề</label>
            <input
              id="title"
              required
              maxLength={200}
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
            <label htmlFor="description">Yêu cầu bài tập</label>
            <textarea
              id="description"
              required
              rows={8}
              maxLength={20000}
              value={form.description}
              onChange={(e) =>
                setForm({ ...form, description: e.target.value })
              }
            />
            <label htmlFor="due-date">Hạn nộp</label>
            <input
              id="due-date"
              type="datetime-local"
              required
              value={form.dueDate}
              onChange={(e) => setForm({ ...form, dueDate: e.target.value })}
            />
            <FileInput
              existing={existing}
              selected={files}
              onSelect={setFiles}
            />
            {existing.map((file) => (
              <button
                key={file.id}
                type="button"
                className="button text danger"
                onClick={() =>
                  setExisting(existing.filter((f) => f.id !== file.id))
                }
              >
                Bỏ tệp {file.fileName}
              </button>
            ))}
            <label className="check">
              <input
                type="checkbox"
                checked={form.isPublished}
                onChange={(e) =>
                  setForm({ ...form, isPublished: e.target.checked })
                }
              />{" "}
              Công bố cho sinh viên
            </label>
            <button>{action.busy ? "Đang lưu…" : "Lưu bài tập"}</button>
          </fieldset>
        </form>
      </section>
    </>
  );
}

export function AssignmentWork() {
  const { assignmentId = "" } = useParams();
  const { user } = useAuth();
  const assignment = useRemote<Assignment>(`/api/assignments/${assignmentId}`);
  const student = user?.role === "Student" && can(user, "submissions.submit");
  const reviewer =
    !!user && user.role !== "Student" && can(user, "submissions.review");
  const mine = useRemote<Submission>(
    student ? `/api/assignments/${assignmentId}/submission` : null,
    true,
  );
  const submissions = useRemote<Submission[]>(
    reviewer ? `/api/assignments/${assignmentId}/submissions` : null,
  );
  if (assignment.loading || mine.loading || submissions.loading)
    return <Loading />;
  const error = assignment.error ?? mine.error ?? submissions.error;
  if (error)
    return (
      <>
        <Alert message={error} />
        <button
          onClick={() => {
            assignment.reload();
            mine.reload();
            submissions.reload();
          }}
        >
          Thử lại
        </button>
      </>
    );
  if (!assignment.data) return null;
  const a = assignment.data;
  return (
    <>
      <Back classId={a.classId} />
      <section className="panel editor">
        <h1>{a.title}</h1>
        <p className="plain-content">{a.description}</p>
        <p>Hạn nộp: {new Date(a.dueDate).toLocaleString("vi-VN")}</p>
        <Files files={a.attachments} />
      </section>
      {student && (
        <SubmissionForm
          key={assignmentId}
          assignment={a}
          submission={mine.data}
          onSaved={mine.reload}
        />
      )}
      {reviewer && (
        <section className="panel editor">
          <h2>Bài nộp của sinh viên ({submissions.data?.length ?? 0})</h2>
          {submissions.data?.length ? (
            submissions.data.map((s) => (
              <GradeForm
                key={`${s.id}-${s.submittedAt}-${s.gradedAt}`}
                submission={s}
                onSaved={submissions.reload}
              />
            ))
          ) : (
            <p>Chưa có sinh viên nộp bài.</p>
          )}
        </section>
      )}
    </>
  );
}
function SubmissionForm({
  assignment,
  submission,
  onSaved,
}: {
  assignment: Assignment;
  submission: Submission | null;
  onSaved: () => void;
}) {
  const action = useAction();
  const [content, setContent] = useState(submission?.content ?? "");
  const [existing, setExisting] = useState(submission?.attachments ?? []);
  const [files, setFiles] = useState<File[]>([]);
  const closed = new Date(assignment.dueDate) <= new Date();
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void action.run(
      (signal) =>
        saveWithFiles<Submission>(
          `/api/assignments/${assignment.id}/submission`,
          "PUT",
          { content },
          `/Upload/SubmissionFile?assignmentId=${assignment.id}`,
          existing,
          files,
          signal,
        ),
      onSaved,
    );
  };
  return (
    <section className="panel editor">
      <h2>Bài nộp của tôi</h2>
      {submission && (
        <div className="notice" role="status">
          Đã nộp lúc {new Date(submission.submittedAt).toLocaleString("vi-VN")}.
          Điểm: {submission.grade ?? "Chưa chấm"}
          {submission.teacherComment && (
            <p>Nhận xét: {submission.teacherComment}</p>
          )}
        </div>
      )}
      {closed && <p>Đã hết hạn nộp. Bạn vẫn có thể xem bài và kết quả.</p>}
      <Alert message={action.error} />
      <form onSubmit={submit}>
        <fieldset disabled={action.busy || closed}>
          <label htmlFor="answer">Nội dung trả lời</label>
          <textarea
            id="answer"
            rows={10}
            maxLength={20000}
            required={!existing.length && !files.length}
            value={content}
            onChange={(e) => setContent(e.target.value)}
          />
          <FileInput existing={existing} selected={files} onSelect={setFiles} />
          {existing.map((file) => (
            <button
              key={file.id}
              type="button"
              className="button text danger"
              onClick={() =>
                setExisting(existing.filter((f) => f.id !== file.id))
              }
            >
              Bỏ tệp {file.fileName}
            </button>
          ))}
          <p className="muted">
            Nộp lại sẽ thay thế bài trước và xóa điểm, nhận xét cũ để giáo viên
            chấm lại.
          </p>
          <button>
            {action.busy ? "Đang nộp…" : submission ? "Nộp lại bài" : "Nộp bài"}
          </button>
        </fieldset>
      </form>
    </section>
  );
}
function GradeForm({
  submission: s,
  onSaved,
}: {
  submission: Submission;
  onSaved: () => void;
}) {
  const action = useAction();
  const [grade, setGrade] = useState(s.grade?.toString() ?? "");
  const [comment, setComment] = useState(s.teacherComment);
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void action.run(
      (signal) =>
        api<Submission>(`/api/submissions/${s.id}/grade`, {
          method: "PUT",
          signal,
          body: { grade: Number(grade), teacherComment: comment },
        }),
      onSaved,
    );
  };
  return (
    <article className="assignment">
      <h3>{s.studentName ?? s.studentId}</h3>
      <small>Nộp lúc {new Date(s.submittedAt).toLocaleString("vi-VN")}</small>
      <p className="plain-content">{s.content}</p>
      <Files files={s.attachments} />
      {s.grade !== null && <p>Điểm hiện tại: {s.grade}/10</p>}
      <Alert message={action.error} />
      <form onSubmit={submit}>
        <fieldset disabled={action.busy}>
          <label htmlFor={`grade-${s.id}`}>Điểm (0–10)</label>
          <input
            id={`grade-${s.id}`}
            type="number"
            min={0}
            max={10}
            step="0.1"
            required
            value={grade}
            onChange={(e) => setGrade(e.target.value)}
          />
          <label htmlFor={`comment-${s.id}`}>Nhận xét</label>
          <textarea
            id={`comment-${s.id}`}
            rows={3}
            maxLength={4000}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
          />
          <button>{action.busy ? "Đang lưu…" : "Lưu điểm"}</button>
        </fieldset>
      </form>
    </article>
  );
}

export function ArchiveButton({
  kind,
  id,
  onArchived,
}: {
  kind: "lessons" | "assignments";
  id: string;
  onArchived: () => void;
}) {
  const action = useAction();
  return (
    <>
      <button
        className="button text danger"
        disabled={action.busy}
        onClick={() => {
          if (
            window.confirm(
              "Lưu trữ nội dung này? Sinh viên sẽ không còn xem được.",
            )
          )
            void action.run(
              (signal) =>
                api<void>(`/api/${kind}/${id}`, { method: "DELETE", signal }),
              onArchived,
            );
        }}
      >
        {action.busy ? "Đang lưu trữ…" : "Lưu trữ"}
      </button>
      <Alert message={action.error} />
    </>
  );
}
