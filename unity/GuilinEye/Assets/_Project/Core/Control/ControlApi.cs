using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GuilinEye.Core
{
    [Serializable]
    public class ControlState
    {
        public bool paused;
        public string segment;
        public float volume = 1f;
        public string command;
    }

    /// <summary>
    /// 控幕接口。EHE 控制播放器 → venue-api /api/v1/control → 本机轮询 → VenueBus。
    /// 讲解员可 pause/resume/segment/volume，模块必须支持相位连续。
    /// </summary>
    public class ControlApi : MonoBehaviour
    {
        public static ControlApi Instance { get; private set; }
        public string baseUrl = "http://127.0.0.1:8080";
        public string opsToken = "";
        public float pollInterval = 0.5f;

        ControlState _last;
        Coroutine _loop;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyEnvironment();
        }

        void ApplyEnvironment()
        {
            var api = Environment.GetEnvironmentVariable("VENUE_API_URL");
            if (!string.IsNullOrEmpty(api)) baseUrl = api.TrimEnd('/');
            var token = Environment.GetEnvironmentVariable("VENUE_OPS_TOKEN");
            if (!string.IsNullOrEmpty(token)) opsToken = token.Trim();
        }

        public void StartPolling()
        {
            ApplyEnvironment();
            if (_loop == null)
            {
                _loop = StartCoroutine(Poll());
                Debug.Log("[Control] 轮询控幕状态 → " + baseUrl + (string.IsNullOrEmpty(opsToken) ? "" : "（带 X-Ops-Token）"));
            }
        }

        public void StopPolling()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        }

        IEnumerator Poll()
        {
            while (true)
            {
                using (var req = UnityWebRequest.Get(baseUrl + "/api/v1/control/state"))
                {
                    req.timeout = 2;
                    if (!string.IsNullOrEmpty(opsToken)) req.SetRequestHeader("X-Ops-Token", opsToken);
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var s = JsonUtility.FromJson<ControlState>(req.downloadHandler.text);
                            if (s != null) Apply(s);
                        }
                        catch (Exception e) { Debug.LogWarning("[Control] " + e.Message); }
                    }
                }
                yield return new WaitForSeconds(pollInterval);
            }
        }

        void Apply(ControlState s)
        {
            if (_last != null && _last.command == s.command && _last.segment == s.segment
                && _last.paused == s.paused && Mathf.Approximately(_last.volume, s.volume)) return;
            _last = s;
            VenueBus.Publish("control.state", "venue-api", s);
        }

        public void Simulate(string command, string segment = null, float volume = 1f)
        {
            var s = new ControlState { command = command, segment = segment, volume = volume, paused = command == "pause" };
            Apply(s);
        }
    }
}
