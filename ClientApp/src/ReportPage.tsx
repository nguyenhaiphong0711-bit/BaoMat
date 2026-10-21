import { can, useAuth } from "./auth";
import { useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { LearningReport } from "./types";

export function LearningReports() {
  const { user } = useAuth();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const allowed = !!user && can(user, "reports.view");
  const query = new URLSearchParams();
  if (from) query.set("from", from);
  if (to) query.set("to", to);
  const suffix = query.size ? `?${query}` : "";
  const result = useRemote<LearningReport>(
    allowed ? `/api/reports/learning${suffix}` : null,
  );
  if (!allowed)
    return <Alert message="Bạn chưa được cấp quyền xem báo cáo học tập." />;
  if (result.loading) return <Loading />;
  if (result.error)
    return (
      <>
        <Alert message={result.error} />
        <button onClick={result.reload}>Thử lại</button>
      </>
    );
  if (!result.data) return null;
  const report = result.data;
  const supportTotal = Math.max(
    1,
    report.supportStatuses.reduce((sum, item) => sum + item.value, 0),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">THEO DÕI VÀ ĐÁNH GIÁ</span>
          <h1>Báo cáo học tập</h1>
          <p className="muted">
            Phạm vi: {report.scopeName}. Dữ liệu được tính trực tiếp theo quyền
            hiện tại.
          </p>
        </div>
      </div>
      <section className="panel filters">
        <label>Từ ngày<input aria-label="Từ ngày" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
        <label>Đến ngày<input aria-label="Đến ngày" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
        <a className="button secondary" href={`/api/reports/learning.csv${suffix}`}>Xuất CSV</a>
      </section>
      <div className="stats">
        {report.metrics.map((metric) => (
          <article className="panel stat" key={metric.key}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
            <small>{metric.unit}</small>
          </article>
        ))}
      </div>
      <div className="quick-grid">
        <section className="panel">
          <h2>Yêu cầu hỗ trợ</h2>
          {report.supportStatuses.map((item) => (
            <div key={item.key}>
              <div className="pagination">
                <span>{item.label}</span>
                <strong>{item.value}</strong>
              </div>
              <div
                className="report-track"
                aria-label={`${item.label}: ${item.value}`}
              >
                <span
                  style={{ width: `${(item.value * 100) / supportTotal}%` }}
                />
              </div>
            </div>
          ))}
        </section>
        <section className="panel">
          <h2>Sự kiện gần đây</h2>
          {report.recentEvents.length ? (
            <ul>
              {report.recentEvents.map((event, index) => (
                <li key={`${event.occurredAt}-${index}`}>
                  <strong>{event.summary}</strong>
                  <br />
                  <small>
                    {new Date(event.occurredAt).toLocaleString("vi-VN")}
                  </small>
                </li>
              ))}
            </ul>
          ) : (
            <p className="empty">Chưa có sự kiện mới được ghi nhận.</p>
          )}
        </section>
      </div>
    </>
  );
}
import { useState } from "react";
