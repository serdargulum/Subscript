import { NextRequest, NextResponse } from "next/server";
import { backendFetch } from "@/lib/backend";
import { getSession } from "@/lib/session";

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext) {
  const session = await getSession();
  if (!session.isAuthenticated) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  if (request.method !== "GET" && request.method !== "HEAD") {
    const csrfHeader = request.headers.get("x-csrf-token");
    if (!csrfHeader || !session.csrfToken || csrfHeader !== session.csrfToken) {
      return NextResponse.json({ error: "Invalid CSRF token" }, { status: 403 });
    }
  }

  const { path } = await context.params;
  const targetPath = `/api/v1/${path.join("/")}${request.nextUrl.search}`;
  const body =
    request.method === "GET" || request.method === "HEAD"
      ? undefined
      : await request.text();

  const upstream = await backendFetch(targetPath, {
    method: request.method,
    body,
    headers: body ? { "Content-Type": "application/json" } : undefined
  });

  const responseBody = await upstream.text();
  return new NextResponse(responseBody, {
    status: upstream.status,
    headers: {
      "Content-Type": upstream.headers.get("Content-Type") ?? "application/json"
    }
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
