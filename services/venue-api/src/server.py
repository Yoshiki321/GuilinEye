# -*- coding: utf-8 -*-
"""桂林之眼 venue-api：零依赖本地服务（http.server + sqlite3）。

运行：
    python src/server.py --port 8080
    python src/server.py --port 8080 --db data/venue.db

接口：
    GET  /api/v1/health
    GET  /api/v1/control/state          # Unity 轮询，扁平结构
    POST /api/v1/control/{cmd}          # pause|resume|segment|volume
    GET  /api/v1/b04/questions?age=...
    POST /api/v1/b04/answers
    GET  /api/v1/b04/wall-events?after=
    POST /api/v1/b04/certificates
    POST /api/v1/c03/blessings
    GET  /api/v1/c03/blessings/{id}
    GET  /api/v1/c03/state
    POST /api/v1/c04/works
    GET  /api/v1/c04/wall
    GET  /api/v1/c05/catalog
    POST /api/v1/c05/orders
    GET  /api/v1/c05/orders/{code}
"""
import argparse
import hashlib
import hmac
import json
import mimetypes
import os
import secrets
import sqlite3
import threading
import time
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse, parse_qs

REPO = Path(__file__).resolve().parents[3]
WEB = REPO / "web" / "h5"
DATA = Path(__file__).resolve().parents[1] / "data"
DB_PATH = DATA / "venue.db"
DB_LOCK = threading.RLock()
OPS_TOKEN = os.environ.get("VENUE_OPS_TOKEN", "").strip()
CORS_ORIGIN = os.environ.get("VENUE_CORS_ORIGIN", "http://127.0.0.1:8080").strip() or "http://127.0.0.1:8080"
MAX_BODY = 2 * 1024 * 1024
C03_LEASE_SECONDS = 40
C03_CONCURRENT = 12


QUESTION_SEED = [
    ("静夜思", "床前明月光，下一句是？", ["疑是地上霜", "低头思故乡", "举头望明月", "白发三千丈"], 0, "《静夜思》第二句为“疑是地上霜”。", "小学低年级", "疑是地上霜"),
    ("登鹳雀楼", "白日依山尽，下一句是？", ["黄河入海流", "更上一层楼", "欲穷千里目", "春风吹又生"], 0, "《登鹳雀楼》首联为“白日依山尽，黄河入海流”。", "小学低年级", "黄河入海流"),
    ("登鹳雀楼", "欲穷千里目，下一句是？", ["更上一层楼", "黄河入海流", "白日依山尽", "一览众山小"], 0, "“欲穷千里目，更上一层楼”劝人登高望远。", "小学高年级", "更上一层楼"),
    ("春晓", "春眠不觉晓，下一句是？", ["处处闻啼鸟", "花落知多少", "夜来风雨声", "春风吹又生"], 0, "《春晓》首句之后是“处处闻啼鸟”。", "小学低年级", "处处闻啼鸟"),
    ("春晓", "夜来风雨声，下一句是？", ["花落知多少", "处处闻啼鸟", "春眠不觉晓", "润物细无声"], 0, "“夜来风雨声，花落知多少”写惜春。", "小学高年级", "花落知多少"),
    ("江雪", "千山鸟飞绝，下一句是？", ["万径人踪灭", "孤舟蓑笠翁", "独钓寒江雪", "空山不见人"], 0, "《江雪》以“千山鸟飞绝，万径人踪灭”起句。", "初中", "万径人踪灭"),
    ("江雪", "孤舟蓑笠翁，下一句是？", ["独钓寒江雪", "万径人踪灭", "千山鸟飞绝", "风雪夜归人"], 0, "“孤舟蓑笠翁，独钓寒江雪”是柳宗元的名句。", "初中", "独钓寒江雪"),
    ("泊船瓜洲", "京口瓜洲一水间，下一句是？", ["钟山只隔数重山", "春风又绿江南岸", "明月何时照我还", "两岸猿声啼不住"], 0, "王安石《泊船瓜洲》首联写地理之近。", "初中", "钟山只隔数重山"),
    ("梅花", "遥知不是雪，下一句是？", ["为有暗香来", "凌寒独自开", "墙角数枝梅", "只留清气满乾坤"], 0, "王安石《梅花》以暗香点出梅。", "小学高年级", "为有暗香来"),
    ("晓出净慈寺送林子方", "接天莲叶无穷碧，下一句是？", ["映日荷花别样红", "毕竟西湖六月中", "风光不与四时同", "水光潋滟晴方好"], 0, "杨万里写六月西湖，以“映日荷花别样红”收束。", "成人", "映日荷花别样红"),
    ("送桂州严大夫", "江作青罗带，下一句是？", ["山如碧玉篸", "水如碧玉流", "山似翠屏开", "峰峦如聚"], 0, "韩愈写桂林山水：“江作青罗带，山如碧玉篸”。", "成人", "山如碧玉篸"),
    ("送桂州严大夫", "此诗写的城市是？", ["桂林", "杭州", "扬州", "柳州"], 0, "韩愈《送桂州严大夫》中的桂州即今桂林。", "成人", "桂林"),
]


