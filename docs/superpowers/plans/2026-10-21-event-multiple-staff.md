# Multiple event staff implementation plan

> **For agentic workers:** Execute task-by-task using the executing-plans skill.

**Goal:** Open the employee picker above the visible staff section, select multiple employees, assign each a role, and save together.

**Architecture:** Keep the details modal open. Use a native modal dialog for the employee picker and role form above it. Reuse assignment rules inside one database transaction for the selected group, preserving permission, version, contract, overlap, budget and role validation.

**Tech Stack:** ASP.NET Core MVC/Razor, EF Core/PostgreSQL, native HTML dialog and existing Bootstrap styles.

**Spec:** The user's request in this session: add staff above the visible staff section and allow multiple selections. Individual roles and shared participation time follow the existing department-specific workflow.

- [x] Add integration cases for multiple assignments, rollback on invalid members, duplicate members and stale versions; confirm failure before implementation.
- [x] Extend the existing assignment service and controller for atomic groups, preserving the single assignment API.
- [x] Add checkboxes and a continue action; render per-employee roles with shared participation time and legacy allowance.
- [x] Keep the staff section visible beneath the dialog; preserve keyboard focus, cancellation and mobile layout.
- [x] Update affected browser checks and documentation; build and verify group saving and rollback through the service and browser.

Verified: web build (0 warnings/errors), all 40 PostgreSQL integration cases, browser details/group-save/rollback, full lifecycle, creation popup, and desktop/mobile/standalone overlay focus and scroll preservation. Preview and integration fixtures were cleaned up. The updated server remains available on port 5065.
