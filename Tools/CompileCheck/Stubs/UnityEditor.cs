// MINIMAL UnityEditor API STUBS - NOT PART OF THE GAME. See UnityEngine.cs.
using System;
using UnityEngine;

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get { return string.Empty; } }
        public string path { get { return string.Empty; } }
        public bool IsValid() { return true; }
    }

    public static class SceneManager
    {
        public static Scene GetActiveScene() { return new Scene(); }
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) { return true; }
        public static void RevealInFinder(string path) { }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static bool IsValidFolder(string path) { return true; }
        public static string CreateFolder(string parentFolder, string newFolderName) { return string.Empty; }
    }

    public class EditorBuildSettingsScene
    {
        public string path;
        public bool enabled;
        public EditorBuildSettingsScene() { }
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, Android = 13, iOS = 4 }
    public enum BuildTarget { Android = 13, iOS = 9, StandaloneWindows64 = 19 }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }

    public enum AndroidSdkVersions
    {
        AndroidApiLevel22 = 22,
        AndroidApiLevel23 = 23,
        AndroidApiLevel24 = 24
    }

    [Flags]
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, All = 0xFFFFFFF }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }

        public static void SetApplicationIdentifier(BuildTargetGroup targetGroup, string identifier) { }
        public static void SetScriptingBackend(BuildTargetGroup targetGroup, ScriptingImplementation backend) { }

        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
        }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }
    public enum NewSceneMode { Single = 0, Additive = 1 }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode)
        {
            return new UnityEngine.SceneManagement.Scene();
        }

        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) { return true; }
        public static bool MarkSceneDirty(UnityEngine.SceneManagement.Scene scene) { return true; }
        public static UnityEngine.SceneManagement.Scene OpenScene(string path)
        {
            return new UnityEngine.SceneManagement.Scene();
        }
    }
}

namespace UnityEngine
{
    public enum UIOrientation
    {
        Portrait = 0,
        PortraitUpsideDown = 1,
        LandscapeRight = 2,
        LandscapeLeft = 3,
        AutoRotation = 4
    }
}
