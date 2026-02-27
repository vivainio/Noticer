# Noticer

A lightweight Windows notification overlay app. Receives notifications over UDP and displays them as cards in a persistent, scrollable window.

## Features

- Transparent background — only the notification cards are visible
- Notifications grouped by **source** under collapsible headers
- Color-coded levels: `info`, `warn`, `error`, `success`
- Pin mode 📌 — borderless always-on-top overlay, normal resizable window otherwise
- Raises to foreground on new notification without stealing focus (when unpinned)
- Draggable from the toolbar or any empty area
- Tiny binary (~24 KB) — targets .NET Framework 4.8, pre-installed on Windows 10/11

## Requirements

Windows 10 or 11 (.NET Framework 4.8 is pre-installed — no separate runtime needed).

## Installation

Download `noticer.exe` from [Releases](https://github.com/vivainio/Noticer/releases) and run it.

## Sending notifications

Install the Python client with [uv](https://docs.astral.sh/uv/):

```
uv tool install "noticer-push @ git+https://github.com/vivainio/Noticer.git#subdirectory=noticer-push"
```

### Basic usage

```
noticer-push "Title" "Message"
noticer-push "Build failed" "Exit code 1" --level error
noticer-push "Deploy done" "v1.2.0 is live" --level success --source "CI"
```

Levels: `info` (default), `warn`, `error`, `success`

### All options

| Flag | Description |
|------|-------------|
| `--level`, `-l` | Notification level |
| `--source`, `-s` | Group notifications under a collapsible source header |
| `--slim` | Compact single-row card |
| `--transient` | Removed when any new message arrives in the same group |
| `--host` | Target host (default: `127.0.0.1`, auto-detected in WSL) |
| `--port`, `-p` | Target port (default: `49152`) |

### From Python

```python
from noticer_push import send_notification

send_notification("Job done", "Processed 1000 rows", level="success", source="ETL")
```

## Claude Code integration

Noticer integrates with [Claude Code](https://github.com/anthropics/claude-code) hooks to show live activity — tool calls, permission requests, and completion notifications.

Bash tool calls appear as slim transient cards showing the tool name, description, and command (e.g. `Bash: Build project: dotnet build`).

Install hooks globally:

```
noticer-push --claude-hooks global
```

Or for the current project only:

```
noticer-push --claude-hooks local
```

Uninstall:

```
noticer-push --claude-hooks uninstall
```

## Protocol

Notifications are sent as UTF-8 UDP packets with `key=value` lines:

```
title=Deploy failed
message=Pod crashed in prod
level=error
source=myapp
```

Optional fields: `source`, `transient=true`, `slim=true`

Any language that can send a UDP packet can send notifications to Noticer.

## Building from source

Requires [.NET SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project Noticer
```

## License

MIT
