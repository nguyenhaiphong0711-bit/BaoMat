import { useState } from "react";
import type { FormEvent } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { api } from "./api";
import { can, useAuth } from "./auth";
import { useAction, useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { SupportPage, SupportTicket } from "./types";

const statuses: Record<string, string> = {
  Open: "Mới",
  InProgress: "Đang xử lý",
  Resolved: "Đã giải quyết",
  Closed: "Đã đóng",
};
const priorities: Record<string, string> = {
  Low: "Thấp",
  Normal: "Bình thường",
  High: "Cao",
};

export function SupportTickets() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const status = params.get("status") ?? "";
  const page = Math.min(
    100000,
    Math.max(1, Math.floor(Number(params.get("page")) || 1)),
  );
  const allowed =
    !!user && can(user, "support.view") && user.role !== "Teacher";
  const result = useRemote<SupportPage>(
    allowed
      ? `/api/support?q=${encodeURIComponent(q)}&status=${encodeURIComponent(status)}&page=${page}`
      : null,
  );
  const filter = (name: string, value: string) => {
    const next = new URLSearchParams(params);
    next.delete("page");
    value ? next.set(name, value) : next.delete(name);
    setParams(next, { replace: true });
  };
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền xem yêu cầu hỗ trợ." />;
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">HỖ TRỢ NGƯỜI HỌC</span>
          <h1>Yêu cầu hỗ trợ</h1>
          <p className="muted">
            Trao đổi và theo dõi quá trình xử lý theo khoa.
          </p>
        </div>
        {user.role === "Student" && can(user, "support.create") && (
          <Link className="button" to="/support/new">
            + Gửi yêu cầu
          </Link>
        )}
      </div>
      <section className="panel">
        <div className="filters">
          <label>
            Tìm tiêu đề hoặc sinh viên
            <input
              maxLength={120}
              value={q}
              onChange={(e) => filter("q", e.target.value)}
            />
          </label>
          <label>
            Trạng thái
            <select
              value={status}
              onChange={(e) => filter("status", e.target.value)}
            >
              <option value="">Tất cả</option>
              {Object.entries(statuses).map(([key, value]) => (
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
                result.data.items.map((ticket) => (
                  <article className="assignment" key={ticket.id}>
                    <span className="badge">
                      {statuses[ticket.status]} · Ưu tiên{" "}
                      {priorities[ticket.priority]}
                    </span>
                    <h2>
                      <Link to={`/support/${ticket.id}`}>{ticket.subject}</Link>
                    </h2>
                    <p>
                      {ticket.studentName} · {ticket.departmentName}
                    </p>
                    <small>
                      {ticket.assignedStaffName
                        ? `Phụ trách: ${ticket.assignedStaffName}`
                        : "Chưa tiếp nhận"}{" "}
                      · Cập nhật{" "}
                      {new Date(ticket.updatedAt).toLocaleString("vi-VN")}
                    </small>
                  </article>
                ))
              ) : (
                <p className="empty">Chưa có yêu cầu phù hợp.</p>
              )}
              <div className="pagination">
                <span>
                  {result.data.total} yêu cầu · Trang {page}
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

export function SupportCreate() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const action = useAction();
  const [form, setForm] = useState({
    subject: "",
    category: "Học tập",
    priority: "Normal",
    content: "",
  });
  if (!user || user.role !== "Student" || !can(user, "support.create"))
    return <Alert message="Bạn chưa được cấp quyền gửi yêu cầu hỗ trợ." />;
  const submit = (event: FormEvent) => {
    event.preventDefault();
    void action.run(
      (signal) =>
        api<SupportTicket>("/api/support", {
          method: "POST",
          signal,
          body: form,
        }),
      (saved) => navigate(`/support/${saved.id}`),
    );
  };
  return (
    <>
      <Link className="back-link" to="/support">
        ← Yêu cầu hỗ trợ
      </Link>
      <section className="panel editor">
        <h1>Gửi yêu cầu hỗ trợ</h1>
        <p className="muted">
          Yêu cầu được chuyển đến cán bộ thuộc khoa trong hồ sơ của bạn.
        </p>
        <Alert message={action.error} />
        <form onSubmit={submit}>
          <fieldset disabled={action.busy}>
            <label>
              Tiêu đề
              <input
                required
                maxLength={200}
                value={form.subject}
                onChange={(e) => setForm({ ...form, subject: e.target.value })}
              />
            </label>
            <label>
              Nhóm vấn đề
              <select
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value })}
              >
                <option>Học tập</option>
                <option>Tài khoản</option>
                <option>Học phí</option>
                <option>Lịch học</option>
                <option>Khác</option>
              </select>
            </label>
            <label>
              Mức ưu tiên
              <select
                value={form.priority}
                onChange={(e) => setForm({ ...form, priority: e.target.value })}
              >
                <option value="Low">Thấp</option>
                <option value="Normal">Bình thường</option>
                <option value="High">Cao</option>
              </select>
            </label>
            <label>
              Nội dung
              <textarea
                required
                rows={6}
                maxLength={5000}
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
              />
            </label>
            <button>{action.busy ? "Đang gửi…" : "Gửi yêu cầu"}</button>
          </fieldset>
        </form>
      </section>
    </>
  );
}

