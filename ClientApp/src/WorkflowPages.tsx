import { useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { api, errorMessage } from "./api";
import { useAuth } from "./auth";
import { useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { CourseOpening, CourseOpeningOptions, NotificationPage } from "./types";

const statusNames = { Pending: "Chờ duyệt", Processing: "Đang xử lý", Approved: "Đã duyệt", Rejected: "Từ chối" };

function Decision({ item, reload }: { item: CourseOpening; reload: () => void }) {
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function decide(approve: boolean) {
    setBusy(true); setError(null);
    try {
      await api(`/api/course-openings/${item.id}/decision`, { method: "POST", body: { approve, comment } });
      reload();
    } catch (reason) { setError(errorMessage(reason)); }
    finally { setBusy(false); }
  }
  return <div>
    <label>Nhận xét quyết định<textarea value={comment} maxLength={1000} onChange={(e) => setComment(e.target.value)} /></label>
    <div className="actions">
      <button disabled={busy} onClick={() => decide(true)}>Duyệt và tạo lớp</button>
      <button className="button danger" disabled={busy || !comment.trim()} onClick={() => decide(false)}>Từ chối</button>
    </div>
    <Alert message={error} />
  </div>;
}

export function CourseOpenings() {
  const { user } = useAuth();
  const rows = useRemote<CourseOpening[]>(user ? "/api/course-openings" : null);
  const options = useRemote<CourseOpeningOptions>(user?.role === "Teacher" ? "/api/course-openings/options" : null);
  const [form, setForm] = useState({ subjectId: "", academicTermId: "", name: "", description: "", enrollmentCapacity: 40 });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  if (!user || !["Teacher", "Admin"].includes(user.role)) return <Alert message="Bạn không có quyền xem đề xuất mở lớp." />;
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(null); setNotice(null);
    try {
      await api("/api/course-openings", { method: "POST", body: form });
      setForm({ subjectId: "", academicTermId: "", name: "", description: "", enrollmentCapacity: 40 });
      setNotice("Đã gửi đề xuất cho quản trị viên."); rows.reload();
    } catch (reason) { setError(errorMessage(reason)); }
    finally { setBusy(false); }
  }
  return <>
    <div className="page-heading"><div><span className="eyebrow">QUY TRÌNH MỞ LỚP</span><h1>Đề xuất mở lớp</h1><p className="muted">Giảng viên đề xuất, Admin kiểm tra và duyệt trước khi lớp được tạo.</p></div></div>
    <Alert message={error ?? rows.error ?? options.error} />
    {notice && <div className="notice" role="status">{notice}</div>}
    {user.role === "Teacher" && <section className="panel editor"><h2>Đề xuất mới</h2>
      <form onSubmit={submit}>
        <div className="form-grid">
          <label>Môn học<select required value={form.subjectId} onChange={(e) => setForm({...form,subjectId:e.target.value})}><option value="">Chọn môn</option>{options.data?.subjects.map(x=><option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
          <label>Học kỳ<select required value={form.academicTermId} onChange={(e) => setForm({...form,academicTermId:e.target.value})}><option value="">Chọn học kỳ</option>{options.data?.terms.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        </div>
        <label>Tên lớp<input required minLength={2} maxLength={160} value={form.name} onChange={(e)=>setForm({...form,name:e.target.value})}/></label>
        <label>Mô tả<textarea maxLength={2000} value={form.description} onChange={(e)=>setForm({...form,description:e.target.value})}/></label>
        <label>Sĩ số tối đa<input type="number" min={1} max={500} value={form.enrollmentCapacity} onChange={(e)=>setForm({...form,enrollmentCapacity:Number(e.target.value)})}/></label>
        <button disabled={busy || !options.data?.subjects.length || !options.data?.terms.length}>{busy ? "Đang gửi…" : "Gửi đề xuất"}</button>
      </form></section>}
    <section className="panel"><h2>{user.role === "Admin" ? "Đề xuất cần xử lý" : "Đề xuất của tôi"}</h2>
      {rows.loading ? <Loading /> : rows.data?.length ? rows.data.map(item=><article className="assignment" key={item.id}>
        <span className="badge">{statusNames[item.status]}</span><h3>{item.name}</h3>
        <p>{item.subjectName} · {item.termName} · {item.enrollmentCapacity} sinh viên</p>
        {user.role === "Admin" && <small>Giảng viên: {item.teacherName}</small>}
        {item.description && <p>{item.description}</p>}{item.decisionComment && <p><strong>Nhận xét:</strong> {item.decisionComment}</p>}
        {item.createdClassId && <Link to={`/classes/${item.createdClassId}`}>Mở lớp đã tạo →</Link>}
        {user.role === "Admin" && item.status === "Pending" && <Decision item={item} reload={rows.reload}/>}</article>) : <p className="empty">Chưa có đề xuất mở lớp.</p>}
    </section>
  </>;
}

export function Notifications() {
  const result = useRemote<NotificationPage>("/api/notifications");
  const [error, setError] = useState<string | null>(null);
  async function read(id?: string) {
    setError(null);
    try { await api(id ? `/api/notifications/${id}/read` : "/api/notifications/read", { method: "POST" }); result.reload(); }
    catch (reason) { setError(errorMessage(reason)); }
  }
  return <><div className="page-heading"><div><span className="eyebrow">CẬP NHẬT CÁ NHÂN</span><h1>Thông báo</h1><p className="muted">Các thay đổi liên quan trực tiếp đến tài khoản của bạn.</p></div>
    {!!result.data?.unread && <button className="button secondary" onClick={()=>read()}>Đánh dấu tất cả đã đọc</button>}</div>
    <Alert message={error ?? result.error}/>{result.loading ? <Loading/> : <section className="panel">
      {result.data?.items.length ? result.data.items.map(item=><article className="assignment" key={item.id}>
        <span className="badge">{item.readAt ? "Đã đọc" : "Mới"}</span><h2>{item.title}</h2><p>{item.message}</p>
        <small>{new Date(item.createdAt).toLocaleString("vi-VN")}</small><div className="actions">
          {item.link && <a href={item.link} onClick={()=>void read(item.id)}>Xem chi tiết</a>}
          {!item.readAt && <button className="button secondary" onClick={()=>read(item.id)}>Đánh dấu đã đọc</button>}
        </div></article>) : <p className="empty">Bạn chưa có thông báo.</p>}
    </section>}</>;
}
