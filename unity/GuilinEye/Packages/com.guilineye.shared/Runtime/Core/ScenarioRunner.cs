using System;
using System.Collections;
using UnityEngine;

namespace GuilinEye.Shared
{
    [Serializable]
    public class ScenarioStep
    {
        public string id;
        public float delay;
        public string action;
        public string payload;
    }

    /// <summary>SP-04 按秒编排步骤；循环/暂停/停止，替代散落协程。</summary>
    public class ScenarioRunner : MonoBehaviour
    {
        public bool loop;
        public bool playOnStart;
        public ScenarioStep[] steps;

        Coroutine _coroutine;
        bool _running;
        bool _paused;

        public event Action<ScenarioStep> StepStarted;
        public event Action Completed;
        public bool IsRunning { get { return _running; } }
        public bool IsPaused { get { return _paused; } }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            Play(steps);
        }

        public void Play(ScenarioStep[] sequence)
        {
            if (sequence == null || sequence.Length == 0) return;
            Stop();
            steps = sequence;
            _running = true;
            _paused = false;
            _coroutine = StartCoroutine(Run());
        }

        public void Pause(bool paused) { _paused = paused; }

        public void Stop()
        {
            _running = false;
            _paused = false;
            if (_coroutine != null) { StopCoroutine(_coroutine); _coroutine = null; }
        }

        IEnumerator Run()
        {
            do
            {
                for (var i = 0; i < steps.Length && _running; i++)
                {
                    var step = steps[i];
                    if (step == null) continue;
                    if (step.delay > 0f) yield return new WaitForSecondsRealtime(step.delay);
                    while (_paused && _running) yield return null;
                    if (!_running) yield break;
                    if (StepStarted != null) StepStarted(step);
                }
            }
            while (_running && loop);

            _running = false;
            _coroutine = null;
            if (Completed != null) Completed();
        }

        void OnDisable() { Stop(); }
    }
}
