# -*- coding: utf-8 -*-
"""Send standard TUIO 1.1 / OSC 1.0 binary bundles to the venue player.

Usage:
  python tools/tuio_binary_probe.py --host 127.0.0.1 --radar-port 3333 --ir-port 3334

This is intentionally dependency-free (struct only) so it can run on the show PC.
It sends:
  R1 source + set(id=1) + alive(1) + fseq
  R2 source + set(id=1) + alive(1) + fseq   (same cursor id as R1, different source)
  IR source + set(id=2) + alive(2) + fseq
Then waits and sends one leave/alive-empty for each source.
"""
import argparse
import socket
import struct
import time


def osc_string(s):
    b = s.encode("utf-8")
    return b + b"\0" * (4 - (len(b) % 4))


def osc_int(v):
    return struct.pack(">i", int(v))


def osc_float(v):
    return struct.pack(">f", float(v))


def osc_message(address, type_tag, *args):
    out = bytearray(osc_string(address))
    out += osc_string("," + type_tag)
    for t, v in zip(type_tag, args):
        if t in ("i", "h", "c"):
            out += osc_int(v)
        elif t == "f":
            out += osc_float(v)
        elif t == "s":
            out += osc_string(str(v))
        else:
            raise ValueError("unsupported type " + t)
    return bytes(out)


def bundle(messages):
    out = bytearray(osc_string("#bundle"))
    out += struct.pack(">Q", 0)  # immediate time tag
    for m in messages:
        out += struct.pack(">i", len(m))
        out += m
    return bytes(out)


def send(sock, host, port, payload):
    sock.sendto(payload, (host, port))
    print("sent %d B -> %s:%d" % (len(payload), host, port))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--radar-port", type=int, default=3333)
    ap.add_argument("--ir-port", type=int, default=3334)
    ap.add_argument("--hold", type=float, default=3.0)
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def cur(source, cursor_id, x, y):
        return bundle([
            osc_message("/tuio/2Dcur", "ss", "source", source),
            osc_message("/tuio/2Dcur", "siffff", "set", cursor_id, x, y, 0.0, 0.0, 0.0),
            osc_message("/tuio/2Dcur", "si", "alive", cursor_id),
            osc_message("/tuio/2Dcur", "si", "fseq", 1),
        ])

    # R1/R2 deliberately use the same cursor id: source isolation must keep both alive.
    send(sock, args.host, args.radar_port, cur("R1", 1, 0.82, 0.55))
    time.sleep(0.3)
    send(sock, args.host, args.radar_port, cur("R2", 1, 0.18, 0.55))
    time.sleep(0.3)
    send(sock, args.host, args.ir_port, cur("IR", 2, 0.62, 0.48))

    time.sleep(args.hold)

    # Empty alive for each source: only that source's bucket may be removed.
    # Type tag must be "ss" for source (two string args); alive is just "s" with one arg.
    send(sock, args.host, args.radar_port, bundle([
        osc_message("/tuio/2Dcur", "ss", "source", "R1"),
        osc_message("/tuio/2Dcur", "s", "alive"),
    ]))
    send(sock, args.host, args.radar_port, bundle([
        osc_message("/tuio/2Dcur", "ss", "source", "R2"),
        osc_message("/tuio/2Dcur", "s", "alive"),
    ]))
    send(sock, args.host, args.ir_port, bundle([
        osc_message("/tuio/2Dcur", "ss", "source", "IR"),
        osc_message("/tuio/2Dcur", "s", "alive"),
    ]))
    time.sleep(0.4)

    # Re-enter with the same IR cursor id: proves leave cleared the active bucket.
    send(sock, args.host, args.ir_port, cur("IR", 2, 0.62, 0.48))
    time.sleep(0.5)
    send(sock, args.host, args.ir_port, bundle([
        osc_message("/tuio/2Dcur", "ss", "source", "IR"),
        osc_message("/tuio/2Dcur", "s", "alive"),
    ]))
    print("done")


if __name__ == "__main__":
    main()
