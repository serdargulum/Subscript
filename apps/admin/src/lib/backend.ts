const apiBaseUrl = process.env.API_BASE_URL ?? "http://localhost:5226";
const apiKey = process.env.API_KEY ?? "";

export async function backendFetch(path: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  headers.set("X-Api-Key", apiKey);
  if (!headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }

  const url = `${apiBaseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  return fetch(url, {
    ...init,
    headers,
    cache: "no-store"
  });
}
