# Search list cache implementation plan

**Goal:** Reuse list data for keyword searches for 15 minutes without changing the UI.

**Approved behavior:** The next request after expiry reloads the list. Successful writes invalidate cached lists. Keyword, paging and presentation remain request-specific. Approval and write validation always read current database state.

**Architecture:** A singleton cache stores serialized snapshots and returns independent copies to each request. Use absolute expiry and coalesce concurrent misses. Cache keys include authorization scope, category and date/period where relevant, never keyword or page. EF interceptors invalidate snapshots after successful writes, including bulk SQL, and after transaction completion to prevent caching intermediate transaction state.

**Tech stack:** ASP.NET Core, existing IMemoryCache, EF Core interceptors; no new dependencies.

## Tasks

- [x] Add executable cache checks: repeated keyword requests reuse data, absolute 15-minute expiry, invalidation, concurrent loads, failed loads, independent object graphs, and writes during loading.
- [x] Implement shared cache and register EF invalidation. Work in the current checkout; leave changes uncommitted for user review.
- [x] Cache employee/contract/catalog lists before filtering; remove redundant uncached list queries in their search actions.
- [x] Cache scoped request lists, attendance by date/scope, and payroll by period. Preserve authorization before cache lookup and all fresh write-side queries.
- [x] Build WebAdmin, run cache checks and existing relevant executable tests, then obtain a fresh code review and address material findings.

## Review focus

- Cross-department or cross-user data exposure through cache keys.
- Stale snapshots republished by in-flight loads after writes or transaction completion.
- Cached EF entities becoming tracked or mutated across requests.
- Auxiliary queries accidentally hitting the database for every keyword.
- Date-sensitive calculations remaining current while underlying lists are cached.

## Progress

Initial inspection: 13 server-side search groups; shift planning already filters in the browser. Existing cache registration can be reused. No application UI changes are needed.

Implementation complete. Independent review found two issues, both fixed: read-only payroll transaction commits now preserve cache; attendance and leave-request rows are cached together with dependent maps to prevent cross-entry invalidation races. Regression checks failed before each fix and passed afterwards. Transaction dirtiness is tracked with weak references to avoid retaining abandoned transactions.

Validation: WebAdmin builds successfully. Executable checks passed for SearchListCache, AuditLogRetention, AccountQueueRules, AttendanceRules, AttendanceFlow, PayrollRules, ReportRules, HotelEvents, and HotelEventsWeb. HotelEventsWeb initially needed its missing restore assets, then passed after restoring from the existing package cache. New controller checks prime snapshots and reject all attempted database connections; warm keyword requests across all 13 groups produce zero connection attempts. No live database or browser session was used. No view, JavaScript or CSS files changed.

Operational scope: cache lives in this WebAdmin process. Any successful intercepted write invalidates all search snapshots conservatively, including data written via mobile API controllers using the same context registration. Writes from other processes become visible after expiry and the next request. Cold snapshot loads are serialized; independent warm reads do not wait for that lock.
