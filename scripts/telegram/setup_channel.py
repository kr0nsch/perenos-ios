"""Первичная настройка официального канала «Перенос» и профиля бота.

Запуск:
  python setup_channel.py --discover          # найти канал и показать его данные
  python setup_channel.py --chat @username    # настроить канал по известной ссылке
  python setup_channel.py                     # настройка канала из channel_state.json

Состояние (chat_id, username) сохраняется в channel_state.json рядом со скриптом.
"""

import argparse
import json
import sys
from pathlib import Path

from tglib import ROOT, api, upload

HERE = Path(__file__).resolve().parent
STATE = HERE / "channel_state.json"

CANDIDATES = [
    "perenos_ios", "perenos", "perenos_channel", "perenos_app",
    "perenos_news", "perenosios", "perenos_official", "perenos_ru",
]

DESCRIPTION = (
    "Официальный канал «Переноса» — бесплатной программы для переноса приложений "
    "на новый iPhone и восстановления покупок App Store. Релизы, changelog, гайды. "
    "Windows и macOS, без джейлбрейка. GitHub: github.com/kr0nsch/perenos-ios"
)


def load_state() -> dict:
    if STATE.exists():
        return json.loads(STATE.read_text(encoding="utf-8"))
    return {}


def save_state(state: dict) -> None:
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def discover() -> dict | None:
    for upd in api("getUpdates"):
        for key in ("my_chat_member", "channel_post", "message"):
            chat = (upd.get(key) or {}).get("chat") or {}
            if chat.get("type") == "channel":
                return chat
    for name in CANDIDATES:
        try:
            chat = api("getChat", chat_id=f"@{name}")
        except RuntimeError:
            continue
        if chat.get("type") == "channel":
            return chat
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--discover", action="store_true")
    parser.add_argument("--chat")
    parser.add_argument("--title")
    args = parser.parse_args()

    state = load_state()
    if args.chat:
        chat = api("getChat", chat_id=args.chat)
        state.update(chat_id=chat["id"], username=chat.get("username"), title=chat.get("title"))
        save_state(state)
    elif not state.get("chat_id"):
        chat = discover()
        if not chat:
            print("CHANNEL_NOT_FOUND: укажите ссылку через --chat @username")
            return
        state.update(chat_id=chat["id"], username=chat.get("username"), title=chat.get("title"))
        save_state(state)

    chat = api("getChat", chat_id=state["chat_id"])
    print(json.dumps({k: chat.get(k) for k in ("id", "title", "username", "description", "photo")},
                     ensure_ascii=False, indent=2))
    if args.discover:
        return

    if len(DESCRIPTION) > 255:
        raise SystemExit(f"description too long: {len(DESCRIPTION)}")
    print("setChatDescription:", api("setChatDescription", chat_id=state["chat_id"], description=DESCRIPTION))
    print("setChatPhoto:", upload("setChatPhoto", "photo", ROOT / "assets" / "logo-1024.png", chat_id=state["chat_id"]))
    if args.title:
        print("setChatTitle:", api("setChatTitle", chat_id=state["chat_id"], title=args.title))

    channel_url = f"https://t.me/{state['username']}" if state.get("username") else None
    bot_text = (
        "Бот обратной связи проекта «Перенос» — программы переноса приложений на новый iPhone "
        "и восстановления покупок App Store.\n\nПринимает сообщения об ошибках, идеи и отзывы "
        "и передаёт их разработчику. Нажмите «Начать», чтобы открыть меню с кнопками."
    )
    if channel_url:
        bot_text += f"\n\nОфициальный канал: {channel_url}"
    print("setMyName:", api("setMyName", name="Перенос"))
    print("setMyDescription:", api("setMyDescription", description=bot_text))
    print("setMyShortDescription:", api("setMyShortDescription",
                                        short_description="Баги, идеи и отзывы о программе «Перенос» — разработчику."))
    print("setMyCommands:", api("setMyCommands", commands=[
        {"command": "start", "description": "Главное меню с кнопками"},
        {"command": "help", "description": "Как пользоваться ботом"},
        {"command": "version", "description": "Актуальная версия «Переноса»"},
        {"command": "channel", "description": "Официальный канал проекта"},
    ]))
    print("DONE")


if __name__ == "__main__":
    sys.exit(main())