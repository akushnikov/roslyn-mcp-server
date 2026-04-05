# roslyn-mcp-server

Roslyn-backed MCP server for AI agents working with .NET solutions.

The project provides a sync-aware semantic layer over `MSBuildWorkspace`, so MCP clients can:

- load a solution explicitly
- inspect projects and source files
- navigate symbols and references
- query diagnostics and semantic relationships
- keep workspace state aligned with external file edits

The server targets .NET 9 and supports both `stdio` and HTTP transports.

## What it does

The current implementation is focused on read-only Roslyn tooling for agent workflows.

### Workspace and server tools

- `server_info`
  Returns basic server and runtime information.
- `workspace_context`
  Resolves workspace context from an explicit `solutionPath`.
- `diagnose`
  Checks environment readiness, MSBuild availability, and solution load health.
- `load_solution`
  Loads a `.sln`, `.slnx`, or `.csproj` into a live Roslyn workspace.
- `get_workspace_state`
  Returns the currently loaded workspace state and cached solution summaries.
- `get_project_structure`
  Returns projects and, optionally, source documents for a loaded solution.

### Navigation tools

- `get_document_outline`
  Returns a structural outline of a C# source file.
- `get_file_overview`
  Returns a compact overview of a source file, including its main declarations.
- `get_type_overview`
  Returns a compact overview of a resolved type.
- `get_type_members`
  Returns members declared on the resolved type.
- `search_symbols`
  Searches source declarations across the loaded solution.
- `get_symbol_info`
  Resolves semantic symbol information at a file position.
- `get_symbol_attributes`
  Returns attributes applied to the resolved symbol.
- `go_to_definition`
  Returns source definition locations for the resolved symbol.
- `get_method_signature`
  Returns the signature of a callable member at a file position.
- `get_method_source`
  Returns the source body of a callable member at a file position.
- `find_references`
  Finds source references for the resolved symbol.

### Analysis tools

- `get_diagnostics`
  Returns compiler diagnostics for a solution or a specific file.
- `validate_code`
  Returns a lightweight validity summary based on compiler diagnostics.
- `find_implementations`
  Finds implementations of interfaces, abstract types, and virtual members.
- `get_type_hierarchy`
  Returns base types, derived types, and interfaces for a resolved type.
- `find_callers`
  Finds caller locations for callable symbols.
- `get_outgoing_calls`
  Returns outgoing call edges for a callable member.
- `analyze_method`
  Returns method-level data flow, control flow, signature, and outgoing call information.
- `analyze_change_impact`
  Returns a best-effort impact summary for a symbol, including references and implementations.
- `check_type_compatibility`
  Checks whether two resolved types are compatible.

## Workspace synchronization

The server is designed for long-running agent sessions where files may change outside the Roslyn API.

It maintains a live workspace synchronization pipeline that:

- watches workspace files for external changes
- updates Roslyn documents incrementally when possible
- reloads the workspace when project structure changes require it
- ensures semantic queries use the current `Workspace.CurrentSolution`

In practice, this means tools such as `find_references`, `analyze_method`, and `get_diagnostics` can reflect edits made:

- by the user in an IDE
- by an agent through direct file edits
- by other tooling that changes files on disk

## Running the server

Build the solution:

```powershell
dotnet build .\roslyn-mcp-server.sln -v minimal
```

Run the `stdio` host:

```powershell
dotnet run --project .\src\RoslynMcpServer.Host.Stdio\RoslynMcpServer.Host.Stdio.csproj
```

Run the HTTP host:

```powershell
dotnet run --project .\src\RoslynMcpServer.Host.Http\RoslynMcpServer.Host.Http.csproj -- --urls http://localhost:3001
```

## HTTP host

The HTTP host exposes:

- `GET /health`
- MCP endpoint at `/mcp`

HTTP request and server logs are written:

- to the console
- to rolling log files under `src/RoslynMcpServer.Host.Http/logs`

## Using with MCP Inspector

When the HTTP host is running on `http://localhost:3001`, connect the Inspector to:

- `http://localhost:3001/mcp`

Typical manual flow:

1. Call `server_info`
2. Call `workspace_context`
3. Call `diagnose`
4. Call `load_solution`
5. Call `get_workspace_state`
6. Call `get_project_structure`
7. Use navigation or analysis tools

Example `load_solution` request:

```json
{
  "solutionPath": "D:\\Projects\\opensource\\roslyn-mcp-server\\roslyn-mcp-server.sln",
  "forceReload": false
}
```

## Current scope

The current server is intentionally read-only.

Refactoring, code generation, preview/apply workflows, and other mutating tools are planned after the query and synchronization layers are fully stabilized.

## Acknowledgements

This project was informed by ideas and implementation patterns from:

- [SharpLens MCP](https://github.com/pzalutski-pixel/sharplens-mcp)
- [JoshuaRamirez/RoslynMcpServer](https://github.com/JoshuaRamirez/RoslynMcpServer)

These references were especially useful for tool inventory planning, Roslyn-first workflows, and MCP server ergonomics.
