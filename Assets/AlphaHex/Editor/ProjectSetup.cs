using System;
using System.IO;
using AlphaHex.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AlphaHex.Editor
{
    /// <summary>Creates editor-owned render assets on first import; also exposes repeatable build entry points.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string StarterScene = "Assets/Scenes/Starter.unity";
        private const string PipelinePath = "Assets/AlphaHex/Settings/AlphaHexPipeline.asset";
        private const string RendererPath = "Assets/AlphaHex/Settings/AlphaHexRenderer.asset";
        private const string SessionKey = "AlphaHex.ProjectConfigured";

        static ProjectSetup()
        {
            EditorApplication.delayCall += ConfigureWhenReady;
        }

        private static void ConfigureWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ConfigureWhenReady;
                return;
            }
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            try
            {
                EnsureProject();
            }
            catch (Exception exception)
            {
                SessionState.SetBool(SessionKey, false);
                Debug.LogError("Alpha Hex setup failed. Use Alpha Hex > Configure project after fixing the Console error.\n" + exception);
            }
        }

        [MenuItem("Alpha Hex/Configure project")]
        public static void EnsureProject()
        {
            EnsureFolder("Assets/AlphaHex/Settings");
            EnsureFolder("Assets/AlphaHex/Resources");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "AlphaHexRenderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "AlphaHexPipeline";
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 2;
                pipeline.shadowDistance = 55;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;

            // Resources materials retain shaders used by the procedurally generated runtime world.
            EnsureMaterial("AlphaHexLit", "Universal Render Pipeline/Lit", true);
            EnsureMaterial("AlphaHexUnlit", "Universal Render Pipeline/Unlit", false);

            PlayerSettings.companyName = "Project Alpha";
            PlayerSettings.productName = "Alpha Hex Builder";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.projectalpha.hexbuilder");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.projectalpha.hexbuilder");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            // This starter deliberately uses the built-in mouse/touch Input API; no input package is needed.
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length > 0)
            {
                var serialized = new SerializedObject(settings[0]);
                SerializedProperty input = serialized.FindProperty("activeInputHandler");
                if (input != null && input.intValue != 0)
                {
                    input.intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (!File.Exists(StarterScene)) CreateStarterScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(StarterScene, true) };
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string name, string shaderName, bool emission)
        {
            string path = "Assets/AlphaHex/Resources/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Required shader missing: " + shaderName);
            var material = new Material(shader) { name = name };
            material.color = Color.white;
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.05f, 0.12f, 0.2f));
            }
            AssetDatabase.CreateAsset(material, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void CreateStarterScene()
        {
            EnsureFolder("Assets/Scenes");
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var bootstrap = new GameObject("Game Bootstrap");
                SceneManager.MoveGameObjectToScene(bootstrap, scene);
                bootstrap.AddComponent<GameBootstrap>();
                if (!EditorSceneManager.SaveScene(scene, StarterScene))
                    throw new InvalidOperationException("Could not save the starter scene.");
            }
            finally
            {
                // Automatic setup must preserve unrelated open scenes, including unsaved edits.
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
            }
        }

        [MenuItem("Alpha Hex/Open starter scene")]
        public static void OpenStarter()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureProject();
            EditorSceneManager.OpenScene(StarterScene);
        }

        [MenuItem("Alpha Hex/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/AlphaHexBuilder.app");

        [MenuItem("Alpha Hex/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AlphaHexBuilder.exe");

        [MenuItem("Alpha Hex/Build/iOS Xcode project")]
        public static void BuildIOS() => Build(BuildTarget.iOS, "Builds/iOS");

        private static void Build(BuildTarget target, string output)
        {
            EnsureProject();
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("Install the " + target + " Build Support module in Unity Hub first.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { StarterScene },
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Build failed: " + report.summary.result);
            Debug.Log("Built " + report.summary.outputPath);
        }
    }
}
