# ToolBridge Agent Notes

## Project Shape
- Current desktop app: `src/MusicShell.Wpf`, a Windows-only WPF app targeting `net8.0-windows`.
- Server target: Ubuntu 22.04 Docker host, 2-4 GB RAM, Docker bridge networking, HTTP access under `/api`.
- Linux containers must use a separate API/service project. Do not try to run the WPF app inside Linux Docker.

## Build Commands
- Desktop build: `.\build.ps1 -Configuration Release`
- Source validation: `.\validate.ps1 -Configuration Release -SkipExternalTools`
- API build: `dotnet build .\src\ToolBridge.Api\ToolBridge.Api.csproj -c Release`
- API container: `docker compose up --build -d`

## Docker/API Guidance
- Keep the API lightweight and dependency-minimal for 2-4 GB servers.
- Bind Kestrel to `0.0.0.0:8080` in containers.
- Keep all externally consumed HTTP routes under `/api`.
- Prefer bridge networking in compose unless the user explicitly requests host networking.
- Use Ubuntu 22.04 based .NET images (`*-jammy`) for Docker targets.

## Editing Guidance
- Preserve user changes in the WPF project; the worktree may be dirty.
- Avoid unrelated UI refactors when touching API or deployment files.
- Add shared abstractions only when both WPF and API need them.
