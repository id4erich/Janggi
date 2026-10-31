using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Janggi.Editor
{
    public class APKBuilder
    {
        [MenuItem("Build/Build Android APK")]
        public static void BuildAPK()
        {
            // 앱 타이틀 설정 ("성원장기")
            PlayerSettings.productName = "성원장기";
            PlayerSettings.companyName = "SeongWon";

            // 앱 아이콘 설정 (Assets/Resources/AppIcon.png)
            Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/AppIcon.png");
            if (iconTex != null)
            {
                // TextureImportSettings를 GUI/Icon 타겟으로 최적화 설정 가능
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[] { iconTex });
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new Texture2D[] { iconTex });
            }

            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string apkPath = Path.Combine(outputDir, "성원장기.apk");

            string[] scenes = new string[]
            {
                "Assets/Scenes/SampleScene.unity"
            };

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[APK Builder] Build Succeeded: {apkPath} ({summary.totalSize / 1024 / 1024} MB)");
            }
            else
            {
                Debug.LogError($"[APK Builder] Build Failed with result: {summary.result}");
            }
        }
    }
}
