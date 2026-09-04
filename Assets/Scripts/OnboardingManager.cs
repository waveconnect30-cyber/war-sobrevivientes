using System.Collections;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class OnboardingManager : MonoBehaviour
    {
        private const string StepKey = "frostbound-onboarding-step-v1";
        private const string TestModeKey = "frostbound-onboarding-local-test";
        public const int CompletedStep = 5;
        public static OnboardingManager Instance { get; private set; }
        public static int Step => Instance != null ? Instance.step : CompletedStep;
        public static bool IsActive => Step < CompletedStep;
        public static bool BlockOtherPanels => IsActive && (Step <= 2 || (Step == 4 && !(QuestMailManager.Instance?.QuestsOpen ?? false)));

        private int step;
        private FrostboundFrontierPrototype prototype;
        private Texture2D shade;
        private Texture2D card;
        private GUIStyle shadeStyle;
        private GUIStyle cardStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle pointerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OnboardingManager>() == null)
                new GameObject(nameof(OnboardingManager)).AddComponent<OnboardingManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => FindAnyObjectByType<FrostboundFrontierPrototype>() != null);
            prototype = FindAnyObjectByType<FrostboundFrontierPrototype>();
            bool hasLocal = PlayerPrefs.HasKey(StepKey);
            step = hasLocal ? Mathf.Clamp(PlayerPrefs.GetInt(StepKey), 0, CompletedStep) : (prototype.LegacyTutorialComplete ? CompletedStep : 0);
            PrepareStep();
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (!IsLocalTestMode && cloud != null && cloud.CanQueryWorld)
                yield return cloud.FetchTutorialProgress(ApplyCloudProgress, _ => { });
        }

        public static void Notify(string action)
        {
            if (Instance == null || !IsActive) return;
            string expected = Instance.step == 0 ? "GeneratorOn" : Instance.step == 1 ? "WorkerAssigned" : Instance.step == 2 ? "TroopsTrained" : Instance.step == 3 ? "BeastDefeated" : "DailyRewardClaimed";
            if (action != expected) return;
            Instance.step++;
            Instance.Persist();
            Instance.PrepareStep();
        }

        private void ApplyCloudProgress(SupabaseSyncClient.TutorialProgressState cloud)
        {
            if (cloud == null) { Persist(); return; }
            step = Mathf.Clamp(Mathf.Max(step, cloud.step), 0, CompletedStep);
            Persist();
            PrepareStep();
        }

        private void Persist()
        {
            PlayerPrefs.SetInt(StepKey, step);
            PlayerPrefs.Save();
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (!IsLocalTestMode && cloud != null && cloud.CanQueryWorld)
                StartCoroutine(cloud.SaveTutorialProgress(step, step >= CompletedStep));
        }

        private void PrepareStep()
        {
            if (prototype == null || step >= CompletedStep) return;
            if (step == 0) prototype.PrepareTutorialBuilding("generator");
            else if (step == 1) prototype.PrepareTutorialBuilding("sawmill");
            else if (step == 2) prototype.PrepareTutorialBuilding("barracks");
        }

        private void OnGUI()
        {
            if (!IsActive || prototype == null) return;
            EnsureStyles();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(safe.width / 1280f, .75f, 1.35f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0f), Quaternion.identity, Vector3.one * scale);
            float width = safe.width / scale;
            float height = safe.height / scale;
            GUI.depth = -900;

            Rect target = TargetRect(width, height);
            bool block = target.width > 0f;
            if (block) DrawBlockingShade(width, height, target);
            Rect message = new Rect(width * .5f - 310f, 90f, 620f, 92f);
            GUI.Box(message, GUIContent.none, cardStyle);
            GUI.Label(new Rect(message.x + 18f, message.y + 9f, message.width - 36f, 30f), "TUTORIAL " + (step + 1) + " / 5", titleStyle);
            GUI.Label(new Rect(message.x + 18f, message.y + 42f, message.width - 36f, 40f), Instruction(), bodyStyle);
            if (block) GUI.Label(new Rect(target.center.x - 34f, target.y - 46f, 68f, 44f), "▼", pointerStyle);
            GUI.matrix = old;
        }

        private Rect TargetRect(float width, float height)
        {
            if (WorldMapManager.IsWorldMapActive) return Rect.zero;
            if (step == 0) return new Rect(width - 455f, height - 136f, 190f, 58f);
            if (step == 1) return new Rect(width - 557f, height - 105f, 92f, 34f);
            if (step == 2) return new Rect(width - 250f, height - 136f, 210f, 58f);
            if (step == 3) return new Rect(width - 250f, height - 220f, 210f, 50f);
            if (step == 4 && !(QuestMailManager.Instance?.QuestsOpen ?? false)) return new Rect(292f, 148f, 150f, 40f);
            return Rect.zero;
        }

        private string Instruction()
        {
            if (step == 0) return "Enciende el Generador Térmico para calentar el asentamiento.";
            if (step == 1) return "Asigna tu primer aldeano al Aserradero.";
            if (step == 2) return "Entrena el primer lote de Infantería de Nieve.";
            if (step == 3) return WorldMapManager.IsWorldMapActive ? "Busca un Lobo de Niebla Nv. 1, selecciónalo y envía una marcha de ataque." : "Abre el Mapa Mundial para buscar un Lobo de Niebla Nv. 1.";
            return (QuestMailManager.Instance?.QuestsOpen ?? false) ? "Reclama la primera misión diaria disponible." : "Abre Misiones y reclama tu primera recompensa diaria.";
        }

        private void DrawBlockingShade(float width, float height, Rect hole)
        {
            Rect[] blocks = {
                new Rect(0f, 0f, width, Mathf.Max(0f, hole.y)),
                new Rect(0f, hole.yMax, width, Mathf.Max(0f, height - hole.yMax)),
                new Rect(0f, hole.y, Mathf.Max(0f, hole.x), hole.height),
                new Rect(hole.xMax, hole.y, Mathf.Max(0f, width - hole.xMax), hole.height)
            };
            foreach (Rect rect in blocks) if (rect.width > 0f && rect.height > 0f) GUI.Button(rect, GUIContent.none, shadeStyle);
            Color border = new Color(.1f, .95f, 1f, 1f);
            DrawBorder(new Rect(hole.x - 5f, hole.y - 5f, hole.width + 10f, hole.height + 10f), 5f, border);
        }

        private void EnsureStyles()
        {
            if (cardStyle != null) return;
            shade = Solid(new Color(0f, .02f, .04f, .72f));
            card = Solid(new Color(.02f, .28f, .43f, .98f));
            shadeStyle = new GUIStyle(GUI.skin.button) { normal = { background = shade }, hover = { background = shade }, active = { background = shade } };
            cardStyle = new GUIStyle(GUI.skin.box) { normal = { background = card } };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            bodyStyle = new GUIStyle(titleStyle) { fontSize = 17, fontStyle = FontStyle.Normal, wordWrap = true };
            pointerStyle = new GUIStyle(titleStyle) { fontSize = 36, normal = { textColor = new Color(.15f, 1f, 1f) } };
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        private static bool IsLocalTestMode => Application.isEditor && PlayerPrefs.GetInt(TestModeKey, 0) == 1;

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