export function SupportDetail() {
  const { ticketId = "" } = useParams();
  const result = useRemote<SupportTicket>(`/api/support/${ticketId}`);
  if (result.loading) return <Loading />;
  if (result.error) return <Alert message={result.error} />;
  if (!result.data) return null;
  return (
    <SupportConversation
      key={`${result.data.id}-${result.data.revision}`}
      ticket={result.data}
      reload={result.reload}
    />
  );
}
function SupportConversation({
  ticket,
  reload,
}: {
  ticket: SupportTicket;
  reload: () => void;
}) {
  const { user } = useAuth();
  const messageAction = useAction();
  const manageAction = useAction();
  const [content, setContent] = useState("");
  const staff = useRemote<{ id: string; fullName: string }[]>(
    ticket.canManage ? `/api/support/${ticket.id}/staff` : null,
  );
  const [management, setManagement] = useState({
    status: ticket.status,
    assignedStaffId:
      ticket.assignedStaffId ?? (user?.role === "Staff" ? user.id : ""),
  });
  const send = (event: FormEvent) => {
    event.preventDefault();
    void messageAction.run(
      (signal) =>
        api<SupportTicket>(`/api/support/${ticket.id}/messages`, {
          method: "POST",
          signal,
          body: { revision: ticket.revision, content },
        }),
      () => {
        setContent("");
        reload();
      },
    );
  };
  const manage = (event: FormEvent) => {
    event.preventDefault();
    void manageAction.run(
      (signal) =>
        api<SupportTicket>(`/api/support/${ticket.id}`, {
          method: "PUT",
          signal,
          body: { revision: ticket.revision, ...management },
        }),
      reload,
    );
  };
  return (
    <>
      <Link className="back-link" to="/support">
        ← Danh sách yêu cầu
      </Link>
      <section className="panel editor">
        <span className="badge">
          {statuses[ticket.status]} · Ưu tiên {priorities[ticket.priority]}
        </span>
        <h1>{ticket.subject}</h1>
        <p>
          {ticket.studentName} · {ticket.departmentName} · {ticket.category}
        </p>
        <p>
          <strong>Phụ trách:</strong>{" "}
          {ticket.assignedStaffName || "Chưa phân công"}
        </p>
        {ticket.messages.map((message) => (
          <article className="assignment" key={message.id}>
            <strong>{message.senderName}</strong>{" "}
            <small>
              · {message.senderRole} ·{" "}
              {new Date(message.sentAt).toLocaleString("vi-VN")}
            </small>
            <p className="plain-content">{message.content}</p>
          </article>
        ))}
        {ticket.canReply && (
          <form onSubmit={send}>
            <fieldset disabled={messageAction.busy}>
              <label>
                Trả lời
                <textarea
                  required
                  rows={4}
                  maxLength={5000}
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                />
              </label>
              <Alert message={messageAction.error} />
              <button>
                {messageAction.busy ? "Đang gửi…" : "Gửi trả lời"}
              </button>
            </fieldset>
          </form>
        )}
        {ticket.canManage && (
          <form onSubmit={manage}>
            <fieldset disabled={manageAction.busy || staff.loading}>
              <legend>Xử lý yêu cầu</legend>
              <label>
                Cán bộ phụ trách
                <select
                  value={management.assignedStaffId}
                  onChange={(e) =>
                    setManagement({
                      ...management,
                      assignedStaffId: e.target.value,
                    })
                  }
                >
                  <option value="">Chưa phân công</option>
                  {staff.data?.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.fullName}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Trạng thái
                <select
                  value={management.status}
                  onChange={(e) =>
                    setManagement({
                      ...management,
                      status: e.target.value as SupportTicket["status"],
                    })
                  }
                >
                  {Object.entries(statuses).map(([key, value]) => (
                    <option key={key} value={key}>
                      {value}
                    </option>
                  ))}
                </select>
              </label>
              <Alert message={staff.error ?? manageAction.error} />
              <button>
                {manageAction.busy ? "Đang lưu…" : "Cập nhật xử lý"}
              </button>
            </fieldset>
          </form>
        )}
      </section>
    </>
  );
}
