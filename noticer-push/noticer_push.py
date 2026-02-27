#!/usr/bin/env python3
"""
noticer-push - Send notifications to the Noticer app.

Usage:
    noticer-push "Title" "Message"
    noticer-push "Title" "Message" --level warn
    noticer-push "Title" "Message" --level success --source "MyApp"
    noticer-push --claude-hook                    # reads Claude Code hook JSON from stdin

Levels: info (default), warn, error, success
"""

import argparse
import json
import os
import socket
import sys


def _default_host() -> str:
    if os.name == "nt" or "microsoft" not in os.uname().release.lower():
        return "127.0.0.1"
    try:
        with open("/proc/net/route") as f:
            for line in f:
                parts = line.split()
                if parts[1] == "00000000":  # default route
                    h = parts[2]
                    return f"{int(h[6:8],16)}.{int(h[4:6],16)}.{int(h[2:4],16)}.{int(h[0:2],16)}"
    except OSError:
        pass
    return "127.0.0.1"


DEFAULT_HOST = _default_host()
DEFAULT_PORT = 49152


CLAUDE_HOOKS = {
    "Notification": [{"matcher": "", "hooks": [{"type": "command", "command": "noticer-push --claude-hook"}]}],
    "PreToolUse": [{"matcher": "Bash", "hooks": [{"type": "command", "command": "noticer-push --claude-hook"}]}],
    "PermissionRequest": [{"matcher": "", "hooks": [{"type": "command", "command": "noticer-push --claude-hook"}]}],
    "Stop": [{"matcher": "", "hooks": [{"type": "command", "command": "noticer-push --claude-hook"}]}],
}


def _claude_hooks(mode: str) -> None:
    if mode == "local":
        settings_path = os.path.join(os.getcwd(), ".claude", "settings.json")
    else:
        settings_path = os.path.expanduser("~/.claude/settings.json")

    if os.path.exists(settings_path):
        with open(settings_path) as f:
            settings = json.load(f)
    else:
        os.makedirs(os.path.dirname(settings_path), exist_ok=True)
        settings = {}

    if mode == "uninstall":
        settings.pop("hooks", None)
        verb = "Uninstalled hooks from"
    else:
        settings["hooks"] = CLAUDE_HOOKS
        verb = "Installed hooks into"

    with open(settings_path, "w") as f:
        json.dump(settings, f, indent=2)
        f.write("\n")
    print(f"{verb} {settings_path}")


def send_notification(title: str, message: str, level: str = "info",
                      source: str | None = None,
                      transient: bool = False,
                      slim: bool = False,
                      host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> None:
    lines = [f"title={title}", f"message={message}", f"level={level}"]
    if source:
        lines.append(f"source={source}")
    if transient:
        lines.append("transient=true")
    if slim:
        lines.append("slim=true")
    payload = "\n".join(lines).encode("utf-8")
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
        "--claude-hook",
        action="store_true",
        dest="hook",
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
    parser.add_argument("--claude-hooks", choices=["global", "local", "uninstall"], help="Install or uninstall Claude Code hooks")
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"Host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", "-p", type=int, default=DEFAULT_PORT, help=f"Port (default: {DEFAULT_PORT})")

    args = parser.parse_args()

    if args.claude_hooks:
        _claude_hooks(args.claude_hooks)
        return

    if args.hook:
        hook = json.load(sys.stdin)
        event = hook.get("hook_event_name", "Event")
        cwd = hook.get("cwd", "")
        args.source = os.path.basename(cwd) if cwd else args.source

        if event == "PreToolUse":
            tool = hook.get("tool_name", "tool")
            tool_input = hook.get("tool_input") or {}
            command = tool_input.get("command") or tool_input.get("path") or str(tool_input)
            description = tool_input.get("description")
            title = f"{tool}: {description}: {command}" if description else f"{tool}: {command}"
            message = ""
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
        elif event == "Stop":
            title = "\u2713"
            message = ""
            level = "success"
            args.transient = True
        else:
            title = hook.get("title") or event
            message = hook.get("message") or event
            level = args.level
    else:
        if not args.title:
            parser.error("title is required unless --hook is used")
        title = args.title
        message = args.message or ""
        level = args.level

    send_notification(title, message, level, args.source, args.transient, args.slim, args.host, args.port)
    sys.stdout.buffer.write(f"[{level.upper()}] {title}: {message}\n".encode("utf-8"))


if __name__ == "__main__":
    main()
