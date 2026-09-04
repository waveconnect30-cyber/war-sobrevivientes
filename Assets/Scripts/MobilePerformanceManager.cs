using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class MobilePerformanceManager : MonoBehaviour
    {
        private const string FpsKey = "frostbound-mobile-fps";
        private const string ResolutionKey = "frostbound-mobile-resolution";
        private bool panelOpen;
        private float smoothedDelta = 1f / 60f;
        private int targetFps;
        private float resolutionScale;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;

        public static int TargetFps { get; private set; } = 60;
        public static float ResolutionScale { get; private set; } = 1f;
        private static bool settingsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MobilePerformanceManager>() == null)
                new GameObject(nameof(MobilePerformanceManager)).AddComponent<MobilePerformanceManager>();
        }

        private void Awake()
        {
            targetFps = PlayerPrefs.GetInt(FpsKey, 60) <= 30 ? 30 : 60;
            resolutionScale = Mathf.Clamp(PlayerPrefs.GetFloat(ResolutionKey, 1f), .7f, 1f);
            Apply();
        }

        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, .08f);
        }

        public static bool IsPointerOverUi(Vector2 screenPoint)
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(safe.width / 1280f, .75f, 1.35f);
            Vector2 point = new Vector2((screenPoint.x - safe.x) / scale, (safe.yMax - screenPoint.y) / scale);
            float width = safe.width / scale;
            if (new Rect(width - 124f, 88f, 104f, 32f).Contains(point)) return true;
            return settingsOpen && new Rect(width - 330f, 126f, 310f, 166f).Contains(point);
        }

        private void Apply()
        {
            TargetFps = targetFps;
            ResolutionScale = resolutionScale;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFps;
            ScalableBufferManager.ResizeBuffers(resolutionScale, resolutionScale);
            PlayerPrefs.SetInt(FpsKey, targetFps);
            PlayerPrefs.SetFloat(ResolutionKey, resolutionScale);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            EnsureStyles();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(safe.width / 1280f, .75f, 1.35f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0f), Quaternion.identity, Vector3.one * scale);
            float width = safe.width / scale;
            float fps = smoothedDelta > .0001f ? 1f / smoothedDelta : 0f;
            GUI.depth = -250;
            GUI.Label(new Rect(width - 230f, 88f, 100f, 32f), Mathf.RoundToInt(fps) + " FPS", labelStyle);
            if (GUI.Button(new Rect(width - 124f, 88f, 104f, 32f), panelOpen ? "CERRAR" : "AJUSTES", buttonStyle)) { panelOpen = !panelOpen; settingsOpen = panelOpen; }
            if (panelOpen)
            {
                GUI.Box(new Rect(width - 330f, 126f, 310f, 166f), GUIContent.none, labelStyle);
                GUI.Label(new Rect(width - 310f, 140f, 270f, 28f), "RENDIMIENTO MÓVIL", labelStyle);
                if (GUI.Button(new Rect(width - 310f, 176f, 128f, 38f), "30 FPS", buttonStyle)) { targetFps = 30; Apply(); }
                if (GUI.Button(new Rect(width - 172f, 176f, 128f, 38f), "60 FPS", buttonStyle)) { targetFps = 60; Apply(); }
                if (GUI.Button(new Rect(width - 310f, 224f, 128f, 38f), "RES. 80%", buttonStyle)) { resolutionScale = .8f; Apply(); }
                if (GUI.Button(new Rect(width - 172f, 224f, 128f, 38f), "RES. 100%", buttonStyle)) { resolutionScale = 1f; Apply(); }
            }
            GUI.matrix = old;
        }

        private void EnsureStyles()
        {
            if (buttonStyle != null) return;
            panelTexture = Solid(new Color(.025f, .09f, .13f, .96f));
            buttonTexture = Solid(new Color(.03f, .48f, .72f, 1f));
            labelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture, textColor = Color.white }, fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { normal = { background = buttonTexture, textColor = Color.white }, fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            FrostboundVisualTheme.ApplyPanel(labelStyle, true);
            FrostboundVisualTheme.ApplyButton(buttonStyle);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
