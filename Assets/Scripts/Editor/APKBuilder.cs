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

            // 기기 시험용 APK라 출시 키(비밀번호는 에디터를 켤 때마다 입력해야 함) 대신 디버그 키로 서명
            // Build App Bundle을 켜 둔 상태여도 설치할 수 있는 APK가 나오도록 잠시 끔
            bool customKeystore = PlayerSettings.Android.useCustomKeystore;
            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            UnityEditor.Build.Reporting.BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                AssetDatabase.SaveAssets(); // 빌드 중 저장된 '디버그 키' 값을 원래대로 파일에 다시 씀
            }
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
