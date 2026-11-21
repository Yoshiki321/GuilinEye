# -*- coding: utf-8 -*-
"""校验全馆参数源：结构、边界、时间轴、粒子、美术槽、硬件端点、单一参数源同步。"""
import hashlib, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CFG = os.path.join(ROOT, "config")
STREAM = os.path.join(ROOT, "unity", "GuilinEye", "Assets", "StreamingAssets", "config")
EXPECTED = ["A01", "A02", "A03", "A04", "B01", "B02", "B03", "B04",
            "C01", "C02", "C03", "C04", "C05", "D01", "D02", "D03", "D04", "D05"]

def load(name):
    with open(os.path.join(CFG, name), encoding="utf-8") as f:
        return json.load(f)

errors, warnings = [], []

def err(msg):
    errors.append(msg)

canvas = load("canvas.json")
quality = load("quality.json")
sensors = load("sensors.json")
schedule = load("schedule.json")
inst = load("installations.json")
art = load("art-slots.json")
hardware = load("hardware.json")

# --- canvas
canvas_map = {c["id"]: c for c in canvas["canvases"]}
channels = []
for c in canvas["canvases"]:
    for ch in c.get("channels", []):
        channels.append((c["id"], ch["id"]))
        r = ch["rect"]
        if r["x"] < 0 or r["y"] < 0 or r["x"] + r["w"] > c["width"] or r["y"] + r["h"] > c["height"]:
            err("通道越界 %s/%s %s" % (c["id"], ch["id"], r))
if len(channels) != 24:
    err("投影通道数应为 24，实际 %d" % len(channels))
if len({ch[1] for ch in channels}) != 24:
    err("通道 ID 不唯一")

# --- modules
mods = inst["modules"]
ids = [m["id"] for m in mods]
if ids != EXPECTED:
    err("模块 ID/顺序不符：%s" % ids)
if len(mods) != 18:
    err("模块数应为 18，实际 %d" % len(mods))

tiers = quality["tiers"]
show_ids = {s["id"] for s in schedule["shows"]}
zones = {z["id"]: z for z in sensors["zones"]}

for m in mods:
    if m["tier"] not in tiers:
        err("%s 画质档不存在 %s" % (m["id"], m["tier"]))
    if m["particlesMax"] > tiers[m["tier"]]["particlesMax"]:
        err("%s 粒子上限 %d 超过 %s 上限 %d" % (m["id"], m["particlesMax"], m["tier"], tiers[m["tier"]]["particlesMax"]))
    if m["show"] not in show_ids:
        err("%s 时间轴不存在 %s" % (m["id"], m["show"]))
    for sfc in m["surfaces"]:
        c = canvas_map.get(sfc["canvas"])
        if not c:
            err("%s 画布不存在 %s" % (m["id"], sfc["canvas"]))
            continue
        r = sfc["rect"]
        if r["x"] < 0 or r["y"] < 0 or r["x"] + r["w"] > c["width"] or r["y"] + r["h"] > c["height"]:
            err("%s 矩形越界 %s %s" % (m["id"], sfc["canvas"], r))
    for it in m["interactions"]:
        zid = it.get("zone")
        if zid and zid not in zones:
            err("%s 触点区不存在 %s" % (m["id"], zid))
        for zid in it.get("zones", []):
            if zid not in zones:
                err("%s 触点区不存在 %s" % (m["id"], zid))

# --- schedule
for s in schedule["shows"]:
    last = 0
    for seg in s.get("segments", []):
        if seg["start"] != last:
            warnings.append("%s 段不连续：期望 %s 实际 %s" % (s["id"], last, seg["start"]))
        last = seg["start"] + seg["dur"]
    if s.get("segments") and last != s.get("cycle") and not s.get("action") and not s.get("window"):
        err("%s 段总长 %s != cycle %s" % (s["id"], last, s["cycle"]))

d = next((s for s in schedule["shows"] if s["id"] == "D-MASTER"), None)
if not d or d["cycle"] != 720:
    err("D-MASTER 总轴必须 720s")
