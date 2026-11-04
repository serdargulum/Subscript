async function getCsrfToken(): Promise<string> {
  // Always read the current session token: it changes when the user signs in again.
  const response = await fetch("/api/auth/me", { cache: "no-store" });
  if (!response.ok) {
    window.location.href = "/login";
    throw new Error("Unauthorized");
  }

  const data = (await response.json()) as { csrfToken?: string };
  return data.csrfToken ?? "";
}

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);

  if (init.body) {
    headers.set("Content-Type", "application/json");
  }

  if (method !== "GET" && method !== "HEAD") {
    headers.set("x-csrf-token", await getCsrfToken());
  }

  const response = await fetch(`/api/backend${path}`, {
    ...init,
    headers,
    cache: "no-store"
  });

  if (response.status === 401) {
    window.location.href = "/login";
    throw new ApiError("Your session expired. Please sign in again.", 401);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as
      | { detail?: string; title?: string }
      | null;

    throw new ApiError(
      problem?.detail ?? problem?.title ?? `Request failed (${response.status})`,
      response.status
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return api<T>(path, {
    method: "POST",
    body: body === undefined ? undefined : JSON.stringify(body)
  });
}

export function queryString(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") {
      search.set(key, String(value));
    }
  }

  const encoded = search.toString();
  return encoded ? `?${encoded}` : "";
}
