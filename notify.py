#!/usr/bin/env python3
"""
notify.py - Send notifications to the Noticer app.

Usage:
    python notify.py "Title" "Message"
    python notify.py "Title" "Message" --level warn
    python notify.py "Title" "Message" --level error
    python notify.py "Title" "Message" --level success
    python notify.py --host 127.0.0.1 --port 5556 "Title" "Message"

Levels: info (default), warn, error, success
"""

import argparse
import json
import socket
import sys


DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5556


def send_notification(title: str, message: str, level: str = "info",
                      source: str | None = None,
                      host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> None:
    data: dict = {"title": title, "message": message, "level": level}
    if source:
        data["source"] = source
    payload = json.dumps(data)
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.sendall((payload + "\n").encode("utf-8"))


def main() -> None:
    parser = argparse.ArgumentParser(description="Send a notification to Noticer.")
    parser.add_argument("title", help="Notification title")
    parser.add_argument("message", help="Notification message")
    parser.add_argument(
        "--level", "-l",
        default="info",
        choices=["info", "warn", "warning", "error", "success"],
        help="Notification level (default: info)",
    )
    parser.add_argument(
        "--source", "-s",
        default=None,
        help="Source name — groups notifications under a collapsible header",
    )
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", "-p", type=int, default=DEFAULT_PORT, help=f"Port (default: {DEFAULT_PORT})")

    args = parser.parse_args()

    try:
        send_notification(args.title, args.message, args.level, args.source, args.host, args.port)
        print(f"[{args.level.upper()}] {args.title}: {args.message}")
    except ConnectionRefusedError:
        print(f"Error: Noticer is not running on {args.host}:{args.port}", file=sys.stderr)
        sys.exit(1)
    except TimeoutError:
        print(f"Error: Connection to {args.host}:{args.port} timed out", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
