using UnityEngine;

namespace GuilinEye.Core
{
    public enum VenueStatus { Simulated, FieldPending, Confirmed }

    /// <summary>三态标记：模拟通过 / 现场待验证 / 已确认。没有现场联调不得标已确认。</summary>
    public class StatusMarker : MonoBehaviour
    {
        TextMesh _text;
        VenueStatus _status = VenueStatus.Simulated;

        public VenueStatus Status { get { return _status; } }

        public void Attach(string moduleId, CanvasConfig canvas, Vector3 localOffset)
        {
            var go = new GameObject("Status_" + moduleId);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f);
            _text = go.AddComponent<TextMesh>();
            _text.fontSize = 48;
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            _text.color = Color.white;
            Set(VenueStatus.Simulated);
        }

        public void Set(VenueStatus status)
        {
            _status = status;
            if (_text == null) return;
            switch (status)
            {
                case VenueStatus.Confirmed:
                    _text.text = "已确认";
                    _text.color = new Color(0.55f, 1f, 0.65f);
                    break;
                case VenueStatus.FieldPending:
                    _text.text = "现场待验证";
                    _text.color = new Color(1f, 0.85f, 0.4f);
                    break;
                default:
                    _text.text = "模拟通过";
                    _text.color = new Color(0.7f, 0.8f, 1f);
                    break;
            }
        }
    }
}
