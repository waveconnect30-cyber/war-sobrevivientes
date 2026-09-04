using System;
using System.Collections;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class QuestMailManager : MonoBehaviour
    {
        public static QuestMailManager Instance { get; private set; }
        public static bool IsPanelOpen => Instance != null && (Instance.questsOpen || Instance.mailOpen);
        public bool QuestsOpen => questsOpen;

        private SupabaseSyncClient.QuestCloudState[] quests = Array.Empty<SupabaseSyncClient.QuestCloudState>();
        private SupabaseSyncClient.AchievementCloudState[] achievements = Array.Empty<SupabaseSyncClient.AchievementCloudState>();
        private SupabaseSyncClient.MailCloudState[] mail = Array.Empty<SupabaseSyncClient.MailCloudState>();
        private bool questsOpen;
        private bool mailOpen;
        private bool showAchievements;
        private bool busy;
        private bool claimingDailyQuest;
        private string mailCategory = "Battle";
        private string message = "Sincronizando objetivos...";
        private Vector2 scroll;
        private Texture2D panelTexture;
        private Texture2D rowTexture;
        private Texture2D buttonTexture;
        private Texture2D accentTexture;
        private GUIStyle panelStyle;
        private GUIStyle rowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private GUIStyle progressStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<QuestMailManager>() == null)
                new GameObject(nameof(QuestMailManager)).AddComponent<QuestMailManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SupabaseSyncClient.Instance != null && SupabaseSyncClient.Instance.CanQueryWorld);
            yield return SupabaseSyncClient.Instance.InitializeHito11(RefreshAll, error => message = error);
        }

        public void RecordProgress(string objective, int amount)
        {
            if (amount <= 0 || SupabaseSyncClient.Instance == null || !SupabaseSyncClient.Instance.CanQueryWorld) return;
            StartCoroutine(SupabaseSyncClient.Instance.RecordQuestProgress(objective, amount, RefreshAll));
        }

        public void AddBattleReport(string sourceKey, string subject, string body)
        {
            if (SupabaseSyncClient.Instance == null || !SupabaseSyncClient.Instance.CanQueryWorld) return;
            StartCoroutine(SupabaseSyncClient.Instance.AddBattleMail(sourceKey, subject, body));
        }

        private void RefreshAll()
        {
            if (busy || SupabaseSyncClient.Instance == null) return;
            StartCoroutine(RefreshRoutine());
        }

        private IEnumerator RefreshRoutine()
        {
            busy = true;
            yield return SupabaseSyncClient.Instance.FetchDailyQuests(rows => quests = rows, error => message = error);
            yield return SupabaseSyncClient.Instance.FetchAchievements(rows => achievements = rows, error => message = error);
            yield return SupabaseSyncClient.Instance.FetchMail(mailCategory, rows => mail = rows, error => message = error);
            busy = false;
            message = "Datos actualizados";
        }

        private void OnGUI()
        {
            if (WorldMapManager.IsWorldMapActive) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.width / 1280f, .75f, 1.35f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.depth = -30;

            if (!AllianceManager.IsPanelOpen && !ResearchManager.IsPanelOpen && !InventoryShopManager.IsPanelOpen && !questsOpen && !mailOpen)
            {
                if (GUI.Button(new Rect(292f, 148f, 150f, 40f), "MISIONES", buttonStyle)) { questsOpen = true; RefreshAll(); }
                if (GUI.Button(new Rect(450f, 148f, 150f, 40f), "BUZON", buttonStyle)) { mailOpen = true; LoadMail("Battle"); }
            }

            if (questsOpen) DrawQuests(width, height);
            if (mailOpen) DrawMail(width, height);
            GUI.matrix = old;
        }

        private void DrawQuests(float width, float height)
        {
            Rect panel = new Rect((width - 850f) * .5f, (height - 570f) * .5f, 850f, 570f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, 570f, 50f), showAchievements ? "LOGROS DE PROGRESO" : "MISIONES DIARIAS", titleStyle);
            if (GUI.Button(new Rect(panel.x + 780f, panel.y + 16f, 48f, 42f), "X", buttonStyle)) { questsOpen = false; return; }
            if (GUI.Button(new Rect(panel.x + 28f, panel.y + 76f, 220f, 44f), "DIARIAS", showAchievements ? buttonStyle : activeButtonStyle)) showAchievements = false;
            if (GUI.Button(new Rect(panel.x + 258f, panel.y + 76f, 220f, 44f), "LOGROS", showAchievements ? activeButtonStyle : buttonStyle)) showAchievements = true;
            if (!showAchievements) DrawDailyContent(panel); else DrawAchievementContent(panel);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 530f, 790f, 28f), busy ? "CARGANDO..." : message, bodyStyle);
        }

        private void DrawDailyContent(Rect panel)
        {
            int points = 0;
            foreach (var q in quests) if (q.progress >= q.target_amount) points += q.points;
            GUI.Label(new Rect(panel.x + 510f, panel.y + 78f, 300f, 34f), "PUNTOS DIARIOS  " + points + " / 100", bodyStyle);
            DrawProgress(new Rect(panel.x + 510f, panel.y + 115f, 300f, 24f), points / 100f, points + "%");
            int[] milestones = { 20, 50, 100 };
            for (int i = 0; i < milestones.Length; i++)
            {
                int milestone = milestones[i];
                bool enabled = points >= milestone && !busy;
                bool before = GUI.enabled; GUI.enabled = enabled;
                if (GUI.Button(new Rect(panel.x + 510f + i * 102f, panel.y + 147f, 92f, 38f), "COFRE " + milestone, buttonStyle)) ClaimChest(milestone);
                GUI.enabled = before;
            }
            for (int i = 0; i < quests.Length; i++) DrawQuestRow(panel, quests[i], i);
        }

        private void DrawQuestRow(Rect panel, SupabaseSyncClient.QuestCloudState q, int index)
        {
            float y = panel.y + 200f + index * 98f;
            GUI.Box(new Rect(panel.x + 28f, y, 782f, 86f), GUIContent.none, rowStyle);
            GUI.Label(new Rect(panel.x + 44f, y + 8f, 430f, 30f), q.title, bodyStyle);
            int progress = Mathf.Min(q.progress, q.target_amount);
            DrawProgress(new Rect(panel.x + 44f, y + 46f, 430f, 22f), progress / (float)q.target_amount, progress + " / " + q.target_amount);
            string reward = RewardText(q.reward_wood, q.reward_food, q.reward_crystals, q.reward_speedups);
            GUI.Label(new Rect(panel.x + 490f, y + 8f, 180f, 58f), reward + "\n+" + q.points + " puntos", bodyStyle);
            bool canClaim = progress >= q.target_amount && string.IsNullOrEmpty(q.claimed_at) && !busy;
            bool before = GUI.enabled; GUI.enabled = canClaim;
            if (GUI.Button(new Rect(panel.x + 682f, y + 20f, 112f, 46f), string.IsNullOrEmpty(q.claimed_at) ? "RECLAMAR" : "RECIBIDO", buttonStyle)) ClaimQuest(q.id);
            GUI.enabled = before;
        }

        private void DrawAchievementContent(Rect panel)
        {
            for (int i = 0; i < achievements.Length; i++)
            {
                var a = achievements[i]; float y = panel.y + 145f + i * 112f;
                GUI.Box(new Rect(panel.x + 28f, y, 782f, 98f), GUIContent.none, rowStyle);
                GUI.Label(new Rect(panel.x + 44f, y + 8f, 430f, 30f), a.title, bodyStyle);
                int progress = Mathf.Min(a.progress, a.target_amount);
                DrawProgress(new Rect(panel.x + 44f, y + 49f, 430f, 22f), progress / (float)a.target_amount, progress + " / " + a.target_amount);
                GUI.Label(new Rect(panel.x + 490f, y + 10f, 180f, 64f), RewardText(a.reward_wood, a.reward_food, a.reward_crystals, a.reward_speedups), bodyStyle);
                bool canClaim = progress >= a.target_amount && string.IsNullOrEmpty(a.claimed_at) && !busy;
                bool before = GUI.enabled; GUI.enabled = canClaim;
                if (GUI.Button(new Rect(panel.x + 682f, y + 25f, 112f, 46f), string.IsNullOrEmpty(a.claimed_at) ? "RECLAMAR" : "RECIBIDO", buttonStyle)) ClaimAchievement(a.id);
                GUI.enabled = before;
            }
        }

        private void DrawMail(float width, float height)
        {
            Rect panel = new Rect((width - 900f) * .5f, (height - 590f) * .5f, 900f, 590f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, 500f, 48f), "BUZON DE CORREO", titleStyle);
            if (GUI.Button(new Rect(panel.x + 830f, panel.y + 16f, 48f, 42f), "X", buttonStyle)) { mailOpen = false; return; }
            string[] categories = { "Battle", "Alliance", "System" };
            string[] labels = { "INFORMES DE BATALLA", "ALIANZA", "SISTEMA" };
            for (int i = 0; i < categories.Length; i++)
                if (GUI.Button(new Rect(panel.x + 28f + i * 220f, panel.y + 76f, 210f, 44f), labels[i], mailCategory == categories[i] ? activeButtonStyle : buttonStyle)) LoadMail(categories[i]);
            if (GUI.Button(new Rect(panel.x + 702f, panel.y + 76f, 170f, 44f), "RECLAMAR TODO", buttonStyle)) ClaimAllMail();

            Rect view = new Rect(panel.x + 28f, panel.y + 136f, 844f, 395f);
            float contentHeight = Mathf.Max(view.height, mail.Length * 112f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20f, contentHeight));
            for (int i = 0; i < mail.Length; i++)
            {
                var row = mail[i]; float y = i * 112f;
                GUI.Box(new Rect(0, y, view.width - 28f, 100f), GUIContent.none, rowStyle);
                string unread = string.IsNullOrEmpty(row.read_at) ? "NUEVO  " : "";
                GUI.Label(new Rect(14f, y + 7f, 500f, 30f), unread + row.subject, bodyStyle);
                GUI.Label(new Rect(14f, y + 38f, 560f, 54f), row.body, bodyStyle);
                string attachment = string.IsNullOrEmpty(row.claimed_at)
                    ? RewardText(row.reward_wood, row.reward_food, row.reward_crystals, row.reward_speedups)
                    : "RECOMPENSA RECLAMADA";
                GUI.Label(new Rect(590f, y + 8f, 190f, 54f), attachment, bodyStyle);
                if (string.IsNullOrEmpty(row.read_at) && GUI.Button(new Rect(660f, y + 61f, 120f, 32f), "MARCAR LEIDO", buttonStyle)) MarkRead(row.id);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x + 28f, panel.y + 542f, 820f, 28f), busy ? "CARGANDO..." : message, bodyStyle);
        }

        private void LoadMail(string category) { mailCategory = category; scroll = Vector2.zero; RefreshAll(); }
        private void ClaimQuest(string id) { claimingDailyQuest = true; StartReward(SupabaseSyncClient.Instance.ClaimQuest(id, RewardReceived, Error)); }
        private void ClaimAchievement(string id) => StartReward(SupabaseSyncClient.Instance.ClaimAchievement(id, RewardReceived, Error));
        private void ClaimChest(int milestone) => StartReward(SupabaseSyncClient.Instance.ClaimDailyChest(milestone, RewardReceived, Error));
        private void ClaimAllMail() => StartReward(SupabaseSyncClient.Instance.ClaimAllMail(RewardReceived, Error));
        private void MarkRead(string id) { if (!busy) StartCoroutine(SupabaseSyncClient.Instance.MarkMailRead(id, RefreshAll)); }
        private void StartReward(IEnumerator routine) { if (!busy) { busy = true; StartCoroutine(routine); } }
        private void RewardReceived(SupabaseSyncClient.RewardCloudState reward)
        {
            busy = false;
            FindAnyObjectByType<FrostboundFrontierPrototype>()?.ApplyClaimedRewards(reward.wood, reward.food, reward.crystals, reward.speedups);
            if (claimingDailyQuest) OnboardingManager.Notify("DailyRewardClaimed");
            claimingDailyQuest = false;
            message = "Recompensa recibida: " + RewardText(reward.wood, reward.food, reward.crystals, reward.speedups);
            RefreshAll();
        }
        private void Error(string error) { busy = false; claimingDailyQuest = false; message = error; }

        private static string RewardText(int wood, int food, int crystals, int speedups)
        {
            string value = string.Empty;
            if (wood > 0) value += wood + " madera  ";
            if (food > 0) value += food + " comida  ";
            if (crystals > 0) value += crystals + " cristales  ";
            if (speedups > 0) value += speedups + " aceleradores";
            return string.IsNullOrEmpty(value) ? "Sin adjuntos" : value.Trim();
        }

        private void DrawProgress(Rect rect, float value, string label)
        {
            GUI.Box(rect, GUIContent.none, progressStyle);
            Color old = GUI.color; GUI.color = new Color(.18f, .78f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(value), rect.height - 4f), accentTexture);
            GUI.color = old; GUI.Label(rect, label, activeButtonStyle);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelTexture = MakeTexture(new Color(.025f, .075f, .11f, .98f));
            rowTexture = MakeTexture(new Color(.07f, .18f, .25f, .98f));
            buttonTexture = MakeTexture(new Color(.08f, .39f, .58f, 1f));
            accentTexture = MakeTexture(Color.white);
            panelStyle = new GUIStyle(GUI.skin.box); panelStyle.normal.background = panelTexture;
            rowStyle = new GUIStyle(GUI.skin.box); rowStyle.normal.background = rowTexture;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            bodyStyle.normal.textColor = new Color(.9f, .96f, 1f);
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 27 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = buttonTexture; buttonStyle.hover.background = buttonTexture; buttonStyle.normal.textColor = Color.white; buttonStyle.hover.textColor = Color.white;
            activeButtonStyle = new GUIStyle(buttonStyle); activeButtonStyle.normal.background = accentTexture; activeButtonStyle.normal.textColor = new Color(.02f, .14f, .22f);
            progressStyle = new GUIStyle(GUI.skin.box); progressStyle.normal.background = MakeTexture(new Color(.01f, .035f, .055f, 1f));
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }
    }
}
