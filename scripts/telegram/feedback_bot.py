"""Бот обратной связи @perenos_ios_bot для официального канала «Перенос».

Запуск: python feedback_bot.py          (long polling, работает постоянно)
На GitHub Actions бот живёт окнами опроса: python feedback_bot.py --window 270
(workflow bot.yml, cron */5); состояние между запусками хранится в кэше Actions,
начальное состояние передаётся секретом FEEDBACK_STATE_SEED.
Проверка логики без сети: python feedback_bot.py --selftest

Как устроено:
• Все действия — инлайн-кнопки под одним «экранным» сообщением; экран редактируется
  на месте, лишние сообщения в личке не плодятся.
• Владелец (разработчик) подключается по секретной ссылке ?start=owner_<код> из лога:
  ему пересылаются баги, идеи и отзывы, он отвечает кнопкой «Ответить» прямо из Telegram.
• Сообщения, пришедшие до регистрации владельца, сохраняются и доставляются позже.
• Состояние — в feedback_state.json (не публикуется). Сброс владельца: удалить файл.
"""

import argparse
import html
import json
import os
import re
import secrets
import signal
import sys
import time
from datetime import datetime
from pathlib import Path

from tglib import ROOT, api

HERE = Path(__file__).resolve().parent
STATE = HERE / "feedback_state.json"
LOG = HERE / "feedback_bot.log"

REPO = "https://github.com/kr0nsch/perenos-ios"
SITE = "https://kr0nsch.github.io/perenos-ios/"
CHANNEL = "https://t.me/perenos_ios"
BOT_URL = "https://t.me/perenos_ios_bot"

CATEGORIES = {
    "bug": ("🐛 Баг", "🐛 <b>Сообщение об ошибке</b>\n\nОпишите, что пошло не так: версия «Переноса», "
            "Windows или macOS, модель iPhone и версия iOS, шаги и текст ошибки. Можно приложить скриншот.\n\n"
            "Отправьте описание следующим сообщением."),
    "idea": ("💡 Идея", "💡 <b>Предложение</b>\n\nРасскажите, чего не хватает в «Переносе» и как это "
             "должно работать.\n\nОтправьте идею следующим сообщением."),
    "talk": ("💬 Отзыв/вопрос", "💬 <b>Отзыв или вопрос</b>\n\nНапишите сообщение разработчику — "
             "ответ придёт сюда, в этот чат.\n\nОтправьте текст следующим сообщением."),
}


def log(text: str) -> None:
    line = f"{datetime.now():%Y-%m-%d %H:%M:%S} {text}"
    print(line, flush=True)
    with LOG.open("a", encoding="utf-8") as fh:
        fh.write(line + "\n")


def current_version() -> str:
    match = re.search(r"^## \[(\d+\.\d+\.\d+)\]", (ROOT / "CHANGELOG.md").read_text(encoding="utf-8"), re.M)
    return match.group(1) if match else "1.9.0"


def load_state() -> dict:
    if STATE.exists():
        state = json.loads(STATE.read_text(encoding="utf-8"))
    else:
        state = {"offset": 0, "owner": None, "users": {}, "pending": []}
    state.setdefault("owner_code", secrets.token_hex(4))
    return state


def save_state(state: dict) -> None:
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def user(state: dict, chat_id: int) -> dict:
    return state["users"].setdefault(str(chat_id), {})


def menu_keyboard() -> dict:
    return {"inline_keyboard": [
        [{"text": "🐛 Сообщить о баге", "callback_data": "cat:bug"},
         {"text": "💡 Предложить идею", "callback_data": "cat:idea"}],
        [{"text": "💬 Отзыв или вопрос", "callback_data": "cat:talk"}],
        [{"text": "📦 Актуальная версия", "callback_data": "ver"},
         {"text": "🔗 Канал и GitHub", "callback_data": "links"}],
    ]}


def back_keyboard() -> dict:
    return {"inline_keyboard": [[{"text": "⬅️ Назад в меню", "callback_data": "back"}]]}


