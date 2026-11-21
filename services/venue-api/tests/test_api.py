# -*- coding: utf-8 -*-
"""venue-api 集成测试：真实起服务、HTTP 走一遍主流程。"""
import json
import os
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.request
import urllib.error

HERE = os.path.dirname(os.path.abspath(__file__))
SERVER = os.path.join(HERE, "..", "src", "server.py")
PORT = 8099
BASE = "http://127.0.0.1:%d/api/v1" % PORT


def call(path, method="GET", body=None, headers=None):
    data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode("utf-8"))


class VenueApiTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="venue_api_")
        cls.db = os.path.join(cls.tmp, "t.db")
        cls.proc = subprocess.Popen(
            [sys.executable, SERVER, "--port", str(PORT), "--db", cls.db, "--quiet"],
            stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        for _ in range(50):
            try:
                call("/health")
                return
            except Exception:
                time.sleep(0.1)
        raise RuntimeError("server did not start: " + str(cls.proc.stderr.read() if cls.proc.stderr else b""))

    @classmethod
    def tearDownClass(cls):
        cls.proc.terminate()
        try:
            cls.proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            cls.proc.kill()
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def test_01_health(self):
        r = call("/health")
        self.assertTrue(r["ok"])
        self.assertEqual(r["data"]["status"], "ok")

    def test_02_control(self):
        call("/control/pause", "POST", {})
        s = call("/control/state")
        self.assertTrue(s["paused"])
        self.assertEqual(s["command"], "pause")
        call("/control/resume", "POST", {})
        self.assertFalse(call("/control/state")["paused"])
        call("/control/segment", "POST", {"segment": "D02"})
        self.assertEqual(call("/control/state")["segment"], "D02")
        # OpsBridge 发 volume，H5/外部调用发 value；两种字段都必须生效，且 0 表示静音。
        call("/control/volume", "POST", {"volume": 0.3})
        self.assertAlmostEqual(call("/control/state")["volume"], 0.3, places=3)
        call("/control/volume", "POST", {"value": 0.0})
        self.assertAlmostEqual(call("/control/state")["volume"], 0.0, places=3)

    def test_03_quiz_and_certificate(self):
        q = call("/b04/questions?age=%E5%B0%8F%E5%AD%A6%E4%BD%8E%E5%B9%B4%E7%BA%A7")["data"]
        self.assertGreaterEqual(q["count"], 1)
        self.assertEqual(q["bankStatus"], "draft")
        first = q["items"][0]
        self.assertNotIn("answer", first)

        sid = "sid_test_1"
        correct = 0
        for item in q["items"][:5]:
            # 服务端种子答案都是第 1 项（索引 0）
            r = call("/b04/answers", "POST", {"sessionId": sid, "questionId": item["id"], "answer": 0})["data"]
            if r["correct"]:
                correct += 1
        self.assertGreaterEqual(correct, 3)
        cert = call("/b04/certificates", "POST", {"sessionId": sid})["data"]
        self.assertFalse(cert["paid"])

        events = call("/b04/wall-events")["data"]["items"]
        self.assertGreaterEqual(len(events), 3)
        self.assertTrue(any(e["char"] for e in events))

    def test_04_blessing_limit(self):
        first = call("/c03/blessings", "POST", {"text": "平安"})["data"]
        self.assertIn(first["status"], ("queued", "waiting"))
        over = None
        for i in range(14):
            over = call("/c03/blessings", "POST", {"text": "愿%d" % i})["data"]
        self.assertEqual(over["status"], "waiting")
        bad = None
        try:
            call("/c03/blessings", "POST", {"text": "一二三四五六七八九十一二三"})
        except urllib.error.HTTPError as e:
            bad = e.code
        self.assertEqual(bad, 400)

    def test_05_works(self):
        r = call("/c04/works", "POST", {"nickname": "测试", "paper": "宣纸", "image": "data:image/png;base64,AAAA"})["data"]
        self.assertEqual(r["dailyUsed"], 1)
        self.assertEqual(call("/c04/wall")["data"]["cells"], 18)

    def test_06_orders(self):
        cat = call("/c05/catalog")["data"]
        self.assertEqual(len(cat["items"]), 4)
        self.assertIsNone(cat["items"][0]["price"])
        o = call("/c05/orders", "POST", {"skuId": "tuanshan", "pattern": "tuanshan", "color": "zhusha", "size": "M"})["data"]
        self.assertEqual(len(o["pickupCode"]), 6)
        self.assertEqual(o["skuId"], "tuanshan")
        self.assertEqual(o["sealSeconds"], 60)

    def test_07_idempotency(self):
        key = "k-" + str(time.time())
        a = call("/c05/orders", "POST", {"pattern": "stone", "color": "shilv", "size": "S"}, {"Idempotency-Key": key})
        b = call("/c05/orders", "POST", {"pattern": "stone", "color": "shilv", "size": "S"}, {"Idempotency-Key": key})
        self.assertEqual(a["data"]["orderId"], b["data"]["orderId"])
        conflict = None
        try:
            call("/c05/orders", "POST", {"pattern": "postcard", "color": "zhusha", "size": "L"}, {"Idempotency-Key": key})
        except urllib.error.HTTPError as e:
            conflict = e.code
        self.assertEqual(conflict, 409)

    def test_08_c03_twelfth_and_thirteenth(self):
        items = call("/c03/queue")["data"]["items"]
        self.assertGreaterEqual(len(items), 12)
        active = items[:12]
        for item in active:
            call("/c03/blessings/%s/ack" % item["id"], "POST", {"status": "displaying"})
        conn = sqlite3.connect(self.db)
        conn.execute("UPDATE blessings SET lease_until='2000-01-01T00:00:00+00:00' WHERE status='displaying'")
        conn.commit()
        conn.close()
        promoted = call("/c03/queue")["data"]
        self.assertEqual(len(promoted["items"]), 3)
        self.assertGreaterEqual(len(set(i["id"] for i in promoted["items"]) - set(i["id"] for i in active)), 1)


if __name__ == "__main__":
    unittest.main(verbosity=2)
