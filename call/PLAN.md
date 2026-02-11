# Multi-Agent Execution Plan

## Goal
Stabilize and validate `NewPCSetupWPF` with the recent improvements:
- Keep requested default app catalog intact.
- Keep custom selector editable at runtime.
- Ensure installer workflow (progress, ETA, cancel, retry, logs in-app) is consistent.
- Validate theme/profile UX and identify defects/risks before release.

## Acceptance Criteria
- Build-ready code path for WPF app (no obvious syntax or binding mismatches).
- Selector/profile behavior is coherent (`Default`, `Recommended`, `Dev`, `Custom`).
- Cancel/retry/logging flows are understandable and no blocking regressions are found.
- Reviewer report includes prioritized findings and concrete fixes.

## Tasks
| ID | Owner | Task | Deliverable | Dependencies | DoD | Estimate |
|---|---|---|---|---|---|---|
| FE-1 | agent-frontend | Review and polish `MainWindow.xaml` + bindings for UX consistency and responsive behavior. | Small focused diff + summary of UI/state improvements. | none | No broken bindings/events; layout remains usable; custom selector flow clear. | M |
| TEST-1 | agent-tester | Validate runtime logic paths via static/command checks available in environment and define manual QA checklist. | Validation report + any minimal fixes for testability. | FE-1 | Key flows covered (refresh, select/install/cancel/profile/custom add/remove/logs). | M |
| REV-1 | agent-code-reviewer | Perform read-only review of resulting diff for bugs, risks, regressions, missing tests. | Prioritized findings list with file references. | FE-1, TEST-1 | Findings actionable, severity-sorted, no fluff. | S |
| SUP-1 | agent-supervisor | Coordinate/integrate outputs and ensure AC coverage with concise release notes. | Final integration summary and remaining risks. | FE-1, TEST-1, REV-1 | AC mapped to implemented evidence; residual risks explicit. | S |
