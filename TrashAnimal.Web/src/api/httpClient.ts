// Shared fetch wrapper for TrashAnimal.Api. GamesController's 422 body is JSON
// (GameCommandResponse); LobbiesController's 400/403/409/422 bodies are bare strings
// (BadRequest("...")/Conflict("...")/UnprocessableEntity("...")). ApiError tolerates both.

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, body: unknown, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) return null;

  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function errorMessageFrom(body: unknown, fallback: string): string {
  if (typeof body === 'string' && body.length > 0) return body;
  if (body && typeof body === 'object' && 'errorMessage' in body) {
    const message = (body as { errorMessage?: unknown }).errorMessage;
    if (typeof message === 'string' && message.length > 0) return message;
  }
  return fallback;
}

export interface RequestOptions {
  /** Status codes to return as a parsed body instead of throwing (e.g. 422 for game commands). */
  expectedStatuses?: number[];
}

/**
 * Issues an HTTP request against TrashAnimal.Api. Resolves with the parsed body for 2xx responses
 * and for any status listed in `expectedStatuses`. Throws `ApiError` for every other non-2xx status.
 */
export async function request<T>(
  path: string,
  init: RequestInit = {},
  options: RequestOptions = {},
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init.headers,
    },
  });

  const body = await parseResponseBody(response);

  if (!response.ok && !options.expectedStatuses?.includes(response.status)) {
    throw new ApiError(response.status, body, errorMessageFrom(body, response.statusText));
  }

  return body as T;
}

export function getJson<T>(path: string, options?: RequestOptions): Promise<T> {
  return request<T>(path, { method: 'GET' }, options);
}

export function postJson<T>(path: string, payload: unknown, options?: RequestOptions): Promise<T> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(payload) }, options);
}
