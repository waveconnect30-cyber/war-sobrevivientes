using System;
using System.Collections;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class AllianceManager : MonoBehaviour
    {
        private const int CreationCost = 500;
        private const string TagKey = "frostbound-alliance-tag";
        public static AllianceManager Instance { get; private set; }
        public static string LocalTag { get; private set; } = string.Empty;
        public static bool IsPanelOpen => Instance != null && Instance.panelOpen;
        public static bool HasAlliance => Instance != null && Instance.alliance != null;
        public static bool CanBuildTerritory => Instance != null && Instance.alliance != null &&
            (Instance.alliance.member_role == "Leader" || Instance.alliance.member_role == "Officer");
        public static float ResourceProductionMultiplier => 1f + (Instance?.buffs?.resource_bonus ?? 0f) / 100f;
        public static float AttackMultiplier => 1f + (Instance?.buffs?.attack_bonus ?? 0f) / 100f;

        private SupabaseSyncClient.AllianceCloudState alliance;
        private SupabaseSyncClient.AllianceSearchRow[] searchResults = Array.Empty<SupabaseSyncClient.AllianceSearchRow>();
        private SupabaseSyncClient.AllianceHelpRow[] helpRequests = Array.Empty<SupabaseSyncClient.AllianceHelpRow>();
        private SupabaseSyncClient.RallyCloudState[] rallies = Array.Empty<SupabaseSyncClient.RallyCloudState>();
        private SupabaseSyncClient.AllianceBuffCloudState buffs = new SupabaseSyncClient.AllianceBuffCloudState();
        private bool panelOpen;
        private bool busy;
        private string allianceName = "Guardianes del Hielo";
        private string allianceTag = "HIE";
        private string search = string.Empty;
        private string message = "Crea una alianza o únete a otros supervivientes.";
        private Vector2 scroll;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle fieldStyle;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private bool stylesReady;
        private int rallyTroops = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AllianceManager>() == null)
                new GameObject(nameof(AllianceManager)).AddComponent<AllianceManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            LocalTag = PlayerPrefs.GetString(TagKey, string.Empty);
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SupabaseSyncClient.Instance != null && SupabaseSyncClient.Instance.CanQueryWorld);
            RefreshAlliance();
        }

        public void RequestHelp(string targetType, string targetKey)
        {
            if (busy) return;
            if (alliance == null) { panelOpen = true; message = "Únete a una alianza para pedir ayuda."; return; }
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.RequestAllianceHelp(targetType, targetKey,
                () => { busy = false; message = "Solicitud enviada a tu alianza."; },
                error => { busy = false; message = error; }));
        }

        private void RefreshAlliance()
        {
            if (busy || SupabaseSyncClient.Instance == null) return;
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.FetchMyAlliance(result =>
            {
                busy = false; alliance = result; LocalTag = result?.tag ?? string.Empty;
                PlayerPrefs.SetString(TagKey, LocalTag); PlayerPrefs.Save();
                if (alliance != null) { RefreshHelp(); RefreshRallies(); RefreshBuffs(); }
            }, error => { busy = false; message = error; }));
        }

        private void RefreshHelp()
        {
            if (SupabaseSyncClient.Instance == null || alliance == null) return;
            StartCoroutine(SupabaseSyncClient.Instance.FetchAllianceHelp(rows => helpRequests = rows, error => message = error));
        }

        private void RefreshRallies()
        {
            if (SupabaseSyncClient.Instance == null || alliance == null) return;
            StartCoroutine(SupabaseSyncClient.Instance.FetchAllianceRallies(rows => rallies = rows, error => message = error));
        }

        private void RefreshBuffs()
        {
            if (SupabaseSyncClient.Instance == null || alliance == null) return;
            StartCoroutine(SupabaseSyncClient.Instance.FetchAllianceBuffs(result => buffs = result ?? new SupabaseSyncClient.AllianceBuffCloudState(), error => message = error));
        }

        public void CreateRallyAt(int x, int y, string targetType, int troops)
        {
            panelOpen = true;
            if (alliance == null) { message = "Necesitas una alianza para iniciar un rally."; return; }
            if (busy) return;
            busy = true;
            message = "Creando rally de 5 minutos...";
            StartCoroutine(SupabaseSyncClient.Instance.CreateAllianceRally(x, y, targetType, troops, result =>
            {
                busy = false;
                message = "Rally creado contra X:" + result.target_x + " Y:" + result.target_y + ".";
                RefreshRallies();
            }, error => { busy = false; message = error; }));
        }

        private void OnGUI()
        {
            if (ResearchManager.IsPanelOpen) return;
            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = 100;
            float scale = Mathf.Clamp(Screen.width / 1280f, .75f, 1.35f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            string label = string.IsNullOrEmpty(LocalTag) ? "⚑ ALIANZA" : "[" + LocalTag + "] ALIANZA";
            if (GUI.Button(new Rect(292f, 98f, 170f, 42f), label, buttonStyle)) panelOpen = !panelOpen;
            if (panelOpen) DrawPanel(width, height);
            GUI.matrix = old;
            GUI.depth = previousDepth;
        }

        private void DrawPanel(float width, float height)
        {
            Rect panel = new Rect(width * .5f - 360f, height * .5f - 245f, 720f, 490f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, 560f, 44f), "ALIANZA DE SUPERVIVIENTES", titleStyle);
            if (GUI.Button(new Rect(panel.x + 650f, panel.y + 16f, 48f, 42f), "×", buttonStyle)) { panelOpen = false; return; }
            GUI.Label(new Rect(panel.x + 28f, panel.y + 66f, panel.width - 56f, 40f), FriendlyMessage(message), bodyStyle);
            if (alliance == null) DrawDiscovery(panel); else DrawAllianceHome(panel);
        }

        private void DrawDiscovery(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 28f, panel.y + 116f, 310f, 32f), "CREAR ALIANZA · 500 CRISTALES", titleStyle);
            allianceName = GUI.TextField(new Rect(panel.x + 28f, panel.y + 154f, 310f, 42f), allianceName, 32, fieldStyle);
            allianceTag = GUI.TextField(new Rect(panel.x + 28f, panel.y + 204f, 112f, 42f), allianceTag.ToUpperInvariant(), 3, fieldStyle);
            GUI.enabled = !busy && allianceName.Trim().Length >= 3 && allianceTag.Trim().Length == 3;
            if (GUI.Button(new Rect(panel.x + 152f, panel.y + 204f, 186f, 42f), "CREAR", buttonStyle)) CreateAlliance();
            GUI.enabled = true;

            GUI.Label(new Rect(panel.x + 374f, panel.y + 116f, 310f, 32f), "BUSCAR Y UNIRSE", titleStyle);
            search = GUI.TextField(new Rect(panel.x + 374f, panel.y + 154f, 206f, 42f), search, 32, fieldStyle);
            if (GUI.Button(new Rect(panel.x + 590f, panel.y + 154f, 94f, 42f), "BUSCAR", buttonStyle)) Search();
            scroll = GUI.BeginScrollView(new Rect(panel.x + 374f, panel.y + 208f, 310f, 238f), scroll, new Rect(0f, 0f, 286f, Mathf.Max(220f, searchResults.Length * 58f)));
            for (int i = 0; i < searchResults.Length; i++)
            {
                SupabaseSyncClient.AllianceSearchRow row = searchResults[i];
                GUI.Label(new Rect(0f, i * 58f, 178f, 50f), "[" + row.tag + "] " + row.name + "\nPoder " + row.power_total, bodyStyle);
                if (GUI.Button(new Rect(184f, i * 58f + 5f, 96f, 40f), "UNIRME", buttonStyle)) Join(row.id);
            }
            GUI.EndScrollView();
        }

        private void DrawAllianceHome(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 28f, panel.y + 116f, 664f, 46f), "[" + alliance.tag + "]  " + alliance.name, titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 164f, 664f, 34f), "Rango: " + alliance.member_role + "  ·  Poder: " + alliance.power_total, bodyStyle);
            GUI.enabled = CanBuildTerritory && !busy;
            if (GUI.Button(new Rect(panel.x + 28f, panel.y + 202f, 150f, 40f), "DESPLEGAR HQ", buttonStyle))
            {
                panelOpen = false;
                FindAnyObjectByType<WorldMapManager>()?.BeginAllianceHQPlacement();
            }
            if (GUI.Button(new Rect(panel.x + 184f, panel.y + 202f, 150f, 40f), "BANDERA +", buttonStyle))
            {
                panelOpen = false;
                FindAnyObjectByType<WorldMapManager>()?.BeginAllianceFlagPlacement();
            }
            GUI.enabled = true;
            rallyTroops = Mathf.Clamp(rallyTroops, 1, 9999);
            GUI.Label(new Rect(panel.x + 346f, panel.y + 202f, 210f, 40f), "BUFFS +" + buffs.resource_bonus + "% REC · +" + buffs.attack_bonus + "% ATQ", bodyStyle);
            if (GUI.Button(new Rect(panel.x + 566f, panel.y + 202f, 126f, 40f), "ACTUALIZAR", buttonStyle)) { RefreshHelp(); RefreshRallies(); RefreshBuffs(); }
            GUI.Label(new Rect(panel.x + 28f, panel.y + 248f, 316f, 30f), "RALLIES ACTIVOS · 5 MIN", titleStyle);
            GUI.Label(new Rect(panel.x + 376f, panel.y + 248f, 260f, 30f), "AYUDAS DE TEMPORIZADOR", titleStyle);
            scroll = GUI.BeginScrollView(new Rect(panel.x + 28f, panel.y + 282f, 316f, 160f), scroll, new Rect(0f, 0f, 292f, Mathf.Max(144f, rallies.Length * 64f)));
            for (int i = 0; i < rallies.Length; i++)
            {
                SupabaseSyncClient.RallyCloudState row = rallies[i];
                TimeSpan left = DateTime.TryParse(row.rally_starts_at, out DateTime starts) ? starts.ToUniversalTime() - DateTime.UtcNow : TimeSpan.Zero;
                string timer = Mathf.Max(0, Mathf.CeilToInt((float)left.TotalSeconds / 60f)) + " min";
                GUI.Label(new Rect(0f, i * 64f, 178f, 56f), (row.target_type == "EliteBeast" ? "BESTIA ELITE" : "INSTALACIÓN") +
                    "\nX:" + row.target_x + " Y:" + row.target_y + " · " + timer, bodyStyle);
                bool expiredFacility = row.target_type == "Facility" && left <= TimeSpan.Zero;
                GUI.enabled = !busy && (expiredFacility || row.leader_id != SupabaseSyncClient.Instance.CurrentUserId);
                string rallyAction = expiredFacility ? "RESOLVER" : "UNIR " + rallyTroops;
                if (GUI.Button(new Rect(184f, i * 64f + 7f, 100f, 42f), rallyAction, buttonStyle))
                {
                    if (expiredFacility) ProcessFacilityRally(row.id); else JoinRally(row.id);
                }
                GUI.enabled = true;
            }
            if (rallies.Length == 0) GUI.Label(new Rect(0f, 8f, 286f, 50f), "Selecciona una Bestia Nv.2 o Fortaleza y pulsa RALLY.", bodyStyle);
            GUI.EndScrollView();
            Vector2 helpScroll = Vector2.zero;
            helpScroll = GUI.BeginScrollView(new Rect(panel.x + 376f, panel.y + 282f, 316f, 160f), helpScroll, new Rect(0f, 0f, 292f, Mathf.Max(144f, helpRequests.Length * 58f)));
            int visibleIndex = 0;
            for (int i = 0; i < helpRequests.Length; i++)
            {
                SupabaseSyncClient.AllianceHelpRow row = helpRequests[i];
                if (row.requester_id == SupabaseSyncClient.Instance.CurrentUserId) continue;
                float y = visibleIndex++ * 58f;
                string target = row.target_type == "HospitalHealing" ? "Curación en Enfermería" :
                    row.target_type == "Research" ? "Investigación: " + row.target_key : "Mejora: " + row.target_key.Replace("_01", "");
                GUI.Label(new Rect(0f, y, 180f, 50f), target + "  ·  " + row.help_count, bodyStyle);
                if (GUI.Button(new Rect(190f, y + 5f, 94f, 40f), "AYUDAR", buttonStyle)) GiveHelp(row.id);
            }
            if (visibleIndex == 0) GUI.Label(new Rect(0f, 12f, 620f, 44f), "No hay compañeros esperando ayuda.", bodyStyle);
            GUI.EndScrollView();
        }

        private void JoinRally(string rallyId)
        {
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.JoinAllianceRally(rallyId, rallyTroops, result =>
            {
                busy = false;
                message = result.status == "Ready" ? "Tus tropas están listas en el rally." : "Tus tropas marchan hacia la ciudad líder.";
                RefreshRallies();
            }, error => { busy = false; message = error; }));
        }

        private void ProcessFacilityRally(string rallyId)
        {
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.ProcessFacilityRally(rallyId, result =>
            {
                busy = false;
                message = result.victory
                    ? "¡INSTALACIÓN CONQUISTADA! +" + result.buff_percent + "% " + (result.buff_type == "AllianceAttack" ? "ATAQUE" : "PRODUCCIÓN")
                    : "Rally derrotado: " + result.combined_power + " / " + result.defense_power + " de poder.";
                RefreshRallies(); RefreshBuffs();
                FindAnyObjectByType<WorldMapManager>()?.ForceWorldRefresh();
            }, error => { busy = false; message = error; }));
        }

        private void CreateAlliance()
        {
            busy = true; message = "Creando alianza...";
            StartCoroutine(SupabaseSyncClient.Instance.CreateAlliance(allianceName.Trim(), allianceTag.Trim().ToUpperInvariant(), result =>
            {
                busy = false; alliance = result; SetTag(result.tag); message = "Alianza creada correctamente.";
                FindAnyObjectByType<FrostboundFrontierPrototype>()?.SpendCrystalsLocally(result.crystal_cost > 0 ? result.crystal_cost : CreationCost);
                RefreshHelp();
            }, error => { busy = false; message = error; }));
        }

        private void Search()
        {
            busy = true; message = "Buscando alianzas...";
            StartCoroutine(SupabaseSyncClient.Instance.SearchAlliances(search, rows => { busy = false; searchResults = rows; message = rows.Length + " alianzas encontradas."; }, error => { busy = false; message = error; }));
        }

        private void Join(string id)
        {
            busy = true; message = "Uniéndote a la alianza...";
            StartCoroutine(SupabaseSyncClient.Instance.JoinAlliance(id, result => { busy = false; alliance = result; SetTag(result.tag); message = "Ahora perteneces a [" + result.tag + "]."; RefreshHelp(); }, error => { busy = false; message = error; }));
        }

        private void GiveHelp(string id)
        {
            busy = true;
            StartCoroutine(SupabaseSyncClient.Instance.GiveAllianceHelp(id, () => { busy = false; message = "Ayuda enviada: -1% o hasta 1 minuto."; RefreshHelp(); }, error => { busy = false; message = error; }));
        }

        private static void SetTag(string tag)
        {
            LocalTag = tag ?? string.Empty; PlayerPrefs.SetString(TagKey, LocalTag); PlayerPrefs.Save();
        }

        private static string FriendlyMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Contains("schema cache") || value.Contains("Could not find")) return "Supabase está actualizando el esquema. Pulsa ACTUALIZAR en unos segundos.";
            return value.Length <= 110 ? value : value.Substring(0, 107) + "...";
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            panelTexture = MakeTexture(new Color(.025f, .075f, .105f, .98f));
            buttonTexture = MakeTexture(new Color(.08f, .42f, .65f, 1f));
            panelStyle = new GUIStyle(GUI.skin.box); panelStyle.normal.background = panelTexture;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; titleStyle.normal.textColor = new Color(.78f, .94f, 1f);
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft, wordWrap = true }; bodyStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; buttonStyle.normal.background = buttonTexture; buttonStyle.hover.background = buttonTexture; buttonStyle.normal.textColor = Color.white;
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 8, 8) };
            stylesReady = true;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }
    }
}
