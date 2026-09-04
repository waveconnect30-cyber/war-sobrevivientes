using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class Hito16Manager : MonoBehaviour
    {
        public static Hito16Manager Instance { get; private set; }
        public static bool IsPanelOpen => Instance != null && Instance.panelOpen;

        private SupabaseSyncClient.AdvancedHeroState[] heroes = Array.Empty<SupabaseSyncClient.AdvancedHeroState>();
        private readonly List<string> team = new List<string>(3);
        private bool panelOpen;
        private bool expeditionTab;
        private bool busy;
        private bool battleAnimating;
        private float battleStarted;
        private int commonKeys;
        private int epicKeys;
        private int highestStage;
        private int heroXp;
        private string message = "Conectando con el Salón de Héroes...";
        private Texture2D panelTexture;
        private Texture2D cardTexture;
        private Texture2D blueTexture;
        private Texture2D orangeTexture;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle cardStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<Hito16Manager>() == null)
                new GameObject(nameof(Hito16Manager)).AddComponent<Hito16Manager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SupabaseSyncClient.Instance != null && SupabaseSyncClient.Instance.CanQueryWorld);
            Refresh();
        }

        private void Refresh()
        {
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud == null || !cloud.CanQueryWorld) return;
            StartCoroutine(cloud.InitializeHito16(state =>
            {
                commonKeys = state.common_keys; epicKeys = state.epic_keys; highestStage = state.highest_stage; heroXp = state.hero_xp;
                StartCoroutine(cloud.FetchAdvancedHeroes(rows => { heroes = rows; AutoFillTeam(); message = "Salón sincronizado"; }, Error));
            }, Error));
        }

        private void OnGUI()
        {
            if (WorldMapManager.IsWorldMapActive || OnboardingManager.BlockOtherPanels) return;
            if (!panelOpen && (AllianceManager.IsPanelOpen || ResearchManager.IsPanelOpen || QuestMailManager.IsPanelOpen || InventoryShopManager.IsPanelOpen)) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.width / 1280f, .75f, 1.35f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.depth = -120;
            if (!panelOpen)
            {
                if (GUI.Button(new Rect(465f, 148f, 160f, 40f), "EXPEDICIÓN", buttonStyle)) { panelOpen = true; Refresh(); }
                GUI.matrix = old; return;
            }

            Rect panel = new Rect(width * .5f - 455f, height * .5f - 285f, 910f, 570f);
            GUI.DrawTexture(panel, panelTexture);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, 540f, 42f), expeditionTab ? "EXPEDICIÓN GLACIAL" : "SALÓN DE HÉROES", titleStyle);
            if (GUI.Button(new Rect(panel.x + panel.width - 58f, panel.y + 16f, 42f, 40f), "×", buttonStyle)) panelOpen = false;
            if (GUI.Button(new Rect(panel.x + 28f, panel.y + 68f, 190f, 42f), "HÉROES", expeditionTab ? buttonStyle : cardStyle)) expeditionTab = false;
            if (GUI.Button(new Rect(panel.x + 228f, panel.y + 68f, 190f, 42f), "EXPEDICIÓN", expeditionTab ? cardStyle : buttonStyle)) expeditionTab = true;
            if (expeditionTab) DrawExpedition(panel); else DrawRecruitment(panel);
            GUI.Label(new Rect(panel.x + 28f, panel.y + panel.height - 44f, panel.width - 56f, 30f), message, bodyStyle);
            GUI.matrix = old;
        }

        private void DrawRecruitment(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 455f, panel.y + 74f, 400f, 34f), "LLAVES  COMUNES " + commonKeys + "   ·   ÉPICAS " + epicKeys, bodyStyle);
            float x = panel.x + 28f, y = panel.y + 128f;
            for (int i = 0; i < heroes.Length && i < 6; i++)
            {
                SupabaseSyncClient.AdvancedHeroState hero = heroes[i];
                Rect card = new Rect(x + (i % 3) * 282f, y + (i / 3) * 142f, 264f, 126f);
                GUI.DrawTexture(card, cardTexture);
                GUI.Label(new Rect(card.x + 16f, card.y + 10f, 232f, 30f), HeroName(hero.hero_key), titleStyle);
                GUI.Label(new Rect(card.x + 16f, card.y + 44f, 232f, 70f), hero.rarity.ToUpperInvariant() + " · " + RoleName(hero.hero_type) + "\nNv. " + hero.level + "  ★" + hero.star_level + "  ·  Fragmentos " + hero.shards_count, bodyStyle);
            }
            GUI.enabled = !busy && commonKeys > 0;
            if (GUI.Button(new Rect(panel.x + 150f, panel.y + 430f, 250f, 54f), "RECLUTAR · LLAVE COMÚN", buttonStyle)) Recruit("Common");
            GUI.enabled = !busy && epicKeys > 0;
            if (GUI.Button(new Rect(panel.x + 510f, panel.y + 430f, 250f, 54f), "RECLUTAR · LLAVE ÉPICA", buttonStyle)) Recruit("Epic");
            GUI.enabled = true;
            GUI.Label(new Rect(panel.x + 125f, panel.y + 492f, 660f, 28f), "Rare 80% / Epic 18% / Legendary 2%  ·  La llave épica mejora las probabilidades", bodyStyle);
        }

        private void DrawExpedition(Rect panel)
        {
            int stage = Mathf.Clamp(highestStage + 1, 1, 50);
            GUI.Label(new Rect(panel.x + 455f, panel.y + 74f, 410f, 34f), "ETAPA " + StageLabel(stage) + "  ·  XP HÉROE " + heroXp, bodyStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 126f, 350f, 34f), "EQUIPO · " + team.Count + " / 3", titleStyle);
            for (int i = 0; i < heroes.Length && i < 6; i++)
            {
                SupabaseSyncClient.AdvancedHeroState hero = heroes[i];
                bool selected = team.Contains(hero.hero_key);
                Rect rect = new Rect(panel.x + 28f, panel.y + 172f + i * 48f, 360f, 40f);
                if (GUI.Button(rect, (selected ? "✓ " : "+ ") + HeroName(hero.hero_key) + " · " + RoleName(hero.hero_type), selected ? cardStyle : buttonStyle)) ToggleHero(hero.hero_key);
            }
            Rect field = new Rect(panel.x + 420f, panel.y + 136f, 458f, 280f);
            GUI.DrawTexture(field, cardTexture);
            if (battleAnimating)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - battleStarted) / 2.6f);
                GUI.Label(new Rect(field.x + 30f, field.y + 38f, field.width - 60f, 38f), "OLEADA EN CURSO", titleStyle);
                GUI.Label(new Rect(field.x + 30f, field.y + 100f, field.width - 60f, 70f), "Tus héroes avanzan por el campo helado\n" + new string('◆', Mathf.Clamp(Mathf.CeilToInt(progress * 10f), 1, 10)), bodyStyle);
                GUI.Label(new Rect(field.x + 30f, field.y + 195f, field.width - 60f, 40f), Mathf.RoundToInt(progress * 100f) + "%", titleStyle);
            }
            else
            {
                GUI.Label(new Rect(field.x + 28f, field.y + 28f, field.width - 56f, 42f), "ENEMIGOS · PODER " + (140 + stage * 105), titleStyle);
                GUI.Label(new Rect(field.x + 28f, field.y + 92f, field.width - 56f, 120f), "VENTAJAS TÁCTICAS\nInfantería  ›  Tiradores\nTiradores  ›  Lanceros\nLanceros  ›  Infantería", bodyStyle);
            }
            GUI.enabled = !busy && !battleAnimating && team.Count > 0;
            if (GUI.Button(new Rect(panel.x + 450f, panel.y + 434f, 200f, 54f), "COMBATIR " + StageLabel(stage), buttonStyle)) StartCoroutine(BattleSequence(stage));
            GUI.enabled = !busy && !battleAnimating;
            if (GUI.Button(new Rect(panel.x + 665f, panel.y + 434f, 190f, 54f), "RECLAMAR IDLE", buttonStyle)) ClaimIdle();
            GUI.enabled = true;
        }

        private IEnumerator BattleSequence(int stage)
        {
            busy = battleAnimating = true; battleStarted = Time.unscaledTime; message = "Combate estratégico en progreso...";
            yield return new WaitForSecondsRealtime(2.6f);
            string[] selected = team.ToArray();
            yield return SupabaseSyncClient.Instance.ProcessExpedition(stage, selected, result =>
            {
                busy = battleAnimating = false;
                if (result.victory) { highestStage = Mathf.Max(highestStage, result.stage); heroXp += result.hero_xp; FindAnyObjectByType<FrostboundFrontierPrototype>()?.ApplyClaimedRewards(result.wood, result.food, 0, 0); }
                message = (result.victory ? "VICTORIA" : "DERROTA") + " · Poder " + result.team_power + " vs " + result.enemy_power + (result.victory ? " · +" + result.wood + " madera, +" + result.food + " comida" : " · Mejora tu equipo");
            }, Error);
        }

        private void Recruit(string keyType)
        {
            busy = true; message = "Abriendo cofre de reclutamiento...";
            StartCoroutine(SupabaseSyncClient.Instance.RecruitHero(keyType, result =>
            {
                busy = false; commonKeys = result.common_keys; epicKeys = result.epic_keys;
                message = result.is_new ? "¡NUEVO HÉROE! " + HeroName(result.hero_key) + " · " + result.rarity : HeroName(result.hero_key) + " · +" + result.shards_awarded + " fragmentos";
                StartCoroutine(SupabaseSyncClient.Instance.FetchAdvancedHeroes(rows => { heroes = rows; AutoFillTeam(); }, Error));
            }, Error));
        }

        private void ClaimIdle()
        {
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.ClaimExpeditionIdle(result =>
            {
                busy = false; heroXp += result.hero_xp;
                FindAnyObjectByType<FrostboundFrontierPrototype>()?.ApplyClaimedRewards(result.wood, result.food, 0, 0);
                message = "Producción de " + Mathf.FloorToInt(result.seconds / 60f) + " min · +" + result.wood + " madera · +" + result.food + " comida · +" + result.hero_xp + " XP";
            }, Error));
        }

        private void ToggleHero(string key)
        {
            if (team.Contains(key)) team.Remove(key); else if (team.Count < 3) team.Add(key); else message = "El equipo admite un máximo de 3 héroes";
        }

        private void AutoFillTeam()
        {
            team.RemoveAll(key => Array.Find(heroes, h => h.hero_key == key) == null);
            for (int i = 0; i < heroes.Length && team.Count < 3; i++) if (!team.Contains(heroes[i].hero_key)) team.Add(heroes[i].hero_key);
        }

        private void Error(string error) { busy = battleAnimating = false; message = error; }
        private static string StageLabel(int value) => ((value - 1) / 10 + 1) + "-" + ((value - 1) % 10 + 1);
        private static string RoleName(string value) => value == "Lancer" ? "Lancero" : value == "Marksman" ? "Tirador" : "Infantería";
        private static string HeroName(string key)
        {
            if (key == "elena_ice_huntress") return "Elena";
            if (key == "kael_frost_guardian") return "Kael";
            if (key == "nyra_aurora_spear") return "Nyra";
            if (key == "boris_snow_lancer") return "Boris";
            if (key == "mira_winter_shield") return "Mira";
            if (key == "orin_frost_marksman") return "Orin";
            return key.Replace('_', ' ').ToUpperInvariant();
        }

        private void EnsureStyles()
        {
            if (panelTexture != null) return;
            panelTexture = Solid(new Color(.025f, .09f, .15f, .99f)); cardTexture = Solid(new Color(.08f, .24f, .34f, .98f));
            blueTexture = Solid(new Color(.04f, .48f, .72f)); orangeTexture = Solid(new Color(.95f, .43f, .08f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            bodyStyle = new GUIStyle(titleStyle) { fontSize = 15, fontStyle = FontStyle.Normal, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { background = blueTexture, textColor = Color.white }, hover = { background = orangeTexture, textColor = Color.white }, active = { background = orangeTexture, textColor = Color.white } };
            FrostboundVisualTheme.ApplyButton(buttonStyle);
            cardStyle = new GUIStyle(buttonStyle) { normal = { background = orangeTexture, textColor = Color.white } };
        }

        private static Texture2D Solid(Color color) { Texture2D t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, color); t.Apply(); return t; }
    }
}
