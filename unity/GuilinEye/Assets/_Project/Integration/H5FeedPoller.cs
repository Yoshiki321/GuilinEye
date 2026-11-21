using System;
using System.Collections;
using System.Collections.Generic;
using GuilinEye.Core;
using GuilinEye.Shared;
using UnityEngine;

namespace GuilinEye.Integration
{
    [Serializable]
    public class ApiEnvelope<T>
    {
        public bool ok;
        public T data;
    }

    [Serializable]
    public class C03QueuePayload
    {
        public int limit;
        public int waiting;
        public C03Item[] items;
    }

    [Serializable]
    public class C03Item
    {
        public string id;
        public string text;
        public string status;
        public string createdAt;
    }

    [Serializable]
    public class C03AckPayload
    {
        public string id;
        public string status;
    }

    [Serializable]
    public class C03AckRequest
    {
        public string status;
    }

    [Serializable]
    public class B04WallPayload
    {
        public B04WallItem[] items;
    }

    [Serializable]
    public class B04WallItem
    {
        public int id;
        public string @char;
        public string at;
    }

    [Serializable]
    public class C04WallPayload
    {
        public C04WallItem[] items;
    }

    [Serializable]
    public class C04WallItem
    {
        public string id;
        public string nickname;
    }

    [Serializable]
    public class C05OrdersPayload
    {
        public C05OrderItem[] items;
    }

    [Serializable]
    public class C05OrderItem
    {
        public string orderId;
        public string pickupCode;
        public string status;
    }

    /// <summary>
    /// H5 → Unity 的单向事件拉取器。HTTP 轮询并非“实时消息”，但展馆局域网内 2s 粒度足够；
    /// 所有事件仍统一走 VenueBus，模块不直接依赖 HTTP。
    /// </summary>
    public class H5FeedPoller : MonoBehaviour
    {
        public string baseUrl = "http://127.0.0.1:8080";
        public float pollInterval = 2f;
        public bool pollC03 = true;
        public bool pollB04 = true;
        public bool pollC04 = true;
        public bool pollC05 = true;
        public bool logErrors;

        JsonHttpClient _client;
        float _nextPoll;
        bool _running;
        int _b04After;
        readonly HashSet<string> _seenWishes = new HashSet<string>();
        readonly HashSet<string> _ackedWishes = new HashSet<string>();
        readonly HashSet<string> _seenWorks = new HashSet<string>();
        readonly HashSet<string> _seenOrders = new HashSet<string>();
        bool _c03InFlight;

        public void StartPoller()
        {
            if (_running) return;
            var env = Environment.GetEnvironmentVariable("VENUE_API_URL");
            if (!string.IsNullOrEmpty(env)) baseUrl = env.TrimEnd('/');
            _client = JsonHttpClient.Instance;
            if (_client == null)
            {
                var go = new GameObject("JsonHttpClient");
                _client = go.AddComponent<JsonHttpClient>();
            }
            _client.baseUrl = baseUrl;
            var token = Environment.GetEnvironmentVariable("VENUE_OPS_TOKEN");
            if (!string.IsNullOrEmpty(token)) _client.opsToken = token.Trim();
            _running = true;
            _nextPoll = Time.unscaledTime;
            Debug.Log("[H5Feed] 启动 → " + baseUrl + "，轮询 " + pollInterval + "s");
        }

        void Update()
        {
            if (!_running || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + Mathf.Max(0.5f, pollInterval);
            if (pollC03) PollC03();
            if (pollB04) PollB04();
            if (pollC04) PollC04();
            if (pollC05) PollC05();
        }

        void PollC03()
        {
            if (_c03InFlight) return;
            _c03InFlight = true;
            _client.Get<ApiEnvelope<C03QueuePayload>>("api/v1/c03/queue",
                env =>
                {
                    _c03InFlight = false;
                    if (env == null || !env.ok || env.data == null || env.data.items == null) return;
                    for (var i = 0; i < env.data.items.Length; i++)
                    {
                        var item = env.data.items[i];
                        if (item == null || string.IsNullOrEmpty(item.id)) continue;
                        if (item.status == "done" || item.status == "displaying")
                        {
                            _seenWishes.Add(item.id);
                            continue;
                        }
                        if (Remember(_seenWishes, item.id))
                            VenueBus.Publish("c03.wish", "h5", item.text ?? "");
                        if (!_ackedWishes.Contains(item.id))
                            AckWish(item);
                    }
                },
                err =>
                {
                    _c03InFlight = false;
                    if (logErrors) Debug.LogWarning("[H5Feed] C03 队列失败：" + err);
                });
        }

        void AckWish(C03Item item)
        {
            _client.Post<ApiEnvelope<C03AckPayload>>("api/v1/c03/blessings/" + item.id + "/ack",
                new C03AckRequest { status = "displaying" },
                env =>
                {
                    if (env != null && env.ok) Remember(_ackedWishes, item.id);
                },
                err => { if (logErrors) Debug.LogWarning("[H5Feed] C03 ack 失败：" + err); },
                "ack-" + item.id);
        }

        void PollB04()
        {
            _client.Get<ApiEnvelope<B04WallPayload>>("api/v1/b04/wall-events?after=" + _b04After,
                env =>
                {
                    if (env == null || !env.ok || env.data == null || env.data.items == null) return;
                    for (var i = 0; i < env.data.items.Length; i++)
                    {
                        var item = env.data.items[i];
                        if (item == null) continue;
                        _b04After = Mathf.Max(_b04After, item.id);
                        if (!string.IsNullOrEmpty(item.@char))
                            VenueBus.Publish("b04.dropChar", "h5", item.@char);
                    }
                },
                err => { if (logErrors) Debug.LogWarning("[H5Feed] B04 失败：" + err); });
        }

        void PollC04()
        {
            _client.Get<ApiEnvelope<C04WallPayload>>("api/v1/c04/wall",
                env =>
                {
                    if (env == null || !env.ok || env.data == null || env.data.items == null) return;
                    for (var i = 0; i < env.data.items.Length; i++)
                    {
                        var item = env.data.items[i];
                        if (item == null || string.IsNullOrEmpty(item.id)) continue;
                        if (Remember(_seenWorks, item.id))
                            VenueBus.Publish("c04.work", "h5", item.id);
                    }
                },
                err => { if (logErrors) Debug.LogWarning("[H5Feed] C04 失败：" + err); });
        }

        void PollC05()
        {
            _client.Get<ApiEnvelope<C05OrdersPayload>>("api/v1/c05/orders",
                env =>
                {
                    if (env == null || !env.ok || env.data == null || env.data.items == null) return;
                    for (var i = 0; i < env.data.items.Length; i++)
                    {
                        var item = env.data.items[i];
                        if (item == null || string.IsNullOrEmpty(item.pickupCode)) continue;
                        if (Remember(_seenOrders, item.pickupCode))
                            VenueBus.Publish("c05.order", "h5", item.pickupCode);
                    }
                },
                err => { if (logErrors) Debug.LogWarning("[H5Feed] C05 失败：" + err); });
        }
        static bool Remember(HashSet<string> seen, string id)
        {
            // 展馆全天运行，集合必须有上限；超过阈值后清空会重放少量旧事件，但不会泄漏内存。
            if (seen.Count > 2500) seen.Clear();
            return seen.Add(id);
        }
    }
}
