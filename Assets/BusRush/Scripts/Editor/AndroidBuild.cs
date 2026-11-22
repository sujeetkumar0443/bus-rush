using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Android test build: applies the player settings (placeholder company / package), logs them read back, and builds
    /// a release APK (IL2CPP, ARM64, Vulkan + GLES3) to C:\UnityBuilds\BusRush_Source\builds\BusRush_test.apk.
    /// Batch: Unity.exe -batchmode -quit -buildTarget Android -executeMethod BusRush.EditorTools.AndroidBuild.BuildApkBatch
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "C:/UnityBuilds/BusRush_Source/builds/BusRush_test.apk";
        public const string PackageName = "busrush.parkingjam.cartraffic"; // placeholder, change before release
        public const string CompanyName = "TestStudio";                   // placeholder
        public const string ProductName = "Bus Rush";
        public const string Version = "0.1.0";
        public const int VersionCode = 1;
        public const int MinApiLevel = 24;

        [MenuItem("Tools/Bus Rush/Build Android APK")]
        public static void BuildApkMenu()
        {
            BuildReport report = BuildApk();
            EditorUtility.DisplayDialog("Bus Rush", report != null && report.summary.result == BuildResult.Succeeded
                ? $"APK built:\n{OutputPath}" : "Android build failed - see the Console.", "OK");
        }

        public static void BuildApkBatch()
        {
            BuildReport report = BuildApk();
            EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Tools/Bus Rush/Apply Android Player Settings")]
        public static void ApplySettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            // Portrait only, no auto-rotate.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // IL2CPP, ARM64 only, min API 24, target = highest installed.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinApiLevel;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Vulkan first, OpenGL ES 3 as the fallback (explicit list instead of "Auto Graphics API").
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // Unity 6: the "Made with Unity" splash is optional on every licence tier, Personal included.
            PlayerSettings.SplashScreen.show = false;

            // Test APK, release (not development).
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidBuild] Player settings applied:\n" + ReadBack());
        }

        /// <summary>The settings as Unity reports them now (to verify they stuck).</summary>
        public static string ReadBack()
        {
            var android = NamedBuildTarget.Android;
            var sb = new StringBuilder();
            sb.AppendLine($"  company={PlayerSettings.companyName} product={PlayerSettings.productName} package={PlayerSettings.GetApplicationIdentifier(android)}");
            sb.AppendLine($"  version={PlayerSettings.bundleVersion} versionCode={PlayerSettings.Android.bundleVersionCode}");
            sb.AppendLine($"  orientation={PlayerSettings.defaultInterfaceOrientation} autorotate(P/PUD/LL/LR)=" +
                          $"{PlayerSettings.allowedAutorotateToPortrait}/{PlayerSettings.allowedAutorotateToPortraitUpsideDown}/" +
                          $"{PlayerSettings.allowedAutorotateToLandscapeLeft}/{PlayerSettings.allowedAutorotateToLandscapeRight}");
            sb.AppendLine($"  backend={PlayerSettings.GetScriptingBackend(android)} arch={PlayerSettings.Android.targetArchitectures} " +
                          $"minSdk={(int)PlayerSettings.Android.minSdkVersion} targetSdk={PlayerSettings.Android.targetSdkVersion}");
            sb.AppendLine($"  graphicsAuto={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)} " +
                          $"apis={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            sb.AppendLine($"  splash={PlayerSettings.SplashScreen.show} aab={EditorUserBuildSettings.buildAppBundle} " +
                          $"development={EditorUserBuildSettings.development}");
            return sb.ToString();
        }

        public static BuildReport BuildApk()
        {
            ApplySettings();
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            if (File.Exists(OutputPath)) File.Delete(OutputPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
                // Test APK: keep the hidden debug panel (DebugOnly) in a non-development (fast) build.
                extraScriptingDefines = new[] { "BUSRUSH_TEST_TOOLS" }
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            long size = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length : 0;
            int warnings = report.steps.SelectMany(st => st.messages).Count(m => m.type == LogType.Warning);
            Debug.Log($"[AndroidBuild] RESULT {s.result}: {OutputPath} {size / (1024f * 1024f):F1} MB, " +
                      $"{s.totalErrors} errors, {warnings} warnings, {s.totalTime.TotalSeconds:F0}s, scenes={string.Join(",", scenes)}");
            foreach (BuildStepMessage m in report.steps.SelectMany(st => st.messages).Where(m => m.type != LogType.Log))
            {
                Debug.Log($"[AndroidBuild] {m.type}: {m.content}");
            }
            return report;
        }
    }
}
