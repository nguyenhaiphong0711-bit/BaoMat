import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "./api";
import { can, useAuth } from "./auth";
import { useAction, useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type {
  QuizAttempt,
  QuizManage,
  QuizStudentDetail,
  QuizSummary,
} from "./types";

const dateTime = (value: string) => new Date(value).toLocaleString("vi-VN");
const localInput = (date: Date) => {
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

export function Quizzes() {
  const { user } = useAuth();
  const allowed = !!user && can(user, "quizzes.view") && user.role !== "Staff";
  const result = useRemote<QuizSummary[]>(allowed ? "/api/quizzes" : null);
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền xem bài kiểm tra." />;
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">ĐÁNH GIÁ HỌC TẬP</span>
          <h1>Bài kiểm tra</h1>
          <p className="muted">
            Làm bài theo thời gian hoặc quản lý kết quả của lớp.
          </p>
        </div>
        {user && can(user, "quizzes.manage") && (
          <Link className="button" to="/quizzes/new">
            + Tạo bài kiểm tra
          </Link>
        )}
      </div>
      <section className="panel">
        <Alert message={result.error} />
        {result.loading ? (
          <Loading />
        ) : result.data?.length ? (
          result.data.map((quiz) => (
            <article className="assignment" key={quiz.id}>
              <span className="badge">
                {quiz.isArchived
                  ? "Đã lưu trữ"
                  : quiz.isPublished
                    ? "Đã công bố"
                    : "Nháp"}
                {quiz.attemptStatus === "Submitted" && ` · ${quiz.score}/10`}
              </span>
              <h2>
                <Link to={`/quizzes/${quiz.id}`}>{quiz.title}</Link>
              </h2>
              <p>
                {quiz.className} · {quiz.questionCount} câu ·{" "}
                {quiz.durationMinutes} phút
              </p>
              <small>
                Mở {dateTime(quiz.opensAt)} — đóng {dateTime(quiz.closesAt)}
              </small>
            </article>
          ))
        ) : (
          <p className="empty">Chưa có bài kiểm tra trong phạm vi của bạn.</p>
        )}
      </section>
    </>
  );
}

export function QuizDetail() {
  const { user } = useAuth();
  const { quizId = "" } = useParams();
  const navigate = useNavigate();
  const result = useRemote<QuizManage | QuizStudentDetail>(
    user && can(user, "quizzes.view") ? `/api/quizzes/${quizId}` : null,
  );
  const action = useAction();
  if (result.loading) return <Loading />;
  if (result.error) return <Alert message={result.error} />;
  if (!result.data) return null;
  const data = result.data;
  const quiz = data.quiz;
  const managed = "questions" in data;
  const change = (name: string) => {
    if (name === "archive" && !window.confirm("Lưu trữ bài kiểm tra này?"))
      return;
    void action.run(
      (signal) =>
        api(`/api/quizzes/${quiz.id}/${name}`, {
          method: "POST",
          signal,
          body: { revision: quiz.revision },
        }),
      result.reload,
    );
  };
  const start = () =>
    void action.run(
      (signal) =>
        api<QuizAttempt>(`/api/quizzes/${quiz.id}/attempts`, {
          method: "POST",
          signal,
        }),
      (attempt) => navigate(`/quizzes/attempts/${attempt.id}`),
    );
  return (
    <>
      <Link className="back-link" to="/quizzes">
        ← Danh sách bài kiểm tra
      </Link>
      <section className="panel editor">
        <span className="badge">
          {quiz.isArchived
            ? "Đã lưu trữ"
            : quiz.isPublished
              ? "Đã công bố"
              : "Nháp"}
        </span>
        <h1>{quiz.title}</h1>
        <p>
          {quiz.className} · {quiz.questionCount} câu · {quiz.durationMinutes}{" "}
          phút
        </p>
        <p>
          <strong>Thời gian:</strong> {dateTime(quiz.opensAt)} —{" "}
          {dateTime(quiz.closesAt)}
        </p>
        <p className="plain-content">{data.instructions}</p>
        <Alert message={action.error} />
        {managed ? (
          <ManageDetail data={data} busy={action.busy} change={change} />
        ) : (
          <StudentActions data={data} busy={action.busy} start={start} />
        )}
      </section>
    </>
  );
}

function ManageDetail({
  data,
  busy,
  change,
}: {
  data: QuizManage;
  busy: boolean;
  change: (name: string) => void;
}) {
  return (
    <>
      <div className="actions">
        {data.canEdit && (
          <Link
            className="button secondary"
            to={`/quizzes/${data.quiz.id}/edit`}
          >
            Chỉnh sửa
          </Link>
        )}
        {data.canPublish && (
          <button disabled={busy} onClick={() => change("publish")}>
            Công bố cho lớp
          </button>
        )}
        {data.canArchive && (
          <button
            className="button text danger"
            disabled={busy}
            onClick={() => change("archive")}
          >
            Lưu trữ
          </button>
        )}
      </div>
      <h2>Câu hỏi</h2>
      <ol>
        {data.questions.map((question) => (
          <li key={question.id}>
            {question.content}{" "}
            <small>
              ({question.topic} · đáp án{" "}
              {question.options[question.correctOption]?.id})
            </small>
          </li>
        ))}
      </ol>
      <h2>Kết quả sinh viên</h2>
      {data.attempts.length ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Sinh viên</th>
                <th>Bắt đầu</th>
                <th>Nộp bài</th>
                <th>Điểm</th>
              </tr>
            </thead>
            <tbody>
              {data.attempts.map((row) => (
                <tr key={row.studentId}>
                  <td>{row.studentName}</td>
                  <td>{dateTime(row.startedAt)}</td>
                  <td>
                    {row.submittedAt ? dateTime(row.submittedAt) : "Đang làm"}
                  </td>
                  <td>{row.score ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="empty">Chưa có sinh viên bắt đầu làm bài.</p>
      )}
    </>
  );
}
function StudentActions({
  data,
  busy,
  start,
}: {
  data: QuizStudentDetail;
  busy: boolean;
  start: () => void;
}) {
  if (data.attemptId)
    return (
      <Link className="button" to={`/quizzes/attempts/${data.attemptId}`}>
        {data.quiz.attemptStatus === "Submitted"
          ? `Xem kết quả ${data.quiz.score}/10`
          : "Tiếp tục làm bài"}
      </Link>
    );
  return data.canStart ? (
    <button disabled={busy} onClick={start}>
      {busy ? "Đang bắt đầu…" : "Bắt đầu làm bài"}
    </button>
  ) : (
    <Alert message="Hiện chưa nằm trong thời gian làm bài." />
  );
}

type EditorOptions = {
  classes: { id: string; name: string; subjectId: string }[];
  questions: {
    id: string;
    content: string;
    topic: string;
    difficulty: string;
  }[];
};
export function QuizEditor() {
  const { user } = useAuth();
  const { quizId } = useParams();
  const allowed =
    !!user &&
    can(user, "quizzes.manage") &&
    (user.role === "Admin" || user.role === "Teacher");
  const existing = useRemote<QuizManage>(
    allowed && quizId ? `/api/quizzes/${quizId}` : null,
  );
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền tạo bài kiểm tra." />;
  if (existing.loading) return <Loading />;
  if (existing.error) return <Alert message={existing.error} />;
  if (quizId && !existing.data?.canEdit)
    return <Alert message="Bài đã công bố hoặc lưu trữ không thể chỉnh sửa." />;
  return <QuizForm key={quizId ?? "new"} existing={existing.data} />;
}
function QuizForm({ existing }: { existing: QuizManage | null }) {
  const navigate = useNavigate();
  const action = useAction();
  const initialClass = existing?.quiz.classId ?? "";
  const [classId, setClassId] = useState(initialClass);
  const options = useRemote<EditorOptions>(
    `/api/quizzes/options${classId ? `?classId=${classId}` : ""}`,
  );
  const now = new Date();
  const [form, setForm] = useState({
    title: existing?.quiz.title ?? "",
    instructions: existing?.instructions ?? "",
    durationMinutes: existing?.quiz.durationMinutes ?? 30,
    opensAt: existing
      ? localInput(new Date(existing.quiz.opensAt))
      : localInput(now),
    closesAt: existing
      ? localInput(new Date(existing.quiz.closesAt))
      : localInput(new Date(now.getTime() + 86400000)),
    questionIds: existing?.questions.map((q) => q.id) ?? ([] as string[]),
  });
  useEffect(() => {
    if (!classId && options.data?.classes[0])
      setClassId(options.data.classes[0].id);
  }, [classId, options.data]);
  useEffect(() => {
    if (!existing) setForm((current) => ({ ...current, questionIds: [] }));
  }, [classId, existing]);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(
      (signal) =>
        api<QuizManage>(
          existing ? `/api/quizzes/${existing.quiz.id}` : "/api/quizzes",
          {
            method: existing ? "PUT" : "POST",
            signal,
            body: {
              ...form,
              classId,
              revision: existing?.quiz.revision ?? 0,
              opensAt: new Date(form.opensAt).toISOString(),
              closesAt: new Date(form.closesAt).toISOString(),
            },
          },
        ),
      (saved) => navigate(`/quizzes/${saved.quiz.id}`),
    );
  };
  return (
    <>
      <Link
        className="back-link"
        to={existing ? `/quizzes/${existing.quiz.id}` : "/quizzes"}
      >
        ← Quay lại
      </Link>
      <section className="panel editor">
        <h1>{existing ? "Chỉnh sửa bài kiểm tra" : "Tạo bài kiểm tra"}</h1>
        <p className="muted">
          Chỉ câu hỏi đã công bố đúng môn của lớp mới có thể được chọn.
        </p>
        <Alert message={options.error ?? action.error} />
        <form onSubmit={submit}>
          <fieldset disabled={action.busy || options.loading}>
            <label>
              Lớp học
              <select
                required
                value={classId}
                onChange={(e) => setClassId(e.target.value)}
              >
                {options.data?.classes.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Tiêu đề
              <input
                required
                maxLength={200}
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
              />
            </label>
            <label>
              Hướng dẫn
              <textarea
                rows={3}
                maxLength={5000}
                value={form.instructions}
                onChange={(e) =>
                  setForm({ ...form, instructions: e.target.value })
                }
              />
            </label>
            <div className="filters">
              <label>
                Thời gian mở
                <input
                  type="datetime-local"
                  required
                  value={form.opensAt}
                  onChange={(e) =>
                    setForm({ ...form, opensAt: e.target.value })
                  }
                />
              </label>
              <label>
                Thời gian đóng
                <input
                  type="datetime-local"
                  required
                  value={form.closesAt}
                  onChange={(e) =>
                    setForm({ ...form, closesAt: e.target.value })
                  }
                />
              </label>
              <label>
                Thời lượng (phút)
                <input
                  type="number"
                  min={5}
                  max={180}
                  required
                  value={form.durationMinutes}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      durationMinutes: Number(e.target.value),
                    })
                  }
                />
              </label>
            </div>
            <fieldset>
              <legend>Câu hỏi ({form.questionIds.length}/50)</legend>
              {options.data?.questions.length ? (
                options.data.questions.map((q) => (
                  <label className="check" key={q.id}>
                    <input
                      type="checkbox"
                      checked={form.questionIds.includes(q.id)}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          questionIds: e.target.checked
                            ? [...form.questionIds, q.id]
                            : form.questionIds.filter((id) => id !== q.id),
                        })
                      }
                    />
                    {q.content}{" "}
                    <small>
                      · {q.topic} · {q.difficulty}
                    </small>
                  </label>
                ))
              ) : (
                <p className="empty">Môn của lớp chưa có câu hỏi đã công bố.</p>
              )}
            </fieldset>
            <button disabled={!classId || !form.questionIds.length}>
              {action.busy ? "Đang lưu…" : "Lưu bản nháp"}
            </button>
          </fieldset>
        </form>
      </section>
    </>
  );
}

