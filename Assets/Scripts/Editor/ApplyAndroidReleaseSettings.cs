#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ApplyAndroidReleaseSettings
{
    [MenuItem("Tools/Android/Apply Release Settings")]
    public static void Apply()
    {
        // Switch platform
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // Build Settings
        EditorUserBuildSettings.buildAppBundle = true;                    // AAB
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;

        // PlayerSettings → Other Settings
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // add ARMv7 if needed
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.gcIncremental = true;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.Android.startInFullscreen = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        // Graphics APIs (force GLES3 only)
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        // Frame pacing
        PlayerSettings.Android.optimizedFramePacing = true;

        // Internet access (Require only if needed)
        PlayerSettings.Android.forceInternetPermission = false;

        // Publishing (minify)
        PlayerSettings.Android.minifyRelease = true;
        PlayerSettings.Android.minifyDebug = false;

        // Quality: disable MSAA on Android tier 0
        int androidTier = 0;
        QualitySettings.SetQualityLevel(androidTier, true);
        QualitySettings.antiAliasing = 0;
        QualitySettings.shadows = ShadowQuality.Disable;

        Debug.Log("✅ Android Release Settings applied. Check Player Settings for keystore + version code, then Build.");
    }
}
#endif
