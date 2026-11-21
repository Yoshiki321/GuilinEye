"""B01 T1 长按 → 空 alive 离场 → 同区二次长按的标准 TUIO 1.1 探针。

用法：
  python tools/tuio_b01_leave_probe.py
需先启动 GuilinEye.exe -onlyarea B -onlymodule B01。
"""
import socket, struct, time

def s(v):
    b = v.encode('utf-8')
    return b + b'\0' * (4 - len(b) % 4)

def msg(addr, tags, *args):
    out = bytearray(s(addr)); out += s(',' + tags)
    for t, v in zip(tags, args):
        out += struct.pack('>i', int(v)) if t == 'i' else (struct.pack('>f', float(v)) if t == 'f' else s(str(v)))
    return bytes(out)

def bundle(ms):
    out = bytearray(s('#bundle')); out += struct.pack('>Q', 0)
    for m in ms:
        out += struct.pack('>i', len(m)); out += m
    return bytes(out)

def touch(sock, ident):
    sock.sendto(bundle([
        msg('/tuio/2Dcur', 'ss', 'source', 'R1'),
        msg('/tuio/2Dcur', 'siffff', 'set', ident, 0.08, 0.50, 0.0, 0.0, 0.0),
        msg('/tuio/2Dcur', 'si', 'alive', ident),
        msg('/tuio/2Dcur', 'si', 'fseq', 1),
    ]), ('127.0.0.1', 3333))

def hold(sock, ident, seconds):
    end = time.time() + seconds
    tick = 0
    while time.time() < end:
        touch(sock, ident)
        tick += 1
        time.sleep(0.1)

def leave(sock):
    sock.sendto(bundle([
        msg('/tuio/2Dcur', 'ss', 'source', 'R1'),
        msg('/tuio/2Dcur', 's', 'alive'),
    ]), ('127.0.0.1', 3333))

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
hold(sock, 1, 3.4)
leave(sock); time.sleep(0.2)
hold(sock, 2, 3.4)
leave(sock); time.sleep(0.5)
print('done')
