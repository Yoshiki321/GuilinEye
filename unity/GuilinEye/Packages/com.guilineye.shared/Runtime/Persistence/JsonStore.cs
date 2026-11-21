using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-14 原子 JSON 存档；先写 .tmp，再替换，失败留 .bak。</summary>
    public static class JsonStore
    {
        public static T Load<T>(string path, T fallback = null) where T : class
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return fallback;
            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var value = JsonUtility.FromJson<T>(text);
                return value ?? fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JsonStore] 读取失败 " + path + "：" + e.Message);
                return fallback;
            }
        }

        public static bool TryLoad<T>(string path, out T value) where T : class
        {
            value = Load<T>(path, null);
            return value != null;
        }

        public static void Save<T>(string path, T data, bool pretty = true)
        {
            if (string.IsNullOrEmpty(path) || data == null) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonUtility.ToJson(data, pretty);
            var tmp = path + ".tmp";
            var bak = path + ".bak";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            try
            {
                if (File.Exists(path))
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Replace(tmp, path, bak);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JsonStore] 原子替换失败，退回直接写 " + path + "：" + e.Message);
                if (File.Exists(tmp)) File.Copy(tmp, path, true);
            }
        }

        public static bool Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>SP-14 配置指纹；配置变更后用于判断是否需要重载/回滚。</summary>
    public static class ConfigFingerprint
    {
        public static string ComputeFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return ToHex(sha.ComputeHash(stream));
        }

        public static string ComputeDirectory(string directory, string searchPattern = "*.json")
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return "";
            var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                foreach (var file in files)
                {
                    var nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(file) + "|");
                    sha.TransformBlock(nameBytes, 0, nameBytes.Length, nameBytes, 0);
                    var bytes = File.ReadAllBytes(file);
                    sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return ToHex(sha.Hash);
            }
        }

        public static string Short(string hash, int length = 12)
        {
            if (string.IsNullOrEmpty(hash)) return "";
            return hash.Length <= length ? hash : hash.Substring(0, length);
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