def screen(state: dict, chat_id: int, text: str, keyboard: dict | None = None, fresh: bool = False) -> None:
    """Одно редактируемое сообщение-экран на пользователя: лишние сообщения не плодятся."""
    u = user(state, chat_id)
    if fresh and u.get("screen"):
        try:
            api("deleteMessage", chat_id=chat_id, message_id=u["screen"])
        except RuntimeError:
            pass
        u["screen"] = None
    if u.get("screen"):
        try:
            api("editMessageText", chat_id=chat_id, message_id=u["screen"], text=text,
                parse_mode="HTML", reply_markup=keyboard, link_preview_options={"is_disabled": True})
            return
        except RuntimeError:
            u["screen"] = None
    result = api("sendMessage", chat_id=chat_id, text=text, parse_mode="HTML",
                 reply_markup=keyboard, link_preview_options={"is_disabled": True})
    u["screen"] = result["message_id"]


def show_menu(state: dict, chat_id: int, fresh: bool = False) -> None:
    user(state, chat_id)["await"] = None
    screen(state, chat_id,
           "👋 <b>Перенос — обратная связь</b>\n\nВыберите, что отправить разработчику. "
           "Ответ придёт прямо в этот чат.",
           menu_keyboard(), fresh=fresh)


def show_version(state: dict, chat_id: int) -> None:
    screen(state, chat_id,
           f"📦 Актуальная версия «Переноса»: <b>{current_version()}</b>\n\n"
           "Установщик Windows и тестовые DMG для macOS — в релизах на GitHub.",
           {"inline_keyboard": [
               [{"text": "⬇️ Скачать", "url": f"{REPO}/releases/latest"}],
               [{"text": "⬅️ Назад в меню", "callback_data": "back"}],
           ]})


def show_links(state: dict, chat_id: int) -> None:
    screen(state, chat_id, "🔗 <b>Ссылки проекта</b>",
           {"inline_keyboard": [
               [{"text": "📣 Канал", "url": CHANNEL}],
               [{"text": "🌐 Сайт", "url": SITE}, {"text": "⭐ GitHub", "url": REPO}],
               [{"text": "⬅️ Назад в меню", "callback_data": "back"}],
           ]})


def sender_label(msg: dict) -> str:
    frm = msg.get("from") or {}
    name = html.escape(((frm.get("first_name") or "") + " " + (frm.get("last_name") or "")).strip())
    username = f"@{frm['username']}, " if frm.get("username") else ""
    return f"<b>{name}</b> ({username}id <code>{frm.get('id')}</code>)"


def send_to_owner(state: dict, owner: int, header: str, text: str, photo: str | None) -> int:
    if photo:
        result = api("sendPhoto", chat_id=owner, photo=photo,
                     caption=(header + html.escape(text))[:1024], parse_mode="HTML")
    else:
        result = api("sendMessage", chat_id=owner, text=header + html.escape(text),
                     parse_mode="HTML", link_preview_options={"is_disabled": True})
    return result["message_id"]


def forward_feedback(state: dict, msg: dict, category: str) -> None:
    chat_id = msg["chat"]["id"]
    label, _ = CATEGORIES[category]
    text = msg.get("text") or msg.get("caption") or ""
    photo = msg["photo"][-1]["file_id"] if msg.get("photo") else None
    header = f"{label} от {sender_label(msg)}\n🕒 {datetime.now():%d.%m.%Y %H:%M}\n\n"
    owner = state.get("owner")
    if owner:
        fwd_id = send_to_owner(state, owner, header, text, photo)
        api("editMessageReplyMarkup", chat_id=owner, message_id=fwd_id,
            reply_markup={"inline_keyboard": [[
                {"text": "✍️ Ответить", "callback_data": f"reply:{chat_id}:{fwd_id}"},
                {"text": "✅ Принято", "callback_data": f"ack:{fwd_id}"},
            ]]})
        log(f"feedback {category} from {chat_id} -> owner {owner}")
        thanks = "✅ Спасибо! Сообщение передано разработчику. Ответ придёт в этот чат."
    else:
        state["pending"].append({"header": header, "text": text, "photo": photo})
        log(f"feedback {category} from {chat_id} queued (no owner yet)")
        thanks = "✅ Спасибо! Сообщение сохранено и будет доставлено разработчику."
    user(state, chat_id)["await"] = None
    screen(state, chat_id, thanks,
           {"inline_keyboard": [[{"text": "📩 Отправить ещё", "callback_data": "back"}]]})


def flush_pending(state: dict, owner: int) -> None:
    for item in state.get("pending", []):
        send_to_owner(state, owner, item["header"], item["text"], item.get("photo"))
    state["pending"] = []


