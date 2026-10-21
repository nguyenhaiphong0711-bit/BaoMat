import { Link, useParams, useSearchParams } from "react-router-dom";
import { can, useAuth } from "./auth";
import { useRemote } from "./hooks";
import { Alert, Loading } from "./ui";
import type { StaffProgress, StaffStudentsPage } from "./types";

export function StaffDashboard() {
  const { user } = useAuth();
  const students = useRemote<StaffStudentsPage>(
    user?.role === "Staff" && can(user, "staff.students.view")
      ? "/api/staff/students"
      : null,
  );
  return (
    <>
      <section className="welcome">
        <div>
          <span className="eyebrow">CÁN BỘ QUẢN LÝ</span>
          <h1>Chào {user?.fullName}</h1>
          <p>Theo dõi sinh viên và học tập trong khoa được phân công.</p>
          {user && can(user, "staff.students.view") && (
            <Link className="button" to="/staff/students">
              Xem sinh viên →
            </Link>
          )}
        </div>
      </section>
      <Alert message={students.error} />
      {students.loading ? (
        <Loading />
      ) : (
        students.data && (
          <section className="panel stat">
            <span>{students.data.departmentName}</span>
            <strong>{students.data.total}</strong>
            <small>Sinh viên trong phạm vi quản lý</small>
          </section>
        )
      )}
      {user && !can(user, "staff.students.view") && (
        <Alert message="Bạn chưa được cấp quyền xem sinh viên. Vui lòng liên hệ Admin." />
      )}
    </>
  );
}
export function StaffStudents() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const query = params.get("q") ?? "";
  const page = Math.min(
    100000,
    Math.max(1, Math.floor(Number(params.get("page")) || 1)),
  );
  const allowed = user?.role === "Staff" && can(user, "staff.students.view");
  const students = useRemote<StaffStudentsPage>(
    allowed
      ? `/api/staff/students?q=${encodeURIComponent(query)}&page=${page}`
      : null,
  );
  const changePage = (next: number) =>
    setParams({ ...(query ? { q: query } : {}), page: String(next) });
  if (!allowed)
    return (
      <Alert message="Bạn không có quyền xem danh sách sinh viên của khoa." />
    );
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">CÁN BỘ QUẢN LÝ</span>
          <h1>Sinh viên trong khoa</h1>
          <p className="muted">
            {students.data?.departmentName ??
              "Phạm vi theo khoa được Admin phân công."}
          </p>
        </div>
      </div>
      <label htmlFor="staff-search">Tìm theo tên hoặc email</label>
      <input
        id="staff-search"
        maxLength={120}
        value={query}
        onChange={(e) =>
          setParams(e.target.value ? { q: e.target.value } : {}, {
            replace: true,
          })
        }
      />
      <Alert message={students.error} />
      {students.error && <button onClick={students.reload}>Thử lại</button>}
      {students.loading ? (
        <Loading />
      ) : (
        students.data && (
          <section className="panel">
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Sinh viên</th>
                    <th>Email</th>
                    <th>Ngành</th>
                    <th>Học kỳ</th>
                    <th>Trạng thái</th>
                    <th>Học tập</th>
                  </tr>
                </thead>
                <tbody>
                  {students.data.items.map((s) => (
                    <tr key={s.id}>
                      <td>{s.fullName}</td>
                      <td>{s.email}</td>
                      <td>{s.programName}</td>
                      <td>{s.currentSemester}</td>
                      <td>{s.isActive ? "Đang hoạt động" : "Đã khóa"}</td>
                      <td>
                        {user && can(user, "staff.progress.view") && (
                          <Link to={`/staff/students/${s.id}`}>
                            Xem tiến trình
                          </Link>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!students.data.items.length && (
              <p className="empty">
                Không có sinh viên phù hợp trong khoa được giao.
              </p>
            )}
            <div className="pagination">
              <span>
                {students.data.total} sinh viên · Trang {page}
              </span>
              <div className="actions">
                <button
                  className="button secondary"
                  disabled={page <= 1}
                  onClick={() => changePage(page - 1)}
                >
                  Trước
                </button>
                <button
                  className="button secondary"
                  disabled={
                    page * students.data.pageSize >= students.data.total
                  }
                  onClick={() => changePage(page + 1)}
                >
                  Sau
                </button>
              </div>
            </div>
          </section>
        )
      )}
    </>
  );
}
export function StaffStudentProgress() {
  const { user } = useAuth();
  const { studentId = "" } = useParams();
  const allowed = user?.role === "Staff" && can(user, "staff.progress.view");
  const report = useRemote<StaffProgress>(
    allowed
      ? `/api/staff/students/${encodeURIComponent(studentId)}/progress`
      : null,
  );
  if (!allowed)
    return <Alert message="Bạn không có quyền xem tiến trình của sinh viên." />;
  return (
    <>
      <Link className="back-link" to="/staff/students">
        ← Danh sách sinh viên
      </Link>
      <Alert message={report.error} />
      {report.error && <button onClick={report.reload}>Thử lại</button>}
      {report.loading ? (
        <Loading />
      ) : (
        report.data && (
          <>
            <div className="page-heading">
              <div>
                <span className="eyebrow">THEO DÕI HỌC TẬP</span>
                <h1>{report.data.student.fullName}</h1>
                <p className="muted">
                  {report.data.student.email} ·{" "}
                  {report.data.student.programName} · Học kỳ{" "}
                  {report.data.student.currentSemester}
                </p>
              </div>
            </div>
            <section className="panel">
              <p>
                Chỉ gồm các lớp đang tham gia thuộc khoa được giao. Điểm hiển
                thị là trung bình bài tập đã chấm, chưa phải điểm tổng kết môn.
              </p>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Lớp</th>
                      <th>Môn</th>
                      <th>Bài tập đã công bố</th>
                      <th>Đã nộp</th>
                      <th>Đã chấm</th>
                      <th>Điểm TB bài tập</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.data.courses.map((c, index) => (
                      <tr key={index}>
                        <td>{c.className}</td>
                        <td>{c.subjectName}</td>
                        <td>{c.assignmentCount}</td>
                        <td>{c.submittedCount}</td>
                        <td>{c.gradedCount}</td>
                        <td>{c.averageGrade?.toFixed(2) ?? "Chưa có điểm"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {!report.data.courses.length && (
                <p className="empty">
                  Chưa có lớp đang tham gia trong khoa được giao.
                </p>
              )}
            </section>
          </>
        )
      )}
    </>
  );
}
