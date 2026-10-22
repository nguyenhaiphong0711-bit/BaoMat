import { useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "./auth";
import { api, errorMessage } from "./api";
import { Alert, Loading } from "./ui";

function AuthShell({ children }: { children: ReactNode }) {
  return (
    <main className="auth-shell">
      <section className="auth-story">
        <Link className="brand" to="/">
          <span className="brand-mark">L</span>
          <span>LearnSpace</span>
        </Link>
        <div>
          <span className="eyebrow">MỖI NGÀY MỘT BƯỚC TIẾN</span>
          <h1>
            Không gian nhỏ.
            <br />
            Tri thức lớn.
          </h1>
          <p>
            Kết nối lớp học, học liệu và những mục tiêu tiếp theo của bạn trong
            một không gian.
          </p>
          <div className="story-grid">
            <span>
              01
              <br />
              <strong>Học tập</strong>
            </span>
            <span>
              02
              <br />
              <strong>Kết nối</strong>
            </span>
            <span>
              03
              <br />
              <strong>Phát triển</strong>
            </span>
          </div>
        </div>
        <small>LearnSpace · Cùng nhau học tốt hơn</small>
      </section>
      <section className="auth-form-wrap">
        <div className="auth-form">{children}</div>
      </section>
    </main>
  );
}
export function Login() {
  const { user, status, signIn, error: connectionError } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  if (status === "loading") return <Loading />;
  if (user) return <Navigate to="/" replace />;
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await signIn(email, password);
      const from: unknown = location.state?.from;
      navigate(
        typeof from === "string" &&
          from.startsWith("/") &&
          !from.startsWith("//")
          ? from
          : "/",
        { replace: true },
      );
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  return (
    <AuthShell>
      <span className="eyebrow">CHÀO MỪNG TRỞ LẠI</span>
      <h2>Đăng nhập</h2>
      <p className="muted">Tiếp tục hành trình học tập của bạn.</p>
      <Alert message={error ?? connectionError} />
      <form onSubmit={submit}>
        <label htmlFor="email">Email tài khoản</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          maxLength={254}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <label htmlFor="password">Mật khẩu</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          maxLength={128}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        <Link className="form-help" to="/forgot-password">
          Quên mật khẩu?
        </Link>
        <button disabled={busy}>
          {busy ? "Đang đăng nhập…" : "Đăng nhập →"}
        </button>
      </form>
      <p className="auth-note">Tài khoản được cấp bởi quản trị viên của bạn.</p>
    </AuthShell>
  );
}

export function AccountSecurity() {
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }
    setBusy(true);
    try {
      await api<{ message: string }>("/api/frontend/change-password", {
        method: "POST",
        body: { currentPassword, newPassword, confirmPassword },
      });
      window.dispatchEvent(new Event("session-expired"));
      navigate("/login", { replace: true });
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="panel form-panel">
      <span className="eyebrow">BẢO MẬT TÀI KHOẢN</span>
      <h1>Đổi mật khẩu</h1>
      <p className="muted">
        Mật khẩu mới phải có chữ hoa, chữ thường, số, ký tự đặc biệt và không
        trùng với 5 mật khẩu gần nhất.
      </p>
      <Alert message={error} />
      <form onSubmit={submit}>
        <label htmlFor="current-password">Mật khẩu hiện tại</label>
        <input id="current-password" type="password" autoComplete="current-password"
          maxLength={128} value={currentPassword}
          onChange={(event) => setCurrentPassword(event.target.value)} required />
        <label htmlFor="account-new-password">Mật khẩu mới</label>
        <input id="account-new-password" type="password" autoComplete="new-password"
          minLength={8} maxLength={128} value={newPassword}
          onChange={(event) => setNewPassword(event.target.value)} required />
        <label htmlFor="account-confirm-password">Xác nhận mật khẩu mới</label>
        <input id="account-confirm-password" type="password" autoComplete="new-password"
          minLength={8} maxLength={128} value={confirmPassword}
          onChange={(event) => setConfirmPassword(event.target.value)} required />
        <button disabled={busy}>{busy ? "Đang đổi…" : "Đổi mật khẩu"}</button>
      </form>
    </section>
  );
}
export function PasswordRecovery() {
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [step, setStep] = useState<"request" | "reset" | "done">("request");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (step === "request") {
        const data = await api<{ message: string }>(
          "/api/frontend/forgot-password",
          { method: "POST", body: { email } },
        );
        setMessage(data.message);
        setStep("reset");
      } else {
        if (password !== confirm)
          throw new Error("Mật khẩu xác nhận không khớp.");
        const data = await api<{ message: string }>(
          "/api/frontend/reset-password",
          {
            method: "POST",
            body: {
              email,
              code,
              newPassword: password,
              confirmPassword: confirm,
            },
          },
        );
        setMessage(data.message);
        setStep("done");
        setPassword("");
        setConfirm("");
        setCode("");
      }
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };
  return (
    <AuthShell>
      <span className="eyebrow">KHÔI PHỤC TÀI KHOẢN</span>
      <h2>
        {step === "request"
          ? "Quên mật khẩu?"
          : step === "reset"
            ? "Nhập mã xác nhận"
            : "Đã đổi mật khẩu"}
      </h2>
      {message && (
        <div className="notice" role="status">
          {message}
        </div>
      )}
      <Alert message={error} />
      {step !== "done" && (
        <form onSubmit={submit}>
          <label htmlFor="recovery-email">Email tài khoản</label>
          <input
            id="recovery-email"
            type="email"
            autoComplete="email"
            value={email}
            maxLength={254}
            onChange={(e) => setEmail(e.target.value)}
            required
            readOnly={step === "reset"}
          />
          {step === "reset" && (
            <>
              <label htmlFor="code">Mã OTP 6 chữ số</label>
              <input
                id="code"
                inputMode="numeric"
                autoComplete="one-time-code"
                pattern="[0-9]{6}"
                maxLength={6}
                value={code}
                onChange={(e) => setCode(e.target.value)}
                required
              />
              <label htmlFor="new-password">Mật khẩu mới</label>
              <input
                id="new-password"
                type="password"
                autoComplete="new-password"
                minLength={8}
                maxLength={128}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
              <label htmlFor="confirm-password">Xác nhận mật khẩu mới</label>
              <input
                id="confirm-password"
                type="password"
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                required
              />
            </>
          )}
          <button disabled={busy}>
            {busy
              ? "Đang xử lý…"
              : step === "request"
                ? "Gửi mã OTP"
                : "Đổi mật khẩu"}
          </button>
          {step === "reset" && (
            <button
              className="button secondary"
              type="button"
              disabled={busy}
              onClick={() => {
                setStep("request");
                setMessage(null);
                setError(null);
              }}
            >
              Gửi lại mã / đổi email
            </button>
          )}
        </form>
      )}
      <Link className="form-help" to="/login">
        ← Quay lại đăng nhập
      </Link>
    </AuthShell>
  );
}