def process_update(state: dict, upd: dict) -> None:
    if "callback_query" in upd:
        cq = upd["callback_query"]
        api("answerCallbackQuery", callback_query_id=cq["id"])
        chat_id = cq["message"]["chat"]["id"]
        data = cq.get("data") or ""
        u = user(state, chat_id)
        if data.startswith("cat:"):
            category = data[4:]
            u["await"] = category
            screen(state, chat_id, CATEGORIES[category][1], back_keyboard())
        elif data in ("back", "menu"):
            show_menu(state, chat_id)
        elif data == "ver":
            show_version(state, chat_id)
        elif data == "links":
            show_links(state, chat_id)
        elif data.startswith("reply:") and chat_id == state.get("owner"):
            _, sender, fwd = data.split(":")
            u["reply_to"] = int(sender)
            u["reply_msg"] = int(fwd)
            try:
                api("editMessageText", chat_id=chat_id, message_id=int(fwd),
                    text=cq["message"].get("text", "") + "\n\n✍️ Напишите ответ следующим сообщением.",
                    parse_mode="HTML", link_preview_options={"is_disabled": True})
            except RuntimeError:
                pass
        elif data.startswith("ack:") and chat_id == state.get("owner"):
            try:
                api("editMessageText", chat_id=chat_id, message_id=int(data[4:]),
                    text=cq["message"].get("text", "") + "\n\n✅ Принято.",
                    parse_mode="HTML", link_preview_options={"is_disabled": True})
            except RuntimeError:
                pass
        return

    msg = upd.get("message")
    if not msg or msg.get("chat", {}).get("type") != "private":
        return
    chat_id = msg["chat"]["id"]
    u = user(state, chat_id)
    text = (msg.get("text") or "").strip()

    if text.startswith("/start"):
        payload = text.partition(" ")[2].strip()
        if payload == f"owner_{state['owner_code']}":
            if state.get("owner") in (None, chat_id):
                state["owner"] = chat_id
                flush_pending(state, chat_id)
                log(f"owner registered: {chat_id}")
                screen(state, chat_id,
                       "👑 <b>Вы подключены как разработчик «Переноса».</b>\n\n"
                       "Сюда будут приходить баги, идеи и отзывы из канала и бота. "
                       "Под сообщением пользователя — кнопки «Ответить» и «Принято».",
                       menu_keyboard(), fresh=True)
            else:
                screen(state, chat_id, "⛔ Владелец уже подключён с другого аккаунта.", fresh=True)
        elif chat_id == state.get("owner"):
            screen(state, chat_id,
                   "👑 <b>Вы — разработчик «Переноса».</b>\n\nСюда приходят баги, идеи и отзывы. "
                   "Под сообщением пользователя — кнопки «Ответить» и «Принято».",
                   menu_keyboard(), fresh=True)
        else:
            show_menu(state, chat_id, fresh=True)
        return
    if text.startswith("/help"):
        show_menu(state, chat_id)
        return
    if text.startswith("/version"):
        show_version(state, chat_id)
        return
    if text.startswith("/channel"):
        show_links(state, chat_id)
        return

    if chat_id == state.get("owner") and u.get("reply_to"):
        sender = u.pop("reply_to")
        u.pop("reply_msg", None)
        api("sendMessage", chat_id=sender,
            text="✉️ <b>Ответ разработчика «Переноса»:</b>\n\n" + html.escape(text),
            parse_mode="HTML")
        log(f"owner reply -> {sender}")
        screen(state, chat_id, "✅ Ответ отправлен пользователю.", menu_keyboard())
        return

    if u.get("await") and (text or msg.get("photo")):
        forward_feedback(state, msg, u["await"])
        return

    show_menu(state, chat_id)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--window", type=int, default=0,
                        help="окно опроса в секундах (0 = бесконечно, локальный режим)")
    parser.add_argument("--selftest", action="store_true")
    args = parser.parse_args()
    if args.selftest:
        selftest()
        return
    if not STATE.exists() and os.environ.get("FEEDBACK_STATE_SEED"):
        STATE.write_text(os.environ["FEEDBACK_STATE_SEED"], encoding="utf-8")
    state = load_state()
    state.setdefault("seen", [])
    if state.get("owner") and state.get("pending"):
        flush_pending(state, state["owner"])
        log(f"pending flushed to owner {state['owner']} on startup")
    save_state(state)
    log("polling started")
    if state.get("owner") is None and not args.window:
        log(f"owner link: {BOT_URL}?start=owner_{state['owner_code']}")

    def shutdown(signum, frame):
        save_state(state)
        log("terminated; state saved")
        sys.exit(0)

    signal.signal(signal.SIGTERM, shutdown)
    deadline = time.monotonic() + args.window if args.window else None
    while True:
        if deadline and time.monotonic() >= deadline:
            save_state(state)
            log("polling window finished; state saved")
            return
        try:
            remaining = int(deadline - time.monotonic()) if deadline else 30
            updates = api("getUpdates", offset=state["offset"] or None,
                          timeout=max(1, min(30, remaining)),
                          allowed_updates=["message", "callback_query"])
            for upd in updates:
                uid = upd["update_id"]
                state["offset"] = uid + 1
                if uid in state["seen"]:
                    save_state(state)
                    continue
                state["seen"].append(uid)
                state["seen"] = state["seen"][-500:]
                try:
                    process_update(state, upd)
                except Exception as exc:
                    log(f"handler error: {exc}")
                save_state(state)
        except KeyboardInterrupt:
            save_state(state)
            log("stopped")
            return
        except RuntimeError as exc:
            if "409" in str(exc):
                save_state(state)
                log("another poller is active; exiting")
                return
            log(f"poll error: {exc}")
            time.sleep(5)
        except Exception as exc:
            log(f"poll error: {exc}")
            time.sleep(5)


