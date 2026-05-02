# Hangfire Dashboard

`MapHangfireDashboard` is wired in `samples/Web/Program.cs`. Navigate to `http://localhost:5080`:

![Hangfire dashboard overview](../assets/hangfire-dashboard-overview.png)

## Recurring jobs

**Recurring Jobs** shows all schedules registered at startup:

![Recurring jobs](../assets/hangfire-recurring-jobs.png)

## Succeeded jobs

**Succeeded Jobs** lists every completed execution. Jobs triggered via MCP appear alongside scheduler-driven runs — there's no separate channel:

![Succeeded jobs](../assets/hangfire-succeeded-jobs.png)

## Job detail

Click any job to inspect arguments, state transitions, and timing. This is the fastest way to confirm an MCP `CallTool` request landed correctly:

![Job detail](../assets/hangfire-job-detail.png)
