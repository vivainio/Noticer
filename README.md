# Noticer

A lightweight Windows notification viewer with a transparent, always-on-top panel. Send notifications from Python (or any TCP client) and see them grouped by source in a floating overlay.

![Noticer](https://github.com/vivainio/Noticer/raw/master/docs/screenshot.png)

## Features

- Transparent background — only the notification cards are visible
- Notifications grouped by **source** under collapsible headers
- Color-coded levels: `info`, `warn`, `error`, `success`
- Pin button to keep the window always on top
- Draggable from the toolbar or any card
- System tray icon with unread count

## Getting started

### Run the app

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project Noticer
```

### Send a notification

Install the CLI globally with [pipx](https://pipx.pypa.io) or [uv](https://docs.astral.sh/uv/):

```bash
pipx install ./noticer-push
# or
uv tool install ./noticer-push
```

Then use from anywhere:

```bash
noticer-push "Title" "Message"
noticer-push "Build failed" "Exit code 1" --level error
noticer-push "Deploy done" "v1.2.0 is live" --level success --source "CI"
```

#### All options

```
usage: noticer-push [-h] [--level {info,warn,warning,error,success}]
                    [--source SOURCE] [--host HOST] [--port PORT]
                    title message

positional arguments:
  title       Notification title
  message     Notification message

options:
  -l, --level   Severity level (default: info)
  -s, --source  Group notifications under this source name
  --host        Target host (default: 127.0.0.1)
  -p, --port    Target port (default: 49152)
```

#### From Python code

```python
from noticer_push import send_notification

send_notification("Job done", "Processed 1000 rows", level="success", source="ETL")
```

## Protocol

Newline-delimited JSON over TCP on `localhost:49152`:

```json
{"title": "Hello", "message": "World", "level": "info", "source": "MyApp"}
```

`source` is optional. Any TCP client can send notifications.

## License

MIT
