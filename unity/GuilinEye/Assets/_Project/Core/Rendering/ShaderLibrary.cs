using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>构建后也能拿到的自定义无光照纹理 Shader，避免 Shader.Find 在 Player 中返回 null。</summary>
    public static class ShaderLibrary
    {
        static Shader _unlit;
        public static Shader UnlitTextured()
        {
            if (_unlit != null) return _unlit;
            _unlit = Shader.Find("GuilinEye/UnlitTextured");
            if (_unlit == null) _unlit = Resources.Load<Shader>("Shaders/GuilinEyeUnlit");
            if (_unlit == null) _unlit = Shader.Find("Sprites/Default");
            if (_unlit == null) Debug.LogError("[Shader] 找不到 GuilinEye/UnlitTextured，检查 Resources/Shaders/GuilinEyeUnlit.shader");
            return _unlit;
        }
    }
}