def now_iso():
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def promote_c03(conn):
    """把 40s 租约到期的 displaying 收为 done，并按 position 递补 waiting -> queued。"""
    now = now_iso()
    conn.execute("UPDATE blessings SET status='done', lease_until=NULL WHERE status='displaying' AND lease_until IS NOT NULL AND lease_until < ?", (now,))
    active = conn.execute("SELECT COUNT(*) AS n FROM blessings WHERE status IN ('queued','displaying')").fetchone()["n"]
    free = max(0, C03_CONCURRENT - active)
    if free <= 0:
        return active
    rows = conn.execute(
        "SELECT id FROM blessings WHERE status='waiting' ORDER BY position, created_at LIMIT ?",
        (free,),
    ).fetchall()
    for row in rows:
        conn.execute("UPDATE blessings SET status='queued' WHERE id=? AND status='waiting'", (row["id"],))
    return active + len(rows)


def c03_queue_position(conn, row):
    if row["status"] == "queued":
        return 0
    if row["status"] == "displaying":
        return 0
    ahead = conn.execute(
        "SELECT COUNT(*) AS n FROM blessings WHERE status IN ('queued','displaying') AND position < ?",
        (row["position"],),
    ).fetchone()["n"]
    return ahead + 1


def connect(db_path):
    conn = sqlite3.connect(str(db_path), timeout=10)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL")
    return conn


def init_db(db_path):
    db_path.parent.mkdir(parents=True, exist_ok=True)
    conn = connect(db_path)
    conn.executescript(
        """
        CREATE TABLE IF NOT EXISTS questions(
            id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT, prompt TEXT, options TEXT,
            answer INTEGER, explanation TEXT, age TEXT, drop_char TEXT, review_status TEXT DEFAULT 'draft');
        CREATE TABLE IF NOT EXISTS sessions(
            id TEXT PRIMARY KEY, age TEXT, correct INTEGER DEFAULT 0, wrong INTEGER DEFAULT 0,
            level INTEGER DEFAULT 1, streak_right INTEGER DEFAULT 0, streak_wrong INTEGER DEFAULT 0,
            created_at TEXT);
        CREATE TABLE IF NOT EXISTS wall_events(
            id INTEGER PRIMARY KEY AUTOINCREMENT, char TEXT, session_id TEXT, created_at TEXT);
        CREATE TABLE IF NOT EXISTS blessings(
            id TEXT PRIMARY KEY, text TEXT, status TEXT, created_at TEXT, position INTEGER,
            lease_until TEXT);
        CREATE TABLE IF NOT EXISTS works(
            id TEXT PRIMARY KEY, nickname TEXT, paper TEXT, image TEXT, slot INTEGER,
            status TEXT, created_at TEXT);
        CREATE TABLE IF NOT EXISTS orders(
            id TEXT PRIMARY KEY, code TEXT UNIQUE, sku_id TEXT, pattern TEXT, color TEXT, size TEXT,
            status TEXT, created_at TEXT);
        CREATE TABLE IF NOT EXISTS certificates(
            token TEXT PRIMARY KEY, session_id TEXT, paid INTEGER DEFAULT 0, created_at TEXT);
        CREATE TABLE IF NOT EXISTS idem(key TEXT PRIMARY KEY, response TEXT, payload_hash TEXT, created_at TEXT);
        CREATE TABLE IF NOT EXISTS control(
            id INTEGER PRIMARY KEY CHECK (id=1), paused INTEGER DEFAULT 0,
            segment TEXT DEFAULT '', volume REAL DEFAULT 1.0, command TEXT DEFAULT '');
        """
    )
    count = conn.execute("SELECT COUNT(*) AS n FROM questions").fetchone()["n"]
    if count == 0:
        for q in QUESTION_SEED:
            conn.execute(
                "INSERT INTO questions(title,prompt,options,answer,explanation,age,drop_char,review_status) VALUES(?,?,?,?,?,?,?,?)",
                (q[0], q[1], json.dumps(q[2], ensure_ascii=False), q[3], q[4], q[5], q[6], "draft"),
            )
    conn.execute("INSERT OR IGNORE INTO control(id) VALUES(1)")
    cols = {row[1] for row in conn.execute("PRAGMA table_info(blessings)").fetchall()}
    if "lease_until" not in cols:
        conn.execute("ALTER TABLE blessings ADD COLUMN lease_until TEXT")
    cols = {row[1] for row in conn.execute("PRAGMA table_info(orders)").fetchall()}
    if "sku_id" not in cols:
        conn.execute("ALTER TABLE orders ADD COLUMN sku_id TEXT")
    cols = {row[1] for row in conn.execute("PRAGMA table_info(idem)").fetchall()}
    if "payload_hash" not in cols:
        conn.execute("ALTER TABLE idem ADD COLUMN payload_hash TEXT")
    conn.commit()
    conn.close()


