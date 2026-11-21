using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>
    /// TUIO 1.1 UDP 接收器。标准 TUIO 1.1 使用 OSC 1.0 二进制 bundle；现场调试软件也可能输出
    /// 便于人工阅读的 ASCII 文本行。两条路径都保留：二进制优先，文本作为兼容。
    /// </summary>
    public class TuioUdpReceiver : IDisposable
    {
        readonly int _port;
        readonly Action<TuioCursor> _onSet;
        readonly Action<TuioCursor> _onRemove;

        // source -> (cursor id -> cursor)。R1/R2 共用一个 UDP 端口时，绝不能用一个全局
        // active 字典互相清理；alive 只允许清理当前 source 桶。
        readonly Dictionary<string, Dictionary<int, TuioCursor>> _active =
            new Dictionary<string, Dictionary<int, TuioCursor>>(StringComparer.OrdinalIgnoreCase);
        readonly object _gate = new object();

        UdpClient _client;
        Thread _thread;
        volatile bool _running;
        int _packets;

        public int Port { get { return _port; } }
        public bool IsRunning { get { return _running; } }

        public TuioUdpReceiver(int port, Action<TuioCursor> onSet, Action<TuioCursor> onRemove)
        {
            _port = port;
            _onSet = onSet;
            _onRemove = onRemove;
        }

        public bool Start()
        {
            if (_running) return true;
            try
            {
                // 先创建未绑定的 UdpClient，设置 ReuseAddress 后再 Bind；构造函数直接 bind 会让
                // 后面的 SetSocketOption 变成无效操作。
                _client = new UdpClient();
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _client.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                _client.Client.ReceiveTimeout = 500;
                _running = true;
                _thread = new Thread(Loop);
                _thread.IsBackground = true;
                _thread.Name = "TuioUdp" + _port;
                _thread.Start();
                Debug.Log("[TUIO] 监听 UDP :" + _port + "（OSC bundle / ASCII 兼容）");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[TUIO] 绑定 UDP :" + _port + " 失败：" + e.Message);
                CleanupClient();
                return false;
            }
        }

        void Loop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint remote = null;
                    var data = _client.Receive(ref remote);
                    if (data != null && data.Length > 0) ParsePacket(data);
                }
                catch (SocketException e)
                {
                    if (!_running) break;
                    if (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock) continue;
                    Debug.LogWarning("[TUIO] :" + _port + " 接收异常 " + e.SocketErrorCode);
                    Thread.Sleep(50);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (_running) Debug.LogWarning("[TUIO] :" + _port + " 解析异常 " + e.Message);
                }
            }
        }

        void ParsePacket(byte[] data)
        {
            if (_packets < 3)
            {
                _packets++;
                Debug.Log("[TUIO] :" + _port + " 收到报文 #" + _packets + " bytes=" + data.Length);
            }

            var ctx = new PacketContext();
            if (IsBundle(data, 0, data.Length))
            {
                ParseBundle(data, 0, data.Length, ctx);
                return;
            }

            // 标准单条 OSC message（不带 bundle 的调试软件也会这样发）。
            if (data.Length > 1 && data[0] == (byte)'/' && TryParseOscMessage(data, 0, data.Length, ctx))
                return;

            // 兼容旧测试脚本/人工调试的纯文本行。
            var text = Encoding.UTF8.GetString(data).Trim('\0', '\r', '\n');
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++) ParseLine(lines[i].Trim(), ctx);
        }

        // ---------- OSC 1.0 binary ----------

        class PacketContext
        {
            public string Source = "";
        }

        class OscValue
        {
            public char Type;
            public string S;
            public long I;
            public double F;

            public int AsInt()
            {
                if (Type == 'i' || Type == 'h' || Type == 'c' || Type == 't') return (int)I;
                if (Type == 'f' || Type == 'd') return (int)F;
                if (Type == 's') { int v; return int.TryParse(S, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : -1; }
                return -1;
            }

            public float AsFloat()
            {
                if (Type == 'f' || Type == 'd') return (float)F;
                if (Type == 'i' || Type == 'h') return (float)I;
                if (Type == 's') { float v; return float.TryParse(S, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }
                return 0f;
            }

            public string AsString()
            {
                if (Type == 's') return S ?? "";
                if (Type == 'i' || Type == 'h' || Type == 'c' || Type == 't') return I.ToString(CultureInfo.InvariantCulture);
                if (Type == 'f' || Type == 'd') return F.ToString(CultureInfo.InvariantCulture);
                return "";
            }
        }

        static bool IsBundle(byte[] data, int start, int end)
        {
            return end - start >= 8 &&
                   data[start] == (byte)'#' && data[start + 1] == (byte)'b' && data[start + 2] == (byte)'u' &&
                   data[start + 3] == (byte)'n' && data[start + 4] == (byte)'d' && data[start + 5] == (byte)'l' &&
                   data[start + 6] == (byte)'e';
        }

        bool ParseBundle(byte[] data, int start, int end, PacketContext ctx)
        {
            if (!IsBundle(data, start, end)) return false;
            int pos = start;
            string head;
            if (!ReadOscString(data, pos, end, out head, out pos) || head != "#bundle") return false;
            if (pos + 8 > end) return false;
            pos += 8; // 8-byte OSC time tag

            while (pos + 4 <= end)
            {
                var size = ReadInt32BE(data, pos);
                pos += 4;
                if (size <= 0 || pos + size > end) break;
                var childEnd = pos + size;
                if (IsBundle(data, pos, childEnd))
                    ParseBundle(data, pos, childEnd, ctx);
                else
                    TryParseOscMessage(data, pos, childEnd, ctx);
                pos = childEnd;
            }
            return true;
        }

        bool TryParseOscMessage(byte[] data, int start, int end, PacketContext ctx)
        {
            int pos = start;
            string address;
            if (!ReadOscString(data, pos, end, out address, out pos)) return false;
            if (string.IsNullOrEmpty(address) || address[0] != '/') return false;

            string typeTag;
            if (!ReadOscString(data, pos, end, out typeTag, out pos)) return false;
            if (string.IsNullOrEmpty(typeTag) || typeTag[0] != ',') return false;

            var args = new List<OscValue>();
            for (var i = 1; i < typeTag.Length; i++)
            {
                OscValue value;
                if (!ReadOscArg(data, ref pos, end, typeTag[i], out value)) return false;
                args.Add(value);
            }

            DispatchOsc(address, args, ctx);
            return true;
        }

        void DispatchOsc(string address, List<OscValue> args, PacketContext ctx)
        {
            if (address != "/tuio/2Dcur" && address != "/tuio/2Dblb") return;
            if (args.Count < 1) return;

            var cmd = args[0].AsString();
            if (cmd == "source")
            {
                ctx.Source = args.Count > 1 ? args[1].AsString() : "";
                return;
            }
            if (cmd == "alive")
            {
                var seen = new HashSet<int>();
                for (var i = 1; i < args.Count; i++)
                {
                    var id = args[i].AsInt();
                    if (id >= 0) seen.Add(id);
                }
                HandleAlive(ctx.Source, seen);
                return;
            }
            if (cmd == "set")
            {
                if (args.Count < 4) return;
                var id = args[1].AsInt();
                if (id < 0) return;
                var x = args[2].AsFloat();
                var y = args[3].AsFloat();
                if (address == "/tuio/2Dblb")
                {
                    // 2Dblb set: s x y angle width height area [X Y]；与 2Dcur 的参数位不同。
                    var w = args.Count > 5 ? args[5].AsFloat() : 0f;
                    var h = args.Count > 6 ? args[6].AsFloat() : 0f;
                    ApplySet(address, id, x, y, 0f, 0f, ctx.Source, w, h);
                }
                else
                {
                    var vx = args.Count > 4 ? args[4].AsFloat() : 0f;
                    var vy = args.Count > 5 ? args[5].AsFloat() : 0f;
                    ApplySet(address, id, x, y, vx, vy, ctx.Source, 0f, 0f);
                }
                return;
            }
            // fseq 只用于时间戳/丢包诊断，本轮不参与交互。
        }

        // ---------- ASCII compatibility ----------

        void ParseLine(string line, PacketContext ctx)
        {
            if (string.IsNullOrEmpty(line)) return;
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 && !line.StartsWith("#")) return;

            var address = parts[0];
            if (line.StartsWith("#bundle")) return;

            if (address == "/tuio/2Dcur" || address == "/tuio/2Dblb")
            {
                var cmd = parts[1];
                if (cmd == "source")
                {
                    ctx.Source = parts.Length > 2 ? parts[2] : "";
                    return;
                }
                if (cmd == "alive")
                {
                    var seen = new HashSet<int>();
                    for (var i = 2; i < parts.Length; i++)
                    {
                        int id;
                        if (TryId(parts[i], out id)) seen.Add(id);
                    }
                    HandleAlive(ctx.Source, seen);
                    return;
                }
                if (cmd == "set")
                {
                    if (parts.Length < 5) return;
                    int id;
                    if (!TryId(parts[2], out id)) return;
                    float x, y;
                    if (!TryFloat(parts[3], out x) || !TryFloat(parts[4], out y)) return;
                    float vx = 0f, vy = 0f, w = 0f, h = 0f;
                    if (parts.Length > 5) TryFloat(parts[5], out vx);
                    if (parts.Length > 6) TryFloat(parts[6], out vy);
                    if (address == "/tuio/2Dblb")
                    {
                        if (parts.Length > 6) TryFloat(parts[6], out w);
                        if (parts.Length > 7) TryFloat(parts[7], out h);
                    }
                    ApplySet(address, id, x, y, vx, vy, ctx.Source, w, h);
                    return;
                }
                return;
            }
        }

        // ---------- shared cursor handling ----------

        void ApplySet(string address, int id, float x, float y, float vx, float vy, string source, float w, float h)
        {
            if (x < -0.02f || x > 1.02f || y < -0.02f || y > 1.02f) return;

            var c = new TuioCursor
            {
                Id = id,
                X = x,
                Y = y,
                VelocityX = vx,
                VelocityY = vy,
                Width = w,
                Height = h,
                Source = NormalizeSource(source),
                TimestampMs = NowMs()
            };

            var src = c.Source;
            Dictionary<int, TuioCursor> bucket;
            lock (_gate)
            {
                if (!_active.TryGetValue(src, out bucket))
                {
                    bucket = new Dictionary<int, TuioCursor>();
                    _active[src] = bucket;
                }
                bucket[id] = c;
            }
            if (_onSet != null) _onSet(c.Clone());
        }

        void HandleAlive(string source, HashSet<int> seen)
        {
            var src = NormalizeSource(source);
            List<TuioCursor> removed = null;
            lock (_gate)
            {
                Dictionary<int, TuioCursor> bucket;
                if (!_active.TryGetValue(src, out bucket) || bucket.Count == 0) return;

                foreach (var pair in bucket)
                {
                    if (seen.Contains(pair.Key)) continue;
                    if (removed == null) removed = new List<TuioCursor>();
                    removed.Add(pair.Value);
                }
                if (removed != null)
                {
                    for (var i = 0; i < removed.Count; i++) bucket.Remove(removed[i].Id);
                }
                if (bucket.Count == 0) _active.Remove(src);
            }

            if (removed != null && _onRemove != null)
                for (var i = 0; i < removed.Count; i++) _onRemove(removed[i].Clone());
        }

        static string NormalizeSource(string source)
        {
            return string.IsNullOrEmpty(source) ? "" : source.Trim();
        }

        // ---------- OSC primitive readers ----------

        static bool ReadOscString(byte[] data, int offset, int end, out string value, out int next)
        {
            value = "";
            next = offset;
            if (offset >= end) return false;
            var i = offset;
            while (i < end && data[i] != 0) i++;
            if (i >= end) return false;
            value = Encoding.UTF8.GetString(data, offset, i - offset);
            next = ((i + 1 + 3) / 4) * 4;
            return next <= end;
        }

        static bool ReadOscArg(byte[] data, ref int pos, int end, char type, out OscValue value)
        {
            value = new OscValue { Type = type };
            switch (type)
            {
                case 'i':
                case 'c':
                    if (pos + 4 > end) return false;
                    value.I = ReadInt32BE(data, pos);
                    pos += 4;
                    return true;
                case 'h':
                case 't':
                    if (pos + 8 > end) return false;
                    value.I = ReadInt64BE(data, pos);
                    pos += 8;
                    return true;
                case 'f':
                    if (pos + 4 > end) return false;
                    value.F = ReadFloatBE(data, pos);
                    pos += 4;
                    return true;
                case 'd':
                    if (pos + 8 > end) return false;
                    value.F = ReadDoubleBE(data, pos);
                    pos += 8;
                    return true;
                case 's':
                    string text;
                    if (!ReadOscString(data, pos, end, out text, out pos)) return false;
                    value.S = text;
                    return true;
                case 'b':
                    if (pos + 4 > end) return false;
                    var size = ReadInt32BE(data, pos);
                    pos += 4;
                    if (size < 0 || pos + size > end) return false;
                    pos = ((pos + size + 3) / 4) * 4;
                    return pos <= end;
                case 'T':
                case 'F':
                case 'N':
                case 'I':
                    return true;
                default:
                    return false;
            }
        }

        static int ReadInt32BE(byte[] data, int pos)
        {
            return (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
        }

        static long ReadInt64BE(byte[] data, int pos)
        {
            long value = 0;
            for (var i = 0; i < 8; i++) value = (value << 8) | (long)data[pos + i];
            return value;
        }

        static float ReadFloatBE(byte[] data, int pos)
        {
            var bytes = new byte[4];
            bytes[0] = data[pos + 3];
            bytes[1] = data[pos + 2];
            bytes[2] = data[pos + 1];
            bytes[3] = data[pos];
            return BitConverter.ToSingle(bytes, 0);
        }

        static double ReadDoubleBE(byte[] data, int pos)
        {
            var bytes = new byte[8];
            for (var i = 0; i < 8; i++) bytes[i] = data[pos + 7 - i];
            return BitConverter.ToDouble(bytes, 0);
        }

        static bool TryId(string token, out int id)
        {
            id = -1;
            if (string.IsNullOrEmpty(token)) return false;
            if ((token[0] == 's' || token[0] == 'S') && token.Length > 1) token = token.Substring(1);
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        static bool TryFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static long NowMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        void CleanupClient()
        {
            if (_thread != null)
            {
                try { if (_thread.IsAlive) _thread.Join(500); } catch { }
                _thread = null;
            }
            if (_client != null)
            {
                try { _client.Close(); } catch { }
                _client = null;
            }
        }

        public void Dispose()
        {
            if (!_running && _client == null && _thread == null) return;
            _running = false;

            List<TuioCursor> last = new List<TuioCursor>();
            lock (_gate)
            {
                foreach (var bucket in _active.Values)
                    last.AddRange(bucket.Values);
                _active.Clear();
            }
            if (_onRemove != null)
                for (var i = 0; i < last.Count; i++) _onRemove(last[i].Clone());

            if (_client != null)
            {
                try { _client.Close(); } catch { }
            }
            CleanupClient();
        }
    }

    /// <summary>同一 UDP 端口只开一个 socket；R1/R2 共用一个雷达调试软件端口时靠 source 分流。</summary>
    public static class TuioHub
    {
        class Entry
        {
            public readonly int Port;
            public readonly TuioUdpReceiver Receiver;
            public readonly List<Action<TuioCursor>> OnSet = new List<Action<TuioCursor>>();
            public readonly List<Action<TuioCursor>> OnRemove = new List<Action<TuioCursor>>();

            public Entry(int port)
            {
                Port = port;
                Receiver = new TuioUdpReceiver(port, DispatchSet, DispatchRemove);
            }

            public void DispatchSet(TuioCursor c)
            {
                Action<TuioCursor>[] copy;
                lock (OnSet) copy = OnSet.ToArray();
                for (var i = 0; i < copy.Length; i++)
                {
                    try { copy[i](c); }
                    catch (Exception e) { Debug.LogWarning("[TUIO] set 回调异常 " + e.Message); }
                }
            }

            public void DispatchRemove(TuioCursor c)
            {
                Action<TuioCursor>[] copy;
                lock (OnRemove) copy = OnRemove.ToArray();
                for (var i = 0; i < copy.Length; i++)
                {
                    try { copy[i](c); }
                    catch (Exception e) { Debug.LogWarning("[TUIO] remove 回调异常 " + e.Message); }
                }
            }
        }

        class Subscription : IDisposable
        {
            readonly int _port;
            readonly Action<TuioCursor> _onSet;
            readonly Action<TuioCursor> _onRemove;
            bool _disposed;

            public Subscription(int port, Action<TuioCursor> onSet, Action<TuioCursor> onRemove)
            {
                _port = port; _onSet = onSet; _onRemove = onRemove;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                TuioHub.Unsubscribe(_port, _onSet, _onRemove);
            }
        }

        static readonly object Gate = new object();
        static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();

        public static IDisposable Subscribe(int port, Action<TuioCursor> onSet, Action<TuioCursor> onRemove)
        {
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(port, out entry))
                {
                    entry = new Entry(port);
                    Entries[port] = entry;
                    entry.Receiver.Start();
                }
                lock (entry.OnSet) entry.OnSet.Add(onSet);
                lock (entry.OnRemove) entry.OnRemove.Add(onRemove);
                return new Subscription(port, onSet, onRemove);
            }
        }

        static void Unsubscribe(int port, Action<TuioCursor> onSet, Action<TuioCursor> onRemove)
        {
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(port, out entry)) return;
                lock (entry.OnSet) entry.OnSet.Remove(onSet);
                lock (entry.OnRemove) entry.OnRemove.Remove(onRemove);
                if (entry.OnSet.Count == 0 && entry.OnRemove.Count == 0)
                {
                    entry.Receiver.Dispose();
                    Entries.Remove(port);
                }
            }
        }
    }
}
