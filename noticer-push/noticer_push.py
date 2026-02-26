#!/usr/bin/env python3
"""
noticer-push - Send notifications to the Noticer app.

Usage:
    noticer-push "Title" "Message"
    noticer-push "Title" "Message" --level warn
    noticer-push "Title" "Message" --level success --source "MyApp"
    noticer-push --hook --source "Claude Code"   # reads Claude Code hook JSON from stdin

Levels: info (default), warn, error, success
"""

import argparse
import json
import os
import socket
import sys


DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 49152


def send_notification(title: str, message: str, level: str = "info",
                      source: str | None = None,
                      host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> None:
    data: dict = {"title": title, "message": message, "level": level}
    if source:
        data["source"] = source
    payload = json.dumps(data).encode("utf-8")
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.sendto(payload, (host, port))


def main() -> None:
    parser = argparse.ArgumentParser(description="Send a notification to Noticer.")
    parser.add_argument("title", nargs="?", help="Notification title")
    parser.add_argument("message", nargs="?", help="Notification message")
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
    parser.add_argument(
        "--hook",
        action="store_true",
        help="Read Claude Code hook JSON from stdin and use its fields as title/message",
    )
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", "-p", type=int, default=DEFAULT_PORT, help=f"Port (default: {DEFAULT_PORT})")

    args = parser.parse_args()

    if args.hook:
        hook = json.load(sys.stdin)
        event = hook.get("hook_event_name", "Event")
        title = hook.get("title") or event
        cwd = hook.get("cwd", "")
        session = os.path.basename(cwd) if cwd else ""
        base_message = hook.get("message") or event
        message = f"{base_message} [{session}]" if session else base_message
        level = args.level
    else:
        if not args.title or not args.message:
            parser.error("title and message are required unless --hook is used")
        title = args.title
        message = args.message
        level = args.level

    send_notification(title, message, level, args.source, args.host, args.port)
    print(f"[{level.upper()}] {title}: {message}")


if __name__ == "__main__":
    main()
