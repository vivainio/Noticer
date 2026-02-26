#!/usr/bin/env python3
"""
notify.py - Send notifications to the Noticer app.

Usage:
    python notify.py "Title" "Message"
    python notify.py "Title" "Message" --level warn
    python notify.py "Title" "Message" --level error
    python notify.py "Title" "Message" --level success --source "MyApp"

Levels: info (default), warn, error, success
"""

import argparse
import json
import socket


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
    send_notification(args.title, args.message, args.level, args.source, args.host, args.port)
    print(f"[{args.level.upper()}] {args.title}: {args.message}")


if __name__ == "__main__":
    main()
