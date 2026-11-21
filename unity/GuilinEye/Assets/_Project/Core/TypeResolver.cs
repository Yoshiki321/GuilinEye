using System;

namespace GuilinEye.Core
{
    /// <summary>跨程序集按类型名查找。Type.GetType 只查调用方程序集，这里补全 AppDomain 扫描。</summary>
    public static class TypeResolver
    {
        public static Type Find(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