export function QuizAttemptPage() {
  const { attemptId = "" } = useParams();
  const result = useRemote<QuizAttempt>(`/api/quizzes/attempts/${attemptId}`);
  if (result.loading) return <Loading />;
  if (result.error) return <Alert message={result.error} />;
  if (!result.data) return null;
  return (
    <AttemptForm
      key={result.data.id + (result.data.submittedAt ?? "open")}
      attempt={result.data}
    />
  );
}
function AttemptForm({ attempt }: { attempt: QuizAttempt }) {
  const navigate = useNavigate();
  const action = useAction();
  const [answers, setAnswers] = useState(attempt.answers);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (attempt.submittedAt) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [attempt.submittedAt]);
  const remaining = Math.max(
    0,
    Math.ceil((new Date(attempt.expiresAt).getTime() - now) / 1000),
  );
  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    void action.run(
      (signal) =>
        api<QuizAttempt>(`/api/quizzes/attempts/${attempt.id}/submit`, {
          method: "POST",
          signal,
          body: { answers },
        }),
      () => navigate(`/quizzes/${attempt.quizId}`, { replace: true }),
    );
  };
  useEffect(() => {
    if (!attempt.submittedAt && remaining === 0 && !action.busy) submit();
  }, [remaining, attempt.submittedAt]);
  return (
    <>
      <Link className="back-link" to={`/quizzes/${attempt.quizId}`}>
        ← Thông tin bài kiểm tra
      </Link>
      <section className="panel editor">
        <h1>{attempt.title}</h1>
        {attempt.submittedAt ? (
          <>
            <div className="notice">
              Điểm: <strong>{attempt.score}/10</strong> · Nộp lúc{" "}
              {dateTime(attempt.submittedAt)}
            </div>
            {attempt.questions.map((question, index) => (
              <article className="assignment" key={index}>
                <h2>
                  Câu {index + 1}: {question.content}
                </h2>
                <ol type="A">
                  {question.options.map((option, optionIndex) => (
                    <li
                      key={option.id}
                      className={
                        optionIndex === question.correctOption ? "notice" : ""
                      }
                    >
                      {option.text}
                      {answers[index] === optionIndex && " · Bạn chọn"}
                    </li>
                  ))}
                </ol>
                {question.explanation && (
                  <p>
                    <strong>Giải thích:</strong> {question.explanation}
                  </p>
                )}
              </article>
            ))}
          </>
        ) : (
          <form onSubmit={submit}>
            <div className="notice">
              Còn {Math.floor(remaining / 60)}:
              {String(remaining % 60).padStart(2, "0")} · Câu chưa chọn sẽ không
              có điểm.
            </div>
            <Alert message={action.error} />
            {attempt.questions.map((question, index) => (
              <fieldset key={index}>
                <legend>
                  Câu {index + 1}: {question.content}
                </legend>
                {question.options.map((option, optionIndex) => (
                  <label className="check" key={option.id}>
                    <input
                      type="radio"
                      name={`question-${index}`}
                      checked={answers[index] === optionIndex}
                      onChange={() =>
                        setAnswers(
                          answers.map((answer, i) =>
                            i === index ? optionIndex : answer,
                          ),
                        )
                      }
                    />
                    {option.id}. {option.text}
                  </label>
                ))}
              </fieldset>
            ))}
            <button disabled={action.busy || remaining === 0}>
              {action.busy ? "Đang nộp…" : "Nộp bài"}
            </button>
          </form>
        )}
      </section>
    </>
  );
}
