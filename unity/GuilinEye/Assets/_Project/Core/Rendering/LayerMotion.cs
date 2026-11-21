using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>层微动：缓慢漂移、呼吸缩放、极轻旋转，避免 18 个模块是静态贴图。</summary>
    public class LayerMotion : MonoBehaviour
    {
        public float drift = 0.008f;
        public float breathe = 0.015f;
        public float speed = 0.18f;
        public float phase;

        Vector3 _basePos;
        Vector3 _baseScale;

        public void Init(float driftMeters, float breatheAmount, float speedValue)
        {
            drift = driftMeters;
            breathe = breatheAmount;
            speed = speedValue;
            _basePos = transform.localPosition;
            _baseScale = transform.localScale;
        }

        void Start()
        {
            if (_basePos == Vector3.zero) _basePos = transform.localPosition;
            if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
        }

        void Update()
        {
            if (_baseScale == Vector3.zero) return;
            var t = Time.time * speed + phase;
            transform.localPosition = _basePos + new Vector3(Mathf.Sin(t * 1.3f) * drift, Mathf.Cos(t) * drift * 0.7f, 0f);
            var k = 1f + Mathf.Sin(t * 0.9f) * breathe;
            transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, _baseScale.z);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f) * 1.2f);
        }
    }
}