class ApiError(Exception):
    def __init__(self, code, message, status=400):
        self.code, self.message, self.status = code, message, status


class Handler(BaseHTTPRequestHandler):
    server_version = "GuilinEyeVenueAPI/1.1"
    db_path = DB_PATH
    verbose = True

    # ---------- helpers ----------
    def _send(self, status, payload, content_type="application/json"):
        body = payload if isinstance(payload, bytes) else json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", CORS_ORIGIN)
        self.send_header("Access-Control-Allow-Headers", "Content-Type,Idempotency-Key,X-Ops-Token")
        self.send_header("Access-Control-Allow-Methods", "GET,POST,OPTIONS")
        self.end_headers()
        try:
            self.wfile.write(body)
        except BrokenPipeError:
            pass

    def ok(self, data, request_id=None):
        self._send(200, {"ok": True, "data": data, "requestId": request_id or secrets.token_hex(8)})

    def fail(self, code, message, status=400):
        self._send(status, {"ok": False, "error": {"code": code, "message": message}, "requestId": secrets.token_hex(8)})

    def _ops_allowed(self):
        if OPS_TOKEN:
            got = self.headers.get("X-Ops-Token", "")
            return hmac.compare_digest(got, OPS_TOKEN)
        return self.client_address[0] in ("127.0.0.1", "::1")

    def body(self):
        length = int(self.headers.get("Content-Length", "0") or 0)
        if length <= 0:
            return {}
        if length > MAX_BODY:
            raise ApiError("body_too_large", "请求体超过 2 MB", 413)
        raw = self.rfile.read(length)
        try:
            return json.loads(raw.decode("utf-8"))
        except Exception:
            raise ApiError("bad_json", "请求体不是合法 JSON")

    def idem(self, key, handler, payload=None):
        if not key:
            return handler()
        ph = hashlib.sha256(json.dumps(payload, ensure_ascii=False, sort_keys=True).encode("utf-8")).hexdigest()
        with DB_LOCK:
            conn = connect(self.db_path)
            row = conn.execute("SELECT response,payload_hash FROM idem WHERE key=?", (key,)).fetchone()
            conn.close()
            if row:
                if row["payload_hash"] and row["payload_hash"] != ph:
                    raise ApiError("idem_conflict", "同一 Idempotency-Key 携带了不同请求体", 409)
                return json.loads(row["response"])
            result = handler()
            conn = connect(self.db_path)
            conn.execute("INSERT OR REPLACE INTO idem(key,response,payload_hash,created_at) VALUES(?,?,?,?)",
                         (key, json.dumps(result, ensure_ascii=False), ph, now_iso()))
            conn.commit()
            conn.close()
            return result

    # ---------- routing ----------
    def do_OPTIONS(self):
        self._send(204, b"")

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        qs = parse_qs(parsed.query)
        try:
            if path.startswith("/api/v1/"):
                return self.route_get(path[len("/api/v1/"):], qs)
            return self.static(path)
        except ApiError as e:
            return self.fail(e.code, e.message, e.status)
        except Exception as e:
            if self.verbose:
                print("ERR", path, repr(e))
            return self.fail("server_error", "服务器内部错误", 500)

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path
        try:
            if not path.startswith("/api/v1/"):
                raise ApiError("not_found", "接口不存在", 404)
            return self.route_post(path[len("/api/v1/"):], self.body())
        except ApiError as e:
            return self.fail(e.code, e.message, e.status)
        except Exception as e:
            if self.verbose:
                print("ERR", path, repr(e))
            return self.fail("server_error", "服务器内部错误", 500)

    def route_get(self, path, qs):
        if path == "health":
            conn = connect(self.db_path)
            conn.execute("SELECT 1").fetchone()
            conn.close()
            return self.ok({"status": "ok", "version": "1.1", "time": now_iso(), "db": str(self.db_path)})
        if path == "control/state":
            return self.control_state()
        if path == "b04/questions":
            return self.b04_questions(qs)
        if path == "b04/wall-events":
            return self.b04_wall_events(qs)
        if path == "c03/state":
            return self.c03_state()
        if path == "c03/queue":
            return self.c03_queue()
        if path.startswith("c03/blessings/"):
            return self.c03_get(path.split("/")[-1])
        if path == "c04/wall":
            return self.c04_wall()
        if path == "c05/catalog":
            return self.c05_catalog()
        if path == "c05/orders":
            return self.c05_orders()
        if path.startswith("c05/orders/"):
            return self.c05_get(path.split("/")[-1])
        raise ApiError("not_found", "接口不存在", 404)

    def route_post(self, path, data):
        key = self.headers.get("Idempotency-Key")
        if path.startswith("control/"):
            if not self._ops_allowed():
                raise ApiError("unauthorized", "控幕接口需要 X-Ops-Token 或本机访问", 401)
            return self.ok(self.control_set(path.split("/")[-1], data))
        if path == "telemetry":
            return self.ok({"accepted": len(data.get("events", []) if isinstance(data, dict) else [])})
        if path == "ops/heartbeat":
            return self.ok({"status": "ok", "at": now_iso()})
        if path.startswith("c03/blessings/") and path.endswith("/ack"):
            if not self._ops_allowed():
                raise ApiError("unauthorized", "河灯上墙确认需要 X-Ops-Token 或本机访问", 401)
            return self.ok(self.c03_ack(path.split("/")[-2], data))
        if path == "b04/answers":
            return self.ok(self.idem(key, lambda: self.b04_answer(data), data))
        if path == "b04/certificates":
            return self.ok(self.idem(key, lambda: self.b04_certificate(data), data))
        if path == "c03/blessings":
            return self.ok(self.idem(key, lambda: self.c03_create(data), data))
        if path == "c04/works":
            return self.ok(self.idem(key, lambda: self.c04_create(data), data))
        if path == "c05/orders":
            return self.ok(self.idem(key, lambda: self.c05_create(data), data))
        raise ApiError("not_found", "接口不存在", 404)

    # ---------- control ----------
    def control_state(self):
        conn = connect(self.db_path)
        row = conn.execute("SELECT paused,segment,volume,command FROM control WHERE id=1").fetchone()
        conn.close()
        return self._send(200, {
            "paused": bool(row["paused"]), "segment": row["segment"] or "",
            "volume": row["volume"], "command": row["command"] or ""
        })

    def control_set(self, cmd, data):
        if cmd not in ("pause", "resume", "segment", "volume"):
            raise ApiError("bad_command", "只支持 pause/resume/segment/volume")
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                if cmd == "pause":
                    conn.execute("UPDATE control SET paused=1, command='pause' WHERE id=1")
                elif cmd == "resume":
                    conn.execute("UPDATE control SET paused=0, command='resume' WHERE id=1")
                elif cmd == "segment":
                    seg = (data or {}).get("segment") or (data or {}).get("arg") or ""
                    conn.execute("UPDATE control SET segment=?, command='segment' WHERE id=1", (seg,))
                else:
                    # OpsBridge 历史上发 volume，H5/外部调用发 value；两者都接受。
                    vol = float((data or {}).get("value", (data or {}).get("volume", 1.0)))
                    conn.execute("UPDATE control SET volume=?, command='volume' WHERE id=1", (max(0.0, min(1.0, vol)),))
                conn.commit()
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()
        return {"command": cmd}

    # ---------- b04 ----------
    def b04_questions(self, qs):
        age = (qs.get("age") or [""])[0]
        conn = connect(self.db_path)
        if age:
            rows = conn.execute("SELECT * FROM questions WHERE age=? OR age='成人' ORDER BY RANDOM() LIMIT 20", (age,)).fetchall()
        else:
            rows = conn.execute("SELECT * FROM questions ORDER BY RANDOM() LIMIT 20").fetchall()
        conn.close()
        items = [{
            "id": r["id"], "title": r["title"], "prompt": r["prompt"],
            "options": json.loads(r["options"]), "age": r["age"]
        } for r in rows]
        return self.ok({
            "items": items, "count": len(items),
            "bankStatus": "draft", "bankNote": "题库为 12 条演示种子，96 题正式题库待学科教师终审"
        })

    def b04_answer(self, data):
        sid = data.get("sessionId") or secrets.token_hex(8)
        qid = int(data.get("questionId", -1))
        answer = int(data.get("answer", -1))
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                q = conn.execute("SELECT * FROM questions WHERE id=?", (qid,)).fetchone()
                if not q:
                    conn.rollback()
                    raise ApiError("question_not_found", "题目不存在", 404)
                correct = answer == q["answer"]
                session = conn.execute("SELECT * FROM sessions WHERE id=?", (sid,)).fetchone()
                if not session:
                    conn.execute("INSERT INTO sessions(id,age,created_at) VALUES(?,?,?)", (sid, data.get("age", ""), now_iso()))
                    session = conn.execute("SELECT * FROM sessions WHERE id=?", (sid,)).fetchone()
                streak_r = session["streak_right"] + 1 if correct else 0
                streak_w = session["streak_wrong"] + 1 if not correct else 0
                level = session["level"]
                if streak_r >= 2: level = min(4, level + 1); streak_r = 0
                if streak_w >= 2: level = max(1, level - 1); streak_w = 0
                conn.execute("UPDATE sessions SET correct=correct+?, wrong=wrong+?, level=?, streak_right=?, streak_wrong=? WHERE id=?",
                             (1 if correct else 0, 0 if correct else 1, level, streak_r, streak_w, sid))
                drop = q["drop_char"] if correct else None
                if correct:
                    conn.execute("INSERT INTO wall_events(char,session_id,created_at) VALUES(?,?,?)", (drop, sid, now_iso()))
                conn.commit()
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()
        return {
            "correct": correct, "explanation": q["explanation"][:40],
            "streakRight": streak_r, "streakWrong": streak_w, "level": level,
            "dropChar": drop, "fallSeconds": 30, "residualMax": 12,
            "sessionId": sid
        }

    def b04_wall_events(self, qs):
        after = int((qs.get("after") or ["0"])[0])
        conn = connect(self.db_path)
        rows = conn.execute("SELECT id,char,created_at FROM wall_events WHERE id>? ORDER BY id LIMIT 50", (after,)).fetchall()
        conn.close()
        return self.ok({"items": [{"id": r["id"], "char": r["char"], "at": r["created_at"]} for r in rows]})

    def b04_certificate(self, data):
        sid = data.get("sessionId", "")
        conn = connect(self.db_path)
        s = conn.execute("SELECT * FROM sessions WHERE id=?", (sid,)).fetchone()
        if not s:
            conn.close()
            raise ApiError("session_not_found", "会话不存在", 404)
        if s["correct"] < 3:
            conn.close()
            raise ApiError("not_enough", "答对 3 题才可领取免费电子证书")
        token = secrets.token_hex(12)
        conn.execute("INSERT INTO certificates(token,session_id,paid,created_at) VALUES(?,?,0,?)", (token, sid, now_iso()))
        conn.commit()
        conn.close()
        return {"token": token, "type": "free_digital", "paid": False,
                "note": "19—29 元烫金证书由厅外打印点线下办理，接口不收款"}

    # ---------- c03 ----------
    def c03_create(self, data):
        text = (data.get("text") or "").strip()
        if not text or len(text) > 12:
            raise ApiError("bad_text", "愿字限 1—12 字")
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                promote_c03(conn)
                active = conn.execute("SELECT COUNT(*) AS n FROM blessings WHERE status IN ('queued','displaying')").fetchone()["n"]
                status = "queued" if active < C03_CONCURRENT else "waiting"
                count = conn.execute("SELECT COUNT(*) AS n FROM blessings").fetchone()["n"]
                bid = "bl_" + secrets.token_hex(8)
                conn.execute("INSERT INTO blessings(id,text,status,created_at,position,lease_until) VALUES(?,?,?,?,?,NULL)",
                             (bid, text, status, now_iso(), count + 1))
                conn.commit()
            finally:
                conn.close()
        return {"id": bid, "status": status,
                "queuePosition": 0 if status == "queued" else max(1, active - C03_CONCURRENT + 1),
                "concurrentLimit": C03_CONCURRENT}

    def c03_get(self, bid):
        position = 0
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                promote_c03(conn)
                r = conn.execute("SELECT * FROM blessings WHERE id=?", (bid,)).fetchone()
                if r and r["status"] not in ("queued", "displaying"):
                    position = c03_queue_position(conn, r)
                conn.commit()
            finally:
                conn.close()
        if not r:
            raise ApiError("not_found", "记录不存在", 404)
        active = r["status"] in ("queued", "displaying")
        return self.ok({"id": r["id"], "text": r["text"], "status": r["status"],
                        "queuePosition": 0 if active else position})

    def c03_state(self):
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                promote_c03(conn)
                active = conn.execute("SELECT COUNT(*) AS n FROM blessings WHERE status IN ('queued','displaying')").fetchone()["n"]
                waiting = conn.execute("SELECT COUNT(*) AS n FROM blessings WHERE status='waiting'").fetchone()["n"]
                total = conn.execute("SELECT COUNT(*) AS n FROM blessings").fetchone()["n"]
                conn.commit()
            finally:
                conn.close()
        return self.ok({"active": active, "limit": C03_CONCURRENT, "total": total, "waiting": waiting,
                        "wishMaxChars": 12, "presets": ["平安", "学业", "团圆", "顺遂"]})

    def c03_queue(self):
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                promote_c03(conn)
                rows = conn.execute(
                    "SELECT id,text,status,created_at,position FROM blessings WHERE status IN ('queued','displaying') ORDER BY position, created_at LIMIT ?",
                    (C03_CONCURRENT,),
                ).fetchall()
                waiting = conn.execute("SELECT COUNT(*) AS n FROM blessings WHERE status='waiting'").fetchone()["n"]
                conn.commit()
            finally:
                conn.close()
        return self.ok({"limit": C03_CONCURRENT, "waiting": waiting,
                        "items": [{"id": r["id"], "text": r["text"], "status": r["status"],
                                   "createdAt": r["created_at"]} for r in rows]})

    def c03_ack(self, bid, data):
        status = (data or {}).get("status", "displaying")
        if status not in ("displaying", "done"):
            raise ApiError("bad_status", "ack status 只支持 displaying/done")
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                row = conn.execute("SELECT * FROM blessings WHERE id=?", (bid,)).fetchone()
                if not row:
                    conn.rollback()
                    raise ApiError("not_found", "记录不存在", 404)
                if status == "displaying":
                    lease = (datetime.now(timezone.utc) + timedelta(seconds=C03_LEASE_SECONDS)).isoformat(timespec="seconds")
                    conn.execute("UPDATE blessings SET status='displaying', lease_until=? WHERE id=?", (lease, bid))
                else:
                    conn.execute("UPDATE blessings SET status='done', lease_until=NULL WHERE id=?", (bid,))
                promote_c03(conn)
                conn.commit()
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()
        return {"id": bid, "status": status, "leaseSeconds": C03_LEASE_SECONDS}

    # ---------- c04 ----------
    def c04_create(self, data):
        nickname = (data.get("nickname") or "游客")[:12]
        paper = data.get("paper", "自由")
        image = data.get("image", "")
        if not image or not str(image).startswith("data:image/"):
            raise ApiError("bad_image", "缺少画作数据或不是 data:image")
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                day = now_iso()[:10]  # 与 created_at 同为 UTC，避免本地 0—8 点日期错位
                today = conn.execute("SELECT COUNT(*) AS n FROM works WHERE created_at LIKE ?", (day + "%",)).fetchone()["n"]
                if today >= 500:
                    conn.rollback()
                    raise ApiError("daily_limit", "今日 500 幅已满，仅存本地不投墙")
                slot = today % 18
                wid = "wk_" + secrets.token_hex(8)
                conn.execute("INSERT INTO works(id,nickname,paper,image,slot,status,created_at) VALUES(?,?,?,?,?,?,?)",
                             (wid, nickname, paper, image, slot, "queued", now_iso()))
                conn.commit()
            except ApiError:
                raise
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()
        return {"id": wid, "slot": slot, "status": "queued", "dailyUsed": today + 1, "dailyMax": 500,
                "collapseEvery": 300, "collapseDur": 8}

    def c04_wall(self):
        conn = connect(self.db_path)
        rows = conn.execute("SELECT id,nickname,paper,slot,status,created_at FROM works ORDER BY created_at DESC, id DESC LIMIT 18").fetchall()
        conn.close()
        return self.ok({"cells": 18, "cols": 6, "rows": 3, "items": [dict(r) for r in rows]})

    # ---------- c05 ----------
    def c05_catalog(self):
        return self.ok({
            "items": [
                {"skuId": "tuanshan", "name": "团扇", "price": None, "status": "pending"},
                {"skuId": "silk", "name": "丝巾", "price": None, "status": "pending"},
                {"skuId": "postcard", "name": "明信片", "price": None, "status": "pending"},
                {"skuId": "ornament", "name": "摆件", "price": None, "status": "pending"}
            ],
            "patterns": ["tuanshan", "stone", "elephant", "river"],
            "colors": ["shilv", "zhusha"],
            "sizes": ["S", "M", "L"],
            "priceNote": "价格与 SKU 由馆方收银系统确认，接口不虚构金额"
        })

    def c05_create(self, data):
        with DB_LOCK:
            conn = connect(self.db_path)
            try:
                conn.execute("BEGIN IMMEDIATE")
                for _ in range(8):
                    code = str(100000 + secrets.randbelow(900000))
                    oid = "od_" + secrets.token_hex(8)
                    try:
                        conn.execute("INSERT INTO orders(id,code,sku_id,pattern,color,size,status,created_at) VALUES(?,?,?,?,?,?,?,?)",
                                     (oid, code, data.get("skuId", ""), data.get("pattern", "tuanshan"),
                                      data.get("color", "shilv"), data.get("size", "M"), "test_draft", now_iso()))
                        conn.commit()
                        break
                    except sqlite3.IntegrityError:
                        continue
                else:
                    conn.rollback()
                    raise ApiError("code_collision", "取货码生成冲突，请重试", 500)
            except ApiError:
                raise
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()
        return {"orderId": oid, "pickupCode": code, "skuId": data.get("skuId", ""), "status": "test_draft",
                "sealSeconds": 60, "note": "支付与生产由馆方收银/取件系统完成，本接口仅测试"}

    def c05_orders(self):
        conn = connect(self.db_path)
        rows = conn.execute("SELECT id,code,sku_id,status,created_at FROM orders ORDER BY created_at DESC, id DESC LIMIT 20").fetchall()
        conn.close()
        return self.ok({"items": [{"orderId": r["id"], "pickupCode": r["code"], "skuId": r["sku_id"] or "", "status": r["status"],
                                   "createdAt": r["created_at"]} for r in rows]})

    def c05_get(self, code):
        conn = connect(self.db_path)
        r = conn.execute("SELECT * FROM orders WHERE code=?", (code,)).fetchone()
        conn.close()
        if not r:
            raise ApiError("not_found", "取货码不存在", 404)
        return self.ok({"orderId": r["id"], "status": r["status"], "pickupCode": r["code"]})

    # ---------- static ----------
    def static(self, path):
        if path == "/":
            path = "/index.html"
        if path.endswith("/"):
            path += "index.html"
        target = (WEB / path.lstrip("/")).resolve()
        try:
            target.relative_to(WEB.resolve())
        except ValueError:
            return self._send(404, b"not found", "text/plain; charset=utf-8")
        if not target.is_file():
            return self._send(404, b"not found", "text/plain; charset=utf-8")
        ctype = mimetypes.guess_type(str(target))[0] or "application/octet-stream"
        if ctype.startswith("text/") or ctype in ("application/javascript", "application/json"):
            ctype += "; charset=utf-8"
        self._send(200, target.read_bytes(), ctype)

    def log_message(self, fmt, *args):
        if self.verbose:
            print("[%s] %s" % (self.log_date_time_string(), fmt % args))


def main():
    global DB_PATH
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8080)
    ap.add_argument("--db", default=str(DB_PATH))
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()
    DB_PATH = Path(args.db)
    init_db(DB_PATH)
    Handler.db_path = DB_PATH
    Handler.verbose = not args.quiet
    srv = ThreadingHTTPServer(("0.0.0.0", args.port), Handler)
    print("venue-api on http://127.0.0.1:%d  db=%s" % (args.port, DB_PATH))
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        srv.server_close()


if __name__ == "__main__":
    main()
