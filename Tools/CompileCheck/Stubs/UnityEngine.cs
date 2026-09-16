// -----------------------------------------------------------------------------
// MINIMAL UnityEngine API STUBS - NOT PART OF THE GAME
//
// These types exist purely so the gameplay scripts in Assets/Scripts/Unity can be
// COMPILE-CHECKED outside the Unity editor (see Tools/CompileCheck). They contain
// no behaviour and are never shipped: the folder lives outside Assets/, so Unity
// never sees or compiles it.
//
// If a signature here disagrees with real Unity, the fix is to correct the stub -
// the real API always wins.
// -----------------------------------------------------------------------------
using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0f, 0f); } }
        public static Vector2 one { get { return new Vector2(1f, 1f); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
        public static bool operator ==(Vector2 a, Vector2 b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object other) { return other is Vector2 && this == (Vector2)other; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero { get { return new Vector4(0f, 0f, 0f, 0f); } }
        public static bool operator ==(Vector4 a, Vector4 b) { return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w; }
        public static bool operator !=(Vector4 a, Vector4 b) { return !(a == b); }
        public override bool Equals(object other) { return other is Vector4 && this == (Vector4)other; }
        public override int GetHashCode() { return x.GetHashCode(); }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public static Color magenta { get { return new Color(1f, 0f, 1f, 1f); } }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a); }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static Color operator /(Color c, float f) { return new Color(c.r / f, c.g / f, c.b / f, c.a / f); }
        public static Color Lerp(Color a, Color b, float t) { return a; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Abs(float v) { return Math.Abs(v); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float InverseLerp(float a, float b, float v) { return 0f; }
        public static int RoundToInt(float v) { return (int)Math.Round(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
    }

    public class Object
    {
        public string name;
        public HideFlags hideFlags;
        public static void Destroy(Object target) { }
        public static void DestroyImmediate(Object target) { }
        public static bool operator ==(Object a, Object b) { return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !ReferenceEquals(a, b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }

    [Flags]
    public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
        public Transform transform { get { return null; } }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public T AddComponent<T>() where T : Component, new() { return null; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }
        public Transform transform { get { return null; } }
        public bool activeSelf { get { return true; } }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component { return null; }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
    }

    public class Transform : Component
    {
        public Vector3 localScale { get; set; }
        public Transform parent { get; set; }
        public int childCount { get { return 0; } }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetAsLastSibling() { }
        public Transform GetChild(int index) { return null; }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Rect rect { get { return new Rect(); } }
    }

    public class Texture : Object { public FilterMode filterMode { get; set; } public TextureWrapMode wrapMode { get; set; } }

    public class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
        public void SetPixels(Color[] colors) { }
        public void Apply() { }
    }

    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum SpriteMeshType { FullRect, Tight }

    public class Sprite : Object
    {
        public Vector4 border { get { return Vector4.zero; } }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) { return null; }
    }

    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) { return null; }
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath { get { return "."; } }
        public static bool isPlaying { get { return false; } }
    }

    // Real Unity declares these as int constants on a static class, not as an enum,
    // which is why Screen.sleepTimeout (an int) can be assigned from them directly.
    public static class SleepTimeout
    {
        public const int NeverSleep = -2;
        public const int SystemSetting = -1;
    }

    public static class Screen
    {
        public static int sleepTimeout { get; set; }
        public static int width { get { return 1080; } }
        public static int height { get { return 1920; } }
    }

    public static class Time
    {
        public static float deltaTime { get { return 0.016f; } }
        public static float unscaledDeltaTime { get { return 0.016f; } }
        public static float time { get { return 0f; } }
    }

    public static class PlayerPrefs
    {
        public static bool HasKey(string key) { return false; }
        public static string GetString(string key) { return string.Empty; }
        public static void SetString(string key, string value) { }
        public static void DeleteKey(string key) { }
        public static void Save() { }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string htmlString, out Color color) { color = Color.white; return true; }
    }

    public class CanvasGroup : Component
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public bool pixelPerfect { get; set; }
        public int sortingOrder { get; set; }
    }

    public class RectOffset
    {
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string header) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string tooltip) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }

    public class PropertyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class AddComponentMenu : Attribute { public AddComponentMenu(string menuName) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponent : Attribute { public RequireComponent(Type type) { } }
}
