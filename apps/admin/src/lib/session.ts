import { SessionOptions, getIronSession } from "iron-session";
import { cookies } from "next/headers";

export type SessionData = {
  isAuthenticated: boolean;
  username?: string;
  csrfToken?: string;
};

export const sessionOptions: SessionOptions = {
  password: process.env.SESSION_PASSWORD ?? "complex-password-at-least-32-characters-long",
  cookieName: "subscript_admin_session",
  cookieOptions: {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/"
  }
};

export async function getSession() {
  return getIronSession<SessionData>(await cookies(), sessionOptions);
}
