import { createContext, useContext, useEffect, useReducer } from "react";
import type { ReactNode } from "react";
import { api, ApiError } from "./api";
import type { Identity } from "./types";

type State = {
  status: "loading" | "anonymous" | "authenticated";
  user: Identity | null;
  error: string | null;
};
type Action =
  | { type: "resolved"; user: Identity | null }
  | { type: "failed"; message: string }
  | { type: "loading" };
function reducer(state: State, action: Action): State {
  switch (action.type) {
    case "resolved":
      return {
        status: action.user ? "authenticated" : "anonymous",
        user: action.user,
        error: null,
      };
    case "failed":
      return {
        ...state,
        status: "anonymous",
        user: null,
        error: action.message,
      };
    case "loading":
      return { status: "loading", user: null, error: null };
  }
}
interface AuthContextValue extends State {
  signIn(email: string, password: string): Promise<void>;
  signOut(): Promise<void>;
  reload(): void;
}
const AuthContext = createContext<AuthContextValue | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, {
    status: "loading",
    user: null,
    error: null,
  });
  useEffect(() => {
    let active = true;
    api<Identity>("/api/auth/me")
      .then((user) => {
        if (active) dispatch({ type: "resolved", user });
      })
      .catch((error: unknown) => {
        if (!active) return;
        if (error instanceof ApiError && error.status === 401)
          dispatch({ type: "resolved", user: null });
        else
          dispatch({
            type: "failed",
            message: "Không thể kết nối máy chủ. Vui lòng tải lại trang.",
          });
      });
    const expire = () => dispatch({ type: "resolved", user: null });
    window.addEventListener("session-expired", expire);
    return () => {
      active = false;
      window.removeEventListener("session-expired", expire);
    };
  }, []);
  const signIn = async (email: string, password: string) => {
    const user = await api<Identity>("/api/frontend/login", {
      method: "POST",
      body: { email, password },
    });
    dispatch({ type: "resolved", user });
  };
  const signOut = async () => {
    await api<void>("/api/frontend/logout", { method: "POST" });
    dispatch({ type: "resolved", user: null });
  };
  return (
    <AuthContext.Provider
      value={{
        ...state,
        signIn,
        signOut,
        reload: () => window.location.reload(),
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("AuthProvider is required");
  return context;
}
export const can = (user: Identity, permission: string) =>
  user.role === "Admin" || user.permissionCodes.includes(permission);
