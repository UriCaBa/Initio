# WebApp Feasibility for NewPCSetup

## Short answer
You can host a web UI, but **it cannot install apps directly from the browser**.

`winget` requires local OS process execution on the target Windows machine, and browsers do not allow that for security reasons.

## Architecture that works
1. Hosted WebApp (frontend + API): selection UI, profiles, status dashboard.
2. Local Windows Agent (small installed app/service): receives install jobs and runs `winget` locally.
3. Secure channel: local agent polls or receives signed jobs from your backend.

## Result
- User opens the hosted web UI.
- Clicks install.
- Backend queues task.
- Local agent executes `winget` on that PC.
- Web UI shows real-time logs/progress from agent updates.

## What can be shared from current WPF app
- App catalog and profiles.
- Selection logic.
- Install pipeline rules (skip installed, retries, upgrade all, logs).

## What must change
- Replace direct process execution in UI with API calls.
- Implement local agent registration/authentication.
- Add server-side job orchestration and auditing.
