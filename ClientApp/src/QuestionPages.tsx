import { useState } from "react";
import type { FormEvent } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { api, errorMessage } from "./api";
import { can, useAuth } from "./auth";
import { useAction, useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { QuestionItem, QuestionPage, Subject } from "./types";

type QuestionSubject = Pick<Subject, "id" | "code" | "name">;
const stateNames: Record<string, string> = {
  Draft: "Nháp",
  PendingReview: "Chờ duyệt",
  Approved: "Đã duyệt",
  Rejected: "Cần chỉnh sửa",
  Published: "Đã công bố",
  Archived: "Đã lưu trữ",
};
const difficultyNames = { Easy: "Dễ", Medium: "Trung bình", Hard: "Khó" };

export function Questions() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const state = params.get("state") ?? "";
  const page = Math.min(
    100000,
    Math.max(1, Math.floor(Number(params.get("page")) || 1)),
  );
  const allowed =
    !!user && can(user, "questions.view") && user.role !== "Student";
  const result = useRemote<QuestionPage>(
    allowed
      ? `/api/questions?q=${encodeURIComponent(q)}&state=${encodeURIComponent(state)}&page=${page}`
      : null,
  );
  const filter = (name: string, value: string) => {
    const next = new URLSearchParams(params);
    next.delete("page");
    value ? next.set(name, value) : next.delete(name);
    setParams(next, { replace: true });
  };
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền xem ngân hàng câu hỏi." />;
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">ĐÁNH GIÁ HỌC TẬP</span>
          <h1>Ngân hàng câu hỏi</h1>
          <p className="muted">
            Câu hỏi trắc nghiệm được phân loại và kiểm duyệt trước khi sử dụng.
          </p>
        </div>
        {can(user, "questions.manage") && (
          <Link className="button" to="/questions/new">
            + Soạn câu hỏi
          </Link>
        )}
      </div>
      <section className="panel">
        <div className="filters">
          <label>
            Tìm nội dung hoặc chủ đề
            <input
              maxLength={120}
              value={q}
              onChange={(e) => filter("q", e.target.value)}
            />
          </label>
          <label>
            Trạng thái
            <select
              value={state}
              onChange={(e) => filter("state", e.target.value)}
            >
              <option value="">Tất cả được phép xem</option>
              {Object.entries(stateNames).map(([key, value]) => (
                <option key={key} value={key}>
                  {value}
                </option>
              ))}
            </select>
          </label>
        </div>
        <Alert message={result.error} />
        {result.loading ? (
          <Loading />
        ) : (
          result.data && (
            <>
              {result.data.items.length ? (
                result.data.items.map((item) => (
                  <article className="assignment" key={item.id}>
                    <span className="badge">
                      {stateNames[item.isArchived ? "Archived" : item.state]} ·{" "}
                      {difficultyNames[item.difficulty]}
                    </span>
                    <h2>
                      <Link to={`/questions/${item.id}`}>{item.content}</Link>
                    </h2>
                    <small>
                      {item.subjectName} · {item.topic}
                    </small>
                  </article>
                ))
              ) : (
                <p className="empty">Chưa có câu hỏi phù hợp.</p>
              )}
              <div className="pagination">
                <span>
                  {result.data.total} câu hỏi · Trang {page}
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
                    disabled={page * 15 >= result.data.total}
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

export function QuestionDetail() {
  const { user } = useAuth();
  const { questionId = "" } = useParams();
  const result = useRemote<QuestionItem>(
    user && can(user, "questions.view") ? `/api/questions/${questionId}` : null,
  );
  const action = useAction();
  const [comment, setComment] = useState("");
  if (result.loading) return <Loading />;
  if (result.error) return <Alert message={result.error} />;
  if (!result.data) return null;
  const item = result.data;
  const change = (name: string, approve = false) => {
    if (name === "archive" && !window.confirm("Lưu trữ câu hỏi này?")) return;
    void action.run(
      (signal) =>
        api<QuestionItem>(`/api/questions/${item.id}/${name}`, {
          method: "POST",
          signal,
          body: { revision: item.revision, approve, comment },
        }),
      () => {
        setComment("");
        result.reload();
      },
    );
  };
  return (
    <>
      <Link className="back-link" to="/questions">
        ← Ngân hàng câu hỏi
      </Link>
      <section className="panel editor">
        <span className="badge">
          {stateNames[item.isArchived ? "Archived" : item.state]} ·{" "}
          {difficultyNames[item.difficulty]}
        </span>
        <h1>{item.content}</h1>
        <p>
          <strong>Môn:</strong> {item.subjectName} · <strong>Chủ đề:</strong>{" "}
          {item.topic}
        </p>
        <ol type="A">
          {item.options.map((option, index) => (
            <li
              key={option.id}
              className={index === item.correctOption ? "notice" : ""}
            >
              {option.text}
              {index === item.correctOption && " — Đáp án đúng"}
            </li>
          ))}
        </ol>
        {item.explanation && (
          <p className="plain-content">
            <strong>Giải thích:</strong> {item.explanation}
          </p>
        )}
        {item.reviewerName && (
          <p>
            <strong>Người duyệt:</strong> {item.reviewerName}
          </p>
        )}
        {item.reviewComment && (
          <div className="notice">Nhận xét: {item.reviewComment}</div>
        )}
        <Alert message={action.error} />
        <div className="actions">
          {item.canEdit && (
            <Link
              className="button secondary"
              to={`/questions/${item.id}/edit`}
            >
              Chỉnh sửa
            </Link>
          )}
          {item.canSubmit && (
            <button disabled={action.busy} onClick={() => change("submit")}>
              Gửi duyệt
            </button>
          )}
          {item.canPublish && (
            <button disabled={action.busy} onClick={() => change("publish")}>
              Công bố
            </button>
          )}
          {item.canArchive && (
            <button
              className="button text danger"
              disabled={action.busy}
              onClick={() => change("archive")}
            >
              Lưu trữ
            </button>
          )}
        </div>
        {item.canReview && (
          <div className="editor">
            <label>
              Nhận xét kiểm duyệt
              <textarea
                rows={3}
                maxLength={2000}
                value={comment}
                onChange={(e) => setComment(e.target.value)}
              />
            </label>
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
      </section>
    </>
  );
}

export function QuestionEditor() {
  const { user } = useAuth();
  const { questionId } = useParams();
  const allowed =
    !!user &&
    can(user, "questions.manage") &&
    (user.role === "Admin" || user.role === "Teacher");
  const existing = useRemote<QuestionItem>(
    allowed && questionId ? `/api/questions/${questionId}` : null,
  );
  const subjects = useRemote<QuestionSubject[]>(
    allowed ? "/api/questions/subjects" : null,
  );
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền soạn câu hỏi." />;
  if (existing.loading || subjects.loading) return <Loading />;
  if (existing.error || subjects.error)
    return <Alert message={existing.error ?? subjects.error} />;
  if (questionId && !existing.data?.canEdit)
    return <Alert message="Câu hỏi ở trạng thái này không thể chỉnh sửa." />;
  return (
    <QuestionForm
      key={questionId ?? "new"}
      question={existing.data}
      subjects={subjects.data ?? []}
    />
  );
}

function QuestionForm({
  question,
  subjects,
}: {
  question: QuestionItem | null;
  subjects: QuestionSubject[];
}) {
  const navigate = useNavigate();
  const action = useAction();
  const [form, setForm] = useState({
    subjectId: question?.subjectId ?? subjects[0]?.id ?? "",
    topic: question?.topic ?? "",
    difficulty:
      question?.difficulty ?? ("Medium" as "Easy" | "Medium" | "Hard"),
    content: question?.content ?? "",
    explanation: question?.explanation ?? "",
    correctOption: question?.correctOption ?? 0,
    options: question?.options.map((x) => x.text) ?? ["", "", "", ""],
  });
  const [objective, setObjective] = useState("");
  const [aiBusy, setAiBusy] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  async function generateAiDraft() {
    setAiBusy(true); setAiError(null);
    try {
      const saved = await api<QuestionItem>("/api/questions/ai-draft", { method: "POST", body: {
        subjectId: form.subjectId, topic: form.topic, difficulty: form.difficulty, learningObjective: objective,
      }});
      navigate(`/questions/${saved.id}/edit`);
    } catch (reason) { setAiError(errorMessage(reason)); }
    finally { setAiBusy(false); }
  }
  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(
      (signal) =>
        api<QuestionItem>(
          question ? `/api/questions/${question.id}` : "/api/questions",
          {
            method: question ? "PUT" : "POST",
            signal,
            body: { ...form, revision: question?.revision ?? 0 },
          },
        ),
      (saved) => navigate(`/questions/${saved.id}`),
    );
  };
  const option = (index: number, value: string) =>
    setForm({
      ...form,
      options: form.options.map((x, i) => (i === index ? value : x)),
    });
  return (
    <>
      <Link
        className="back-link"
        to={question ? `/questions/${question.id}` : "/questions"}
      >
        ← Quay lại
      </Link>
      <section className="panel editor">
        <h1>{question ? "Chỉnh sửa câu hỏi" : "Soạn câu hỏi"}</h1>
        <p className="muted">
          Câu hỏi được lưu nháp và phải do một người khác duyệt trước khi công
          bố.
        </p>
        <Alert message={action.error} />
        <Alert message={aiError} />
        {!subjects.length && (
          <Alert message="Bạn chưa có môn được phân công." />
        )}
        {!question && <div className="panel">
          <h2>Trợ lý tạo nháp</h2>
          <p className="muted">AI chỉ tạo bản nháp để giảng viên rà soát; vẫn phải qua người khác kiểm duyệt.</p>
          <label>Mục tiêu học tập<textarea rows={2} maxLength={1000} value={objective} onChange={(e)=>setObjective(e.target.value)} /></label>
          <button type="button" className="button secondary" disabled={aiBusy || !form.subjectId || !form.topic.trim() || !objective.trim()} onClick={generateAiDraft}>
            {aiBusy ? "Đang tạo nháp…" : "Tạo nháp bằng AI"}
          </button>
        </div>}
        <form onSubmit={submit}>
          <fieldset disabled={action.busy || !subjects.length}>
            <label>
              Môn học
              <select
                required
                value={form.subjectId}
                onChange={(e) =>
                  setForm({ ...form, subjectId: e.target.value })
                }
              >
                {subjects.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.code} · {s.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Chủ đề
              <input
                required
                maxLength={120}
                value={form.topic}
                onChange={(e) => setForm({ ...form, topic: e.target.value })}
              />
            </label>
            <label>
              Độ khó
              <select
                value={form.difficulty}
                onChange={(e) =>
                  setForm({
                    ...form,
                    difficulty: e.target.value as "Easy" | "Medium" | "Hard",
                  })
                }
              >
                <option value="Easy">Dễ</option>
                <option value="Medium">Trung bình</option>
                <option value="Hard">Khó</option>
              </select>
            </label>
            <label>
              Nội dung câu hỏi
              <textarea
                required
                rows={4}
                maxLength={5000}
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
              />
            </label>
            <fieldset>
              <legend>Các lựa chọn và đáp án đúng</legend>
              {form.options.map((text, index) => (
                <div className="filters" key={index}>
                  <label className="check">
                    <input
                      type="radio"
                      name="correct"
                      checked={form.correctOption === index}
                      onChange={() =>
                        setForm({ ...form, correctOption: index })
                      }
                    />
                    Đúng
                  </label>
                  <input
                    aria-label={`Lựa chọn ${index + 1}`}
                    required
                    maxLength={1000}
                    value={text}
                    onChange={(e) => option(index, e.target.value)}
                  />
                  {form.options.length > 2 && (
                    <button
                      type="button"
                      className="button text danger"
                      onClick={() =>
                        setForm({
                          ...form,
                          options: form.options.filter((_, i) => i !== index),
                          correctOption:
                            form.correctOption === index
                              ? 0
                              : form.correctOption > index
                                ? form.correctOption - 1
                                : form.correctOption,
                        })
                      }
                    >
                      Bỏ
                    </button>
                  )}
                </div>
              ))}
            </fieldset>
            <button
              type="button"
              className="button secondary"
              disabled={form.options.length >= 6}
              onClick={() =>
                setForm({ ...form, options: [...form.options, ""] })
              }
            >
              + Thêm lựa chọn
            </button>
            <label>
              Giải thích đáp án
              <textarea
                rows={3}
                maxLength={5000}
                value={form.explanation}
                onChange={(e) =>
                  setForm({ ...form, explanation: e.target.value })
                }
              />
            </label>
            <button>{action.busy ? "Đang lưu…" : "Lưu bản nháp"}</button>
          </fieldset>
        </form>
      </section>
    </>
  );
}
