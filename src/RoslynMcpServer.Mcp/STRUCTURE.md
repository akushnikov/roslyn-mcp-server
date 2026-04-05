# RoslynMcpServer.Mcp structure

This project is the MCP adapter layer.
Keep transport glue here and push application logic into `RoslynMcpServer.Application`.

## Folder layout

Use feature-first folders that mirror the roadmap and abstraction boundaries:

- `Workspace/`
- `Navigation/`
- `Analysis/`
- `Refactoring/`
- `Generation/`
- `Infrastructure/`

Within each feature, use:

- `Tools/` for MCP tool entry points
- `Mappers/` for MCP-to-application contract mapping when translation becomes non-trivial
- `Prompts/` only if a feature later needs MCP prompt adapters
- `Resources/` only for feature-local static assets

Keep cross-feature adapter composition in the project root only when it is truly shared, for example:

- `McpServerRegistrationExtensions.cs`

## Tool placement

Place tools by business capability, not by technical shape.

Examples:

- `workspace_context`, `load_solution`, `get_workspace_state` -> `Workspace/Tools/`
- `get_symbol_info`, `go_to_definition`, `find_references` -> `Navigation/Tools/`
- `get_diagnostics`, `analyze_data_flow`, `find_callers` -> `Analysis/Tools/`
- `rename_symbol`, `organize_usings`, `move_type_to_file` -> `Refactoring/Tools/`
- `convert_to_async`, `generate_constructor` -> `Generation/Tools/`
- `server_info`, `diagnose`, `get_file_overview` -> `Infrastructure/Tools/`

## Rules

- Do not keep tools in the project root.
- Keep each tool class focused on MCP input/output handling and delegation.
- Extract repeated translation code into feature-local `Mappers/` before creating generic shared helpers.
- Prefer namespaces that match folders, for example `RoslynMcpServer.Mcp.Navigation.Tools`.
