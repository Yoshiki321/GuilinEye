using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GuilinEye.Shared
{
    /// <summary>SP-15 统一 JSON HTTP：超时、有限重试、幂等键；不要在 Update 里直接发请求。</summary>
    public class JsonHttpClient : MonoBehaviour
    {
        public static JsonHttpClient Instance { get; private set; }

        public string baseUrl = "http://127.0.0.1:8080";
        public string opsToken = "";
        public float timeoutSeconds = 5f;
        public int maxRetries = 1;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var token = Environment.GetEnvironmentVariable("VENUE_OPS_TOKEN");
            if (!string.IsNullOrEmpty(token)) opsToken = token.Trim();
        }

        public void Get<T>(string path, Action<T> onSuccess, Action<string> onError = null)
        {
            StartCoroutine(Send("GET", path, null, null, onSuccess, onError));
        }

        public void Post<T>(string path, object payload, Action<T> onSuccess, Action<string> onError = null, string idempotencyKey = null)
        {
            var json = payload is string ? (string)payload : JsonUtility.ToJson(payload);
            StartCoroutine(Send("POST", path, json, idempotencyKey, onSuccess, onError));
        }

        IEnumerator Send<T>(string method, string path, string body, string idempotencyKey, Action<T> onSuccess, Action<string> onError)
        {
            var url = BuildUrl(path);
            var attempts = Mathf.Max(0, maxRetries) + 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                using (var req = CreateRequest(method, url, body, idempotencyKey))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var text = req.downloadHandler != null ? req.downloadHandler.text : "";
                        if (string.IsNullOrEmpty(text))
                        {
                            if (onError != null) onError("空响应：" + url);
                            yield break;
                        }
                        T value = default(T);
                        try { value = JsonUtility.FromJson<T>(text); }
                        catch (Exception e)
                        {
                            if (onError != null) onError("JSON 解析失败：" + e.Message);
                            yield break;
                        }
                        if (onSuccess != null) onSuccess(value);
                        yield break;
                    }

                    if (attempt + 1 >= attempts)
                    {
                        if (onError != null) onError(req.error + " [" + req.responseCode + "] " + url);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.2f * (attempt + 1));
                }
            }
        }

        UnityWebRequest CreateRequest(string method, string url, string body, string idempotencyKey)
        {
            UnityWebRequest req;
            if (method == "POST")
            {
                req = new UnityWebRequest(url, "POST");
                if (!string.IsNullOrEmpty(body))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
            }
            else
            {
                req = UnityWebRequest.Get(url);
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.Max(1, (int)timeoutSeconds);
            if (!string.IsNullOrEmpty(opsToken)) req.SetRequestHeader("X-Ops-Token", opsToken);
            if (!string.IsNullOrEmpty(idempotencyKey)) req.SetRequestHeader("Idempotency-Key", idempotencyKey);
            return req;
        }

        string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return baseUrl;
            if (path.StartsWith("http://") || path.StartsWith("https://")) return path;
            if (string.IsNullOrEmpty(baseUrl)) return path;
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }

    [Serializable]
    public class TelemetryEvent
    {
        public string type;
        public string source;
        public string payload;
        public long atUnixMs;
    }

    [Serializable]
    public class TelemetryEnvelope
    {
        public TelemetryEvent[] events;
    }

    /// <summary>SP-15 遥测缓冲；定期 Drain 后批量 POST，避免每事件一条请求。</summary>
    public sealed class TelemetryBuffer
    {
        readonly RingBuffer<TelemetryEvent> _buffer;

        public TelemetryBuffer(int capacity = 256) { _buffer = new RingBuffer<TelemetryEvent>(capacity); }

        public int Count { get { return _buffer.Count; } }

        public void Add(string type, string source, string payload)
        {
            _buffer.Add(new TelemetryEvent
            {
                type = type,
                source = source,
                payload = payload,
                atUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond
            });
        }

        public TelemetryEvent[] Drain()
        {
            var events = _buffer.ToArray();
            _buffer.Clear();
            return events;
        }

        public string DrainJson()
        {
            var events = Drain();
            return JsonUtility.ToJson(new TelemetryEnvelope { events = events }, false);
        }

        public void Clear() { _buffer.Clear(); }
    }
}
