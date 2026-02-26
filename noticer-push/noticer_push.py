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
                      transient: bool = False,
                      slim: bool = False,
                      host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> None:
    data: dict = {"title": title, "message": message, "level": level}
    if source:
        data["source"] = source
    if transient:
        data["transient"] = True
    if slim:
        data["slim"] = True
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
    parser.add_argument(
        "--transient",
        action="store_true",
        help="Mark notification as transient — removed when any new message arrives in the same group",
    )
    parser.add_argument(
        "--slim",
        action="store_true",
        help="Render as a compact single-row card",
    )
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", "-p", type=int, default=DEFAULT_PORT, help=f"Port (default: {DEFAULT_PORT})")

    args = parser.parse_args()

    if args.hook:
        hook = json.load(sys.stdin)
        event = hook.get("hook_event_name", "Event")
        cwd = hook.get("cwd", "")
        args.source = os.path.basename(cwd) if cwd else args.source

        if event == "PreToolUse":
            tool = hook.get("tool_name", "tool")
            tool_input = hook.get("tool_input") or {}
            command = tool_input.get("command") or tool_input.get("path") or str(tool_input)
            title = tool
            message = command
            level = "info"
            args.transient = True
            args.slim = True
        elif event == "PermissionRequest":
            tool = hook.get("tool_name", "tool")
            tool_input = hook.get("tool_input") or {}
            detail = tool_input.get("command") or tool_input.get("path") or str(tool_input)
            title = f"Permission: {tool}"
            message = detail
            level = "warn"
            args.transient = True
        else:
            title = hook.get("title") or event
            message = hook.get("message") or ("Done" if event == "Stop" else event)
            level = args.level
            if event == "Stop":
                args.transient = True
                args.slim = True
    else:
        if not args.title or not args.message:
            parser.error("title and message are required unless --hook is used")
        title = args.title
        message = args.message
        level = args.level

    send_notification(title, message, level, args.source, args.transient, args.slim, args.host, args.port)
    print(f"[{level.upper()}] {title}: {message}")


if __name__ == "__main__":
    main()
