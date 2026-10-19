/**
 * Resolve what the bearer of a data ticket may READ and INGEST in the knowledge base.
 *
 * The browser supplies only the ticket. Everything else — role, department, whether uploads are
 * allowed — is decided by the WebAdmin from its own database and returned here. This is the
 * single place both chat and ingest ask that question, so the two cannot drift into different
 * ideas of who may do what.
 */

const REQUEST_TIMEOUT_MS = 5000;

/** Scope codes the backend understands. Anything else is dropped rather than forwarded. */
const SCOPE_PATTERN = /^(\*|all|dept:\d+)$/;

export interface KnowledgeDepartment {
  code: string;
  name: string;
}

export interface KnowledgeAccess {
  /** Whether the user may upload PDFs. False for anonymous or unauthorised callers. */
  canIngest: boolean;
  /** Scopes whose documents the user may read. Empty means "read nothing". */
  documentScopes: string[];
  /** Scopes the user may assign when uploading. Empty when `canIngest` is false. */
  ingestScopes: string[];
  /** Human-readable read scope, shown as a badge. Empty when no ticket was supplied. */
  scopeLabel: string;
  /** Departments offered in the upload picker. */
  departments: KnowledgeDepartment[];
  /** Account id of the ticket holder, stamped onto uploaded chunks. Null when anonymous. */
  accountId: number | null;
}

/** The narrowest possible answer: shared documents only, no uploads. */
export const READ_ONLY_ACCESS: KnowledgeAccess = {
  canIngest: false,
  documentScopes: ['all'],
  ingestScopes: [],
  scopeLabel: '',
  departments: [],
  accountId: null,
};

/**
 * Ask the WebAdmin about a ticket.
 *
 * Returns {@link READ_ONLY_ACCESS} instead of throwing in every degraded case: no ticket, no
 * configured WebAdmin address, a refusal, or a timeout. Chat must keep working as a document
 * assistant even when the data bridge is unavailable; it simply stops being able to reach
 * department-scoped documents, which is the safe direction.
 */
export async function fetchKnowledgeAccess(
  token: string | undefined,
): Promise<KnowledgeAccess> {
  if (!token) {
    return READ_ONLY_ACCESS;
  }

  const baseUrl = process.env.WEBADMIN_API_URL;
  if (!baseUrl) {
    console.warn(
      'WEBADMIN_API_URL is not set; knowledge access falls back to shared documents only.',
    );
    return READ_ONLY_ACCESS;
  }

  try {
    const response = await fetch(`${baseUrl}/api/ai-data/knowledge-access`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token }),
      // A hung WebAdmin must not hold the chat turn open. Same budget as the digest call.
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });

    if (!response.ok) {
      // 401 here is normal: the ten-minute ticket has expired. Logged without the ticket
      // itself, since that identifies the user.
      console.warn(
        `Knowledge access request rejected with status ${response.status}.`,
      );
      return READ_ONLY_ACCESS;
    }

    return sanitize(await response.json());
  } catch (error) {
    console.warn('Knowledge access request failed:', error);
    return READ_ONLY_ACCESS;
  }
}

/**
 * Coerce the WebAdmin response into the shape this module promises.
 *
 * The response crosses a process boundary, so it is treated as untrusted even though it comes
 * from our own server: a malformed field must degrade to the narrow default, never widen it.
 */
function sanitize(raw: unknown): KnowledgeAccess {
  if (!raw || typeof raw !== 'object') {
    return READ_ONLY_ACCESS;
  }

  const value = raw as Record<string, unknown>;

  return {
    canIngest: value.canIngest === true,
    documentScopes: normalizeScopes(value.documentScopes),
    ingestScopes: normalizeScopes(value.ingestScopes),
    scopeLabel: typeof value.scopeLabel === 'string' ? value.scopeLabel : '',
    departments: Array.isArray(value.departments)
      ? value.departments.flatMap((item) => {
          if (!item || typeof item !== 'object') return [];
          const department = item as Record<string, unknown>;
          if (
            typeof department.code !== 'string' ||
            typeof department.name !== 'string' ||
            !SCOPE_PATTERN.test(department.code)
          ) {
            return [];
          }
          return [{ code: department.code, name: department.name }];
        })
      : [],
    accountId:
      typeof value.accountId === 'number' && Number.isInteger(value.accountId) && value.accountId > 0
        ? value.accountId
        : null,
  };
}

/** Keep only well-formed scope codes, de-duplicated. An empty list stays empty. */
function normalizeScopes(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const scopes = value.filter(
    (item): item is string => typeof item === 'string' && SCOPE_PATTERN.test(item),
  );

  return [...new Set(scopes)];
}
