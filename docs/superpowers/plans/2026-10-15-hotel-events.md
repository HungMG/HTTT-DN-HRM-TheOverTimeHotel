# Hotel events, phase 1

**Goal:** Implement the user's approved notebook calendar, event CRUD/cancellation, cross-department staffing, assessment and HR/Admin acceptance.
**Authority:** The eight-section plan supplied in this conversation on 2026-10-15. Mobile confirmation and payroll transfer are phase 2; preserve payment references for that work.
**Stack:** Existing ASP.NET Core MVC, EF/Npgsql, Bootstrap, native JS/CSS. No new runtime dependencies.

## Decisions and interfaces

- Use `du_an` and `phan_cong_du_an` from PRD/02 section 9. Add `CHO_NGHIEM_THU`, expected attendance, cancellation reason, optimistic `version`, planned allowance and participation timestamps.
- Business times use Vietnam UTC+7; database timestamps are UTC. Intervals are half-open: adjacent bookings are allowed. Include every intersected local calendar day, excluding a midnight end instant.
- Roles: Admin/HR create and finalize; assigned Manager operates only their own events; IT/Employee have no web access. Recheck current account role from the database, do not trust posted IDs/roles.
- All event writes share a transaction advisory lock before checking conflicts, and compare event version; atomic acceptance freezes the event and assignments. Database indexes enforce duplicate assignment. No implicit overtime or payroll writes.
- Existing `nhat_ky_quan_tri` stores audit records in the same transaction. Event subject is `SK-{id}`.
- Keep unrelated pending edits. Work in this checkout because it includes pending authentication/schema work needed by this feature. Do not commit, reset, stop the existing server, or alter unrelated data.
- Inspect actual DB before applying an additive migration. Never silently create schema at web startup. Report unavailable schema in the calendar.

## Tasks

- [x] 1. Inspect live schema and document differences. Add Core entities/rules, EF mappings, additive migration and Data service. Service contract is `Services/IHotelEventService.cs` and DTOs in `Services/HotelEventContracts.cs`.
- [x] 2. Add server MVC controller and view models, validate server-side, register service and add sidebar entry. Surface errors at submitted fields, preserve calendar context on every navigation.
- [x] 3. Add responsive notebook calendar, event forms, three-tab details, assignment/assessment controls. Native dblclick only on day, event links stop propagation, keyboard/touch can select then Create.
- [x] 4. Add regression tests for rules and real DB transactions: conflict races, duplicate/expired staffing, wrong-role/scope, budget, immutable acceptance, stale versions. JS tests cover date selection/navigation and propagation.
- [x] 5. Apply migration after inspection, run full relevant test suites/build, inspect rendered pages and review security/concurrency. Fix all important findings and record evidence below.

## Review focus

Leap days/year changes; midnight and overnight event inclusion; cancelled events release bookings; stale edits cannot overwrite acceptance; simultaneous assignment to two different events cannot bypass conflict checks; invalid forms preserve input/context; an unauthorized Manager cannot infer other event details.

## Execution ledger

- Setup: user explicitly requested execution; proceed without another design approval. No branch/worktree operation required to implement in the authorized shared checkout.
- Pre-flight: UI and controller consume the same shared models; Data service consumes Core entities. Delegate file ownership to prevent concurrent edits.
- Live schema inspected: both event tables existed, zero events/assignments, missing phase-1 columns and waiting status. Additive migration applied with approval and schema reread successfully.
- Core rules 7/7, existing attendance rules 24/24, attendance flow 6/6, account queue policy 9/9 passed.
- Full PostgreSQL service suite: 25/25 passed, including normalized venue adjacency/conflicts, cross-department staffing, contracts, actual/planned budgets, role/scope spoofing, stale changes, concurrent venue/staffing/acceptance, atomic history and cleanup.
- A live query using non-UTC DateTimeOffset exposed a boundary conversion error; ListAsync now normalizes query bounds to UTC and the multiday regression passes.
- One intermediate midnight-contract case failed intermittently; focused rerun and final full suite pass. Added exact affected-row verification and persisted-date diagnostics to the fixture; no expectation was weakened.
- Browser passed real-login single/double click, leap day, keyboard, context cancellation, creation, multiday midnight exclusion, +N, cents assignment, full lifecycle/review/acceptance freeze, conflict field validation/input retention, Manager scope and IT denial; no JS errors.
- Independent security/concurrency review found no remaining important issue. Preserve return context when the saved event still intersects the selected day, otherwise navigate its new start date.
- Final build `dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj --no-restore -p:UseAppHost=false -o build-verification-events-final`: 0 warnings/errors. Core/JS checks fresh green and diff whitespace check clean (Git emits only LF-to-CRLF notices).
- Final browser visual pass verified both Create buttons track the selected day, exact organization/coordination field grouping, immediate invalid-time errors, settled animations, no desktop/mobile horizontal overflow and no JS errors. Screenshots in ignored `.probe`.
- Final preview is running separately on http://localhost:5077/SuKien (process 25388); existing port 5065 server preserved. Preview fixtures cleaned after checks.

## Follow-up: multiple days and creation popup

- User clarified that single clicks should toggle multiple calendar days. Keep existing event links opening their details. A double click ensures its day remains selected and opens one Bootstrap popup; ignore the second click's selection toggle.
- The popup reuses the existing form, validation, authorization and transactional save. Prefill earliest selected date at09:00 through latest at17:00, with explicit continuous-range copy; no selection defaults to today. No new schema or bulk event creation.
- Preserve selection in the calendar URL; popup cancellation/Escape keeps it and restores focus. Arrow keys move focus, Space/Enter toggle selection. Successful popup save returns to the calendar; server validation stays inside the popup with entered values retained.
- RED: Node multi-selection helper absent; browser double click navigated to standalone Create instead of opening popup. GREEN: Node tests and browser popup regression pass including toggle/range/leap day, reload, cancellation, keyboard focus, mobile viewport, save, conflict validation and entered-value retention.
- Fresh WebAdmin build: 0 warnings/errors. Existing core7, attendance24, attendance flow6, account queue9 checks pass. Independent review identified focus restoration, now fixed and verified by browser assertion.
- Updated preview on port5077 runs `build-verification-events-popup` (process8348). Temporary browser fixtures cleaned after verification.

## Follow-up: fix creation on the user's port5065

- Reproduced using a dedicated Admin account: GET5065 `/SuKien/Create?popup=true` returned500 with CS1061 (`EventFormViewModel.IsPopup` absent); same request on5077 returned200 and popup form. Port5065 was serving a stale WebAdmin assembly built16:51 while Razor/JS used the new popup model. This was not an expired login.
- Added tested HTTP-response classification: server500 -> server error,401/login redirect -> expired login,403/access denied -> permission,400 -> refresh form. Both popup loading and saving use it.
- Rebuilt default WebAdmin output and restarted the verified5065 repository process with the existing `http` launch profile. User authorization to fix the specified running web supersedes earlier preservation of that server. Build0 warnings/errors; canonical5065 launcher25132.
- Fresh GET diagnostic200 on both ports. Browser popup regression on **http://localhost:5065 as Admin** passed multi-day selection, range, cancel/focus, mobile, real save, conflict validation and retained inputs. Test fixtures cleaned. No database schema changes for this fix.