if d and sum(x["dur"] for x in d["segments"]) != 720:
    err("D-MASTER 分段之和不是 720")

# --- sensors zones
for z in sensors["zones"]:
    c = canvas_map.get(z["canvas"])
    if not c:
        err("触点 %s 画布不存在" % z["id"])
        continue
    r = z["rect"]
    if r["x"] < 0 or r["y"] < 0 or r["x"] + r["w"] > c["width"] or r["y"] + r["h"] > c["height"]:
        err("触点 %s 越界" % z["id"])
    if z["sensor"] not in sensors["capabilities"]:
        err("触点 %s 传感器不存在" % z["id"])

# --- hardware endpoints
caps = set(sensors["capabilities"].keys()) if isinstance(sensors.get("capabilities"), dict) else set(sensors.get("capabilities", []))
hw_ids = []
ir_keys = []
for group in ("radar", "infrared", "femto"):
    for ep in hardware.get(group, []):
        sid = ep.get("sensorId")
        if not sid or sid not in caps:
            err("hardware.json %s 的 sensorId %s 不在 sensors.capabilities" % (group, sid))
        if group in ("radar", "infrared"):
            port = ep.get("port")
            if not isinstance(port, int) or port < 1 or port > 65535:
                err("hardware.json %s/%s 端口非法 %s" % (group, sid, port))
            if not ep.get("source"):
                err("hardware.json %s/%s 缺 source" % (group, sid))
            if group == "radar":
                hw_ids.append(sid)
            else:
                # 四台红外可以共用 sensorId=IR，只要 (port, source) 组合唯一。
                ir_keys.append((port, ep.get("source")))
        else:
            hw_ids.append(sid)
            zid = ep.get("zone")
            if zid not in zones:
                err("hardware.json femto/%s zone 不存在 %s" % (sid, zid))
            if ep.get("width", 0) <= 0 or ep.get("height", 0) <= 0 or ep.get("fps", 0) <= 0:
                err("hardware.json femto/%s 分辨率/帧率非法" % sid)
            if ep.get("minDepthMm", 0) >= ep.get("maxDepthMm", 0):
                err("hardware.json femto/%s 深度范围非法" % sid)
if len(hw_ids) != len(set(hw_ids)):
    err("hardware.json radar/femto sensorId 重复：%s" % hw_ids)
if len(ir_keys) != len(set(ir_keys)):
    err("hardware.json infrared (port, source) 重复：%s" % ir_keys)
if not hardware.get("radar") or not hardware.get("femto"):
    err("hardware.json 缺 radar/femto 端点")

# --- single source of truth: config/ 与 StreamingAssets/config/ 必须逐字节一致
for name in sorted(os.listdir(CFG)):
    if not name.endswith(".json"):
        continue
    src = os.path.join(CFG, name)
    dst = os.path.join(STREAM, name)
    if not os.path.isfile(dst):
        err("StreamingAssets 缺 %s" % name)
        continue
    with open(src, "rb") as f:
        a = hashlib.sha256(f.read()).hexdigest()
    with open(dst, "rb") as f:
        b = hashlib.sha256(f.read()).hexdigest()
    if a != b:
        err("参数源不同步 %s：config=%s streaming=%s" % (name, a[:12], b[:12]))

# --- art slots
if art["total"] != 91 or len(art["elements"]) != 91:
    err("美术元素应为 91，实际 %d" % len(art["elements"]))
for m in mods:
    n = art["perModule"].get(m["id"], 0)
    if n == 0:
        err("%s 没有美术槽" % m["id"])
if len({e["elementId"] for e in art["elements"]}) != 91:
    err("美术元素 ID 重复")

print("canvas=%d channels=%d modules=%d zones=%d shows=%d art=%d"
      % (len(canvas_map), len(channels), len(mods), len(zones), len(schedule["shows"]), len(art["elements"])))
for w in warnings:
    print("[WARN]", w)
for e in errors:
    print("[ERROR]", e)
if errors:
    sys.exit(1)
print("CONFIG OK")
