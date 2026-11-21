using System;
using System.Collections;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-16 安全区适配；H5 容器/平板/异形屏统一。</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        public RectTransform target;
        public bool applyTop = true;
        public bool applyBottom = true;

        int _lastWidth;
        int _lastHeight;

        void OnEnable() { Apply(); }

        void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight) Apply();
        }

        public void Apply()
        {
            if (target == null) target = GetComponent<RectTransform>();
            if (target == null || Screen.width <= 0 || Screen.height <= 0) return;

            var safe = Screen.safeArea;
            var min = safe.position;
            var max = safe.position + safe.size;
            var anchorMin = new Vector2(min.x / Screen.width, applyBottom ? min.y / Screen.height : 0f);
            var anchorMax = new Vector2(max.x / Screen.width, applyTop ? max.y / Screen.height : 1f);

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }

    /// <summary>SP-16 世界空间文字/图标朝向相机；D 区标注、C05 印章位常用。</summary>
    public class Billboard : MonoBehaviour
    {
        public Camera targetCamera;
        public bool lockY = true;
        public bool invert;

        void LateUpdate() { FaceNow(); }

        public void FaceNow()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            var dir = targetCamera.transform.position - transform.position;
            if (lockY) dir.y = 0f;
            if (invert) dir = -dir;
            if (dir.sqrMagnitude < 0.000001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