def selftest() -> None:
    calls = []
    counter = {"n": 100}

    def fake_api(method, **params):
        calls.append((method, params))
        if method in ("sendMessage", "sendPhoto"):
            counter["n"] += 1
            return {"message_id": counter["n"]}
        if method == "getUpdates":
            return []
        return True

    globals()["api"] = fake_api
    globals()["log"] = lambda text: None
    state = {"offset": 0, "owner": None, "owner_code": "testcode", "users": {}, "pending": []}

    def message(chat, text=None, **extra):
        return {"update_id": 1, "message": {"message_id": 1, "chat": {"id": chat, "type": "private"},
                                           "from": {"id": chat, "first_name": "Test"}, "text": text, **extra}}

    def callback(chat, data, mid=101, text="исходный текст"):
        return {"update_id": 2, "callback_query": {"id": "cq", "data": data,
                "message": {"message_id": mid, "chat": {"id": chat, "type": "private"}, "text": text}}}

    process_update(state, message(200, "/start feedback"))
    process_update(state, callback(200, "cat:talk", state["users"]["200"]["screen"]))
    process_update(state, message(200, "Отзыв до регистрации владельца"))
    assert len(state["pending"]) == 1, "pending queue before owner"

    process_update(state, message(100, "/start"))
    assert state["owner"] is None, "обычный /start не делает владельцем"
    process_update(state, message(100, "/start owner_testcode"))
    assert state["owner"] == 100, "owner registration"
    assert not state["pending"], "pending flushed"
    assert any(c[0] == "sendMessage" and c[1].get("chat_id") == 100
               and "Отзыв до регистрации" in c[1].get("text", "") for c in calls), "flush delivered"

    process_update(state, message(200, "/start feedback"))
    assert state["users"]["200"]["screen"], "user menu screen"
    process_update(state, callback(200, "cat:bug", state["users"]["200"]["screen"]))
    assert state["users"]["200"]["await"] == "bug", "category await"
    process_update(state, message(200, "Установка падает на iPhone 15"))
    forwards = [c for c in calls if c[0] == "sendMessage" and c[1].get("chat_id") == 100
                and "Установка падает" in c[1].get("text", "")]
    assert forwards, "feedback forwarded to owner"
    assert state["users"]["200"]["await"] is None, "await cleared"
    fwd_id = [c for c in calls if c[0] == "editMessageReplyMarkup"][-1][1]["message_id"]

    process_update(state, callback(100, f"reply:200:{fwd_id}", fwd_id))
    assert state["users"]["100"]["reply_to"] == 200, "owner reply mode"
    process_update(state, message(100, "Починю в следующем релизе"))
    assert any(c[0] == "sendMessage" and c[1].get("chat_id") == 200
               and "Починю" in c[1].get("text", "") for c in calls), "owner reply delivered"

    process_update(state, callback(100, f"ack:{fwd_id}", fwd_id))
    process_update(state, callback(200, "ver", state["users"]["200"]["screen"]))
    process_update(state, message(200, "/version"))
    print("SELFTEST PASS: меню, категории, очередь до владельца, пересылка, ответ, версия")


if __name__ == "__main__":
    main()