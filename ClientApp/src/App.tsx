import { useState } from "react";
import {
  Link,
  Navigate,
  NavLink,
  Outlet,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { can, useAuth } from "./auth";
import { errorMessage } from "./api";
import { Alert, Loading } from "./ui";
import { Login, PasswordRecovery } from "./AuthPages";
import { Dashboard, Classes, ClassDetail, Subjects } from "./Pages";

import {
  LessonEditor,
  AssignmentEditor,
  AssignmentWork,
} from "./LearningPages";

import {
  StaffDashboard,
  StaffStudents,
  StaffStudentProgress,
} from "./StaffPages";
import { roleLabel } from "./types";

import { Resources, ResourceDetail, ResourceEditor } from "./ResourcePages";
import { Questions, QuestionDetail, QuestionEditor } from "./QuestionPages";
import { Quizzes, QuizDetail, QuizEditor, QuizAttemptPage } from "./QuizPages";

function Workspace() {
  const { user, status, signOut, error, reload } = useAuth();
  const location = useLocation();
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  if (status === "loading") return <Loading />;
  if (error)
    return (
      <div className="standalone">
        <Alert message={error} />
        <button onClick={reload}>Thử lại</button>
      </div>
    );
  if (!user)
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  const logout = async () => {
    setBusy(true);
    setLogoutError(null);
    try {
      await signOut();
    } catch (error) {
      setLogoutError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  const legacy = (href: string, label: string) => (
    <a href={href}>
      {label}
      <span aria-hidden="true">↗</span>
    </a>
  );
  return (
    <div className="workspace">
      <button
        className="mobile-menu"
        aria-expanded={menuOpen}
        aria-controls="sidebar"
        onClick={() => setMenuOpen(!menuOpen)}
      >
        ☰ Menu
      </button>
      <aside className={`sidebar ${menuOpen ? "open" : ""}`} id="sidebar">
        <Link className="brand" to="/">
          <span className="brand-mark">L</span>
          <span>
            LearnSpace<small>KHÔNG GIAN HỌC TẬP</small>
          </span>
        </Link>
        <p className="nav-caption">KHÔNG GIAN CỦA BẠN</p>
        <nav onClick={() => setMenuOpen(false)}>
          <NavLink to="/" end>
            <span aria-hidden="true">◫</span>Tổng quan
          </NavLink>
          {can(user, "classes.view") && (
            <NavLink to="/classes">
              <span aria-hidden="true">▤</span>
              {user.role === "Student" ? "Lớp của tôi" : "Lớp học"}
            </NavLink>
          )}
          {user.role === "Staff" && can(user, "staff.students.view") && (
            <NavLink to="/staff/students">Sinh viên trong khoa</NavLink>
          )}
          {can(user, "resources.view") && (
            <NavLink to="/resources">Kho học liệu</NavLink>
          )}
          {user.role !== "Student" && can(user, "questions.view") && (
            <NavLink to="/questions">Ngân hàng câu hỏi</NavLink>
          )}
          {user.role !== "Staff" && can(user, "quizzes.view") && (
            <NavLink to="/quizzes">Bài kiểm tra</NavLink>
          )}
          <NavLink to="/subjects">
            <span aria-hidden="true">▦</span>Môn học
          </NavLink>
          {can(user, "schedule.view") && legacy("/Schedule/Index", "Lịch học")}
          {user.role === "Student" && (
            <>
              {can(user, "course-registration.view") &&
                legacy("/CourseRegistration/Index", "Đăng ký môn/lớp")}
              {can(user, "student-progress.view") &&
                legacy("/StudentAcademic/Progress", "Môn học & điểm")}
              {can(user, "tuition.view") &&
                legacy("/StudentAcademic/Tuition", "Học phí")}
            </>
          )}
          {user.role === "Teacher" &&
            can(user, "schedule.availability") &&
            legacy("/Schedule/Availability", "Giờ có thể dạy")}
          {user.role === "Admin" && (
            <>
              <p className="nav-caption">QUẢN TRỊ</p>
              {legacy("/Department/Index", "Khoa")}
              {legacy("/AcademicProgram/Index", "Ngành học")}
              {legacy("/AcademicTerm/Index", "Học kỳ")}
              {legacy("/User/Index", "Người dùng")}
              {legacy("/Permission/Index", "Phân quyền")}
              {legacy("/CourseRegistrationAdmin/Index", "Duyệt đăng ký")}
              {legacy("/TeachingAssignment/Index", "Phân công giảng dạy")}
              {legacy("/ActivityLog/Index", "Nhật ký hoạt động")}
            </>
          )}
        </nav>
        <div className="account">
          <span className="avatar">
            {user.fullName.slice(0, 1).toUpperCase()}
          </span>
          <div>
            <strong>{user.fullName}</strong>
            <small>{roleLabel[user.role]}</small>
          </div>
        </div>
        <Alert message={logoutError} />
        <button className="button ghost" disabled={busy} onClick={logout}>
          {busy ? "Đang đăng xuất…" : "Đăng xuất ↗"}
        </button>
      </aside>
      <div className="main-column">
        <header className="topbar">
          <div>
            <small>LEARN · CONNECT · GROW</small>
            <strong>Học tập mỗi ngày, tiến xa hơn.</strong>
          </div>
          <span>{new Date().toLocaleDateString("vi-VN")}</span>
        </header>
        <main>
          <Outlet />
        </main>
        <footer>LearnSpace · Nền tảng quản lý học tập</footer>
      </div>
    </div>
  );
}
export function App() {
  return (
    <Routes>
      <Route path="login" element={<Login />} />
      <Route path="forgot-password" element={<PasswordRecovery />} />
      <Route element={<Workspace />}>
        <Route index element={<Home />} />
        <Route path="staff/students" element={<StaffStudents />} />
        <Route
          path="staff/students/:studentId"
          element={<StaffStudentProgress />}
        />
        <Route path="resources" element={<Resources />} />
        <Route path="resources/new" element={<ResourceEditor />} />
        <Route path="resources/:resourceId" element={<ResourceDetail />} />
        <Route path="resources/:resourceId/edit" element={<ResourceEditor />} />
        <Route path="questions" element={<Questions />} />
        <Route path="questions/new" element={<QuestionEditor />} />
        <Route path="questions/:questionId" element={<QuestionDetail />} />
        <Route path="questions/:questionId/edit" element={<QuestionEditor />} />
        <Route path="quizzes" element={<Quizzes />} />
        <Route path="quizzes/new" element={<QuizEditor />} />
        <Route path="quizzes/:quizId" element={<QuizDetail />} />
        <Route path="quizzes/:quizId/edit" element={<QuizEditor />} />
        <Route
          path="quizzes/attempts/:attemptId"
          element={<QuizAttemptPage />}
        />
        <Route
          path="resources/:resourceId/versions/:version"
          element={<ResourceDetail />}
        />
        <Route path="subjects" element={<Subjects />} />
        <Route path="classes" element={<Classes />} />
        <Route path="classes/:id" element={<ClassDetail />} />
        <Route path="classes/:classId/lessons/new" element={<LessonEditor />} />
        <Route path="lessons/:lessonId/edit" element={<LessonEditor />} />
        <Route
          path="classes/:classId/assignments/new"
          element={<AssignmentEditor />}
        />
        <Route
          path="assignments/:assignmentId/edit"
          element={<AssignmentEditor />}
        />
        <Route path="assignments/:assignmentId" element={<AssignmentWork />} />
        <Route
          path="*"
          element={
            <section className="panel empty">
              <h1>Không tìm thấy trang</h1>
              <Link to="/">Về tổng quan</Link>
            </section>
          }
        />
      </Route>
    </Routes>
  );
}

function Home() {
  const { user } = useAuth();
  return user?.role === "Staff" ? <StaffDashboard /> : <Dashboard />;
}
