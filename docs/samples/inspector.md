# MCP Inspector Walkthrough

The Aspire AppHost starts the [MCP Inspector](https://github.com/modelcontextprotocol/inspector) pre-connected to the `server` resource's `/mcp` endpoint. Open the inspector URL from `aspire ps` (or the Aspire dashboard under the `inspector` resource).

## Connect

The inspector opens with `http://localhost:5080/mcp` already filled in. Click **Connect**:

![MCP Inspector connected](../assets/inspector-connected.png)

## List tools

Click **List Tools** on the **Tools** tab:

![Tool list](../assets/inspector-tools-list.png)

Each recurring job appears as a tool named `Run_<job-id>` (dots and hyphens replaced with underscores). Manifest-only tools use `Run_<TypeName>_<MethodName>`.

## Run a tool with parameters

Select `Run_send-message_text`, fill in `text`, then **Run Tool**:

![send-message filled](../assets/inspector-send-message-filled.png)

The job is enqueued immediately:

![send-message result](../assets/inspector-send-message-result.png)

## Defaults and optional parameters

`Run_report_generate` shows how `year` is required while `format` (has a C# default) and `since` (nullable) are optional:

![report.generate params](../assets/inspector-report-generate-params.png)

## Tool-call sequence

```mermaid
sequenceDiagram
    participant Client as MCP Inspector
    participant MCP as /mcp
    participant Catalog as JobCatalog
    participant Binder as JobArgumentBinder
    participant Hangfire as Hangfire server

    Client->>MCP: ListTools
    MCP->>Catalog: enumerate descriptors
    Catalog-->>MCP: tools + JSON Schema
    MCP-->>Client: tools

    Client->>MCP: CallTool(Run_report_generate, {year: 2026})
    MCP->>Binder: bind JSON → object[]
    Binder-->>MCP: typed args
    MCP->>Hangfire: Enqueue(Job)
    Hangfire-->>MCP: jobId
    MCP-->>Client: { jobId }
```
