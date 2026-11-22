"""Минимальный клиент Telegram Bot API на стандартной библиотеке.

Токен читается из переменной окружения TELEGRAM_BOT_TOKEN или из локального
файла «Текстовый документ.txt» в корне репозитория (файл не публикуется).
Запуск: python tglib.py <method> [key=value ...] — значения парсятся как JSON.
"""

import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOKEN_FILE = ROOT / "Текстовый документ.txt"


def token() -> str:
    env = os.environ.get("TELEGRAM_BOT_TOKEN")
    if env:
        return env.strip()
    return TOKEN_FILE.read_text(encoding="utf-8-sig").strip()


def _request(method: str, data: bytes, headers: dict, retries: int = 4):
    url = f"https://api.telegram.org/bot{token()}/{method}"
    for attempt in range(retries + 1):
        req = urllib.request.Request(url, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                body = json.load(resp)
        except urllib.error.HTTPError as exc:
            body = json.load(exc)
        if body.get("ok"):
            return body["result"]
        if body.get("error_code") == 429 and attempt < retries:
            time.sleep(body.get("parameters", {}).get("retry_after", 3) + 1)
            continue
        raise RuntimeError(f"{method} failed: {body}")
    raise RuntimeError(f"{method}: retries exhausted")


def api(method: str, **params):
    payload = {k: v for k, v in params.items() if v is not None}
    return _request(
        method,
        json.dumps(payload).encode("utf-8"),
        {"Content-Type": "application/json"},
    )


def upload(method: str, file_field: str, file_path, **params):
    path = Path(file_path)
    boundary = uuid.uuid4().hex
    parts = []
    for key, value in params.items():
        if value is None:
            continue
        text = value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"{key}\"\r\n\r\n{text}\r\n".encode("utf-8")
        )
    ctype = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    parts.append(
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"{file_field}\"; filename=\"{path.name}\"\r\n"
        f"Content-Type: {ctype}\r\n\r\n".encode("utf-8")
    )
    parts.append(path.read_bytes())
    parts.append(f"\r\n--{boundary}--\r\n".encode("utf-8"))
    return _request(
        method,
        b"".join(parts),
        {"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )


def main() -> None:
    if len(sys.argv) < 2:
        print("usage: tglib.py <method> [key=value ...]")
        raise SystemExit(2)
    method = sys.argv[1]
    params = {}
    for arg in sys.argv[2:]:
        key, _, raw = arg.partition("=")
        try:
            params[key] = json.loads(raw)
        except json.JSONDecodeError:
            params[key] = raw
    print(json.dumps(api(method, **params), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()