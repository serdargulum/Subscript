import { NextRequest, NextResponse } from "next/server";
import { randomUUID } from "crypto";
import { getSession } from "@/lib/session";

export async function POST(request: NextRequest) {
  const body = (await request.json()) as { username?: string; password?: string };
  const expectedUser = process.env.ADMIN_USERNAME ?? "admin";
  const expectedPass = process.env.ADMIN_PASSWORD ?? "change-me";

  if (body.username !== expectedUser || body.password !== expectedPass) {
    return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
  }

  const session = await getSession();
  session.isAuthenticated = true;
  session.username = body.username;
  session.csrfToken = randomUUID();
  await session.save();

  return NextResponse.json({ ok: true, csrfToken: session.csrfToken });
}
