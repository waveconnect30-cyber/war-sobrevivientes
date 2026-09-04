using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrostboundFrontier.Editor
{
    public static class FrostboundAndroidBuild
    {
        private const string Identifier = "com.waveconnect.frostboundfrontier";
        private const string BrandingFolder = "Assets/Branding";
        private const string IconPath = BrandingFolder + "/FrostboundIcon.png";
        private const string ApkPath = "Builds/Android/FrostboundFrontier.apk";

        [MenuItem("Frostbound Frontier/Android/1. Aplicar configuración")]
        public static void ApplySettings()
        {
            EnsureBrandingAsset();
            PlayerSettings.productName = "Frostbound Frontier";
            PlayerSettings.companyName = "Wave Connect";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Identifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            #pragma warning disable CS0618
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
            #pragma warning restore CS0618
            Sprite splash = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(.025f, .09f, .15f);
            if (splash != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, splash) };
            AssetDatabase.SaveAssets();
            Debug.Log("Frostbound Android: ARM64, API automática actual, IL2CPP, landscape y branding configurados.");
        }

        [MenuItem("Frostbound Frontier/Android/2. Generar APK")]
        public static void BuildApk()
        {
            ApplySettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("No se pudo activar Android. Verifica que Android Build Support esté instalado.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/FrostboundFrontier.unity" };
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("APK falló: " + report.summary.result + " · " + report.summary.totalErrors + " errores");
            Debug.Log("APK generado: " + Path.GetFullPath(ApkPath));
        }

        [MenuItem("Frostbound Frontier/Tutorial/Reiniciar para prueba")]
        public static void ResetTutorialForTesting()
        {
            PlayerPrefs.SetInt("frostbound-onboarding-local-test", 1);
            PlayerPrefs.DeleteKey("frostbound-onboarding-step-v1");
            PlayerPrefs.DeleteKey("frostbound-frontier-save");
            PlayerPrefs.DeleteKey("frostbound-active-march");
            PlayerPrefs.Save();
            Debug.Log("Tutorial y progreso local reiniciados. La información persistida en Supabase no fue eliminada.");
        }

        [MenuItem("Frostbound Frontier/Tutorial/Completar localmente para prueba")]
        public static void CompleteTutorialForTesting()
        {
            PlayerPrefs.SetInt("frostbound-onboarding-local-test", 1);
            PlayerPrefs.SetInt("frostbound-onboarding-step-v1", 5);
            PlayerPrefs.Save();
            Debug.Log("Tutorial marcado como completo únicamente para esta prueba local del Editor.");
        }

        private static void EnsureBrandingAsset()
        {
            if (!Directory.Exists(BrandingFolder)) Directory.CreateDirectory(BrandingFolder);
            if (!File.Exists(IconPath))
            {
                const int size = 512;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color dark = new Color(.025f, .09f, .15f, 1f);
                Color ice = new Color(.12f, .82f, 1f, 1f);
                Vector2 center = new Vector2(size * .5f, size * .5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float radius = delta.magnitude;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    bool ring = radius > 174f && radius < 194f;
                    bool snowArm = radius < 150f && Mathf.Abs(Mathf.Sin(angle * 3f)) < .09f;
                    bool core = radius < 38f;
                    float glow = Mathf.Clamp01(1f - radius / 256f) * .22f;
                    Color pixel = Color.Lerp(dark, new Color(.04f, .22f, .34f), glow);
                    if (ring || snowArm || core) pixel = ice;
                    texture.SetPixel(x, y, pixel);
                }
                texture.Apply();
                File.WriteAllBytes(IconPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
    }
}
