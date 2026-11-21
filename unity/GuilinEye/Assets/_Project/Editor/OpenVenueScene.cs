using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuilinEye.Editor
{
    /// <summary>
    /// CLI 启动入口：打开 Entrance 分区场景并直接进入 Play。
    /// 用法：
    /// Unity.exe -projectPath ... -executeMethod GuilinEye.Editor.OpenVenueScene.OpenEntrance
    /// </summary>
    public static class OpenVenueScene
    {
        const string EntrancePath = "Assets/_Project/Installations/Entrance.unity";

        public static void OpenEntrance()
        {
            if (!File.Exists(EntrancePath))
            {
                Debug.LogError("[OpenVenueScene] 找不到 " + EntrancePath);
                return;
            }

            EditorSceneManager.OpenScene(EntrancePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            Debug.Log("[OpenVenueScene] 已打开 Entrance 并请求进入 Play。");
        }
    }
}
