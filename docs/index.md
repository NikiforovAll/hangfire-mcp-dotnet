---
layout: home

hero:
  name: "Nall.Hangfire.Mcp"
  text: "Hangfire jobs as MCP tools"
  tagline: Remote MCP server for Hangfire — in-process, zero ceremony.
  actions:
    - theme: brand
      text: Get Started
      link: /getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/NikiforovAll/hangfire-mcp-dotnet

features:
  - title: In-process
    details: Runs inside the ASP.NET host that runs Hangfire.
  - title: Remote
    details: Streamable HTTP endpoint. Any MCP client (VS Code, Claude Desktop, custom agents) can connect.
  - title: Zero ceremony
    details: No attributes, no shim interfaces — discovery reads what you already register with Hangfire.
  - title: Schema from MethodInfo
    details: JSON Schema generated per method. Required vs. optional respects C# defaults and nullable annotations.
---
