# roslyn-mcp-server

MCP server scaffold on .NET 9 with two transport modes and a layered architecture:

- `src/RoslynMcpServer.Host.Stdio` for local agent integrations such as Codex or Claude Code
- `src/RoslynMcpServer.Host.Http` for remote/client-server usage

Current state:

- official MCP C# SDK-based hosts
- layered projects for abstractions, application, infrastructure, MCP adapters, and hosts
- starter tools: `server_info`, `workspace_context`

## Run

Build:

```powershell
dotnet build
```

Run in `stdio` mode:

```powershell
dotnet run --project .\src\RoslynMcpServer.Host.Stdio\RoslynMcpServer.Host.Stdio.csproj
```

Run in `http` mode:

```powershell
dotnet run --project .\src\RoslynMcpServer.Host.Http\RoslynMcpServer.Host.Http.csproj -- --urls http://localhost:3001
```

## HTTP endpoints

- `GET /health`
- MCP endpoint at `/mcp`

## Workspace context strategy

- prefer explicit `solutionPath` when provided
- otherwise use client `roots` capability when supported
- otherwise fall back to server configuration once added
- otherwise return a controlled guidance message explaining what context is missing

## Next steps

- add solution discovery from workspace roots
- add Roslyn workspace loading
- add code navigation and diagnostics tools
- add resources/prompts support if needed by target clients
