using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FrostboundFrontier
{
    public sealed class SupabaseSyncClient : MonoBehaviour
    {
        private const string ProjectUrl = "https://qbqfysphnotknygiurnj.supabase.co";
        private const string PublishableKey = "sb_publishable_eGBJKJ4m5h5HNKzCehcVdQ_jY-W13ai";
        private const string AccessTokenKey = "frostbound-supabase-access-token";
        private const string RefreshTokenKey = "frostbound-supabase-refresh-token";
        private const string UserIdKey = "frostbound-supabase-user-id";

        public static SupabaseSyncClient Instance { get; private set; }
        public static string Status { get; private set; } = "NUBE: INICIANDO";
        public bool CanQueryWorld => HasSession;
        public string CurrentUserId => userId;

        private string accessToken;
        private string refreshToken;
        private string userId;
        private bool requestInProgress;
        private float syncClock;

        [Serializable] private sealed class AuthUser { public string id; }
        [Serializable] private sealed class AuthResponse { public string access_token; public string refresh_token; public AuthUser user; }
        [Serializable] private sealed class AnonymousMetadata { public string game = "frostbound_frontier"; }
        [Serializable] private sealed class AnonymousRequest { public AnonymousMetadata data = new AnonymousMetadata(); }
        [Serializable] private sealed class RefreshRequest { public string refresh_token; }
        [Serializable] private sealed class EmergencySavePayload { public string user_id; public string save_json; public long client_saved_at; }
        [Serializable] private sealed class RelationalPlayerPayload
        {
            public string user_id;
            public string display_name;
            public float temperature;
            public int population;
            public long wood;
            public long food;
            public long coal;
            public int generator_level;
            public float health;
            public float happiness;
            public long power;
            public long client_saved_at;
            public int snow_infantry;
            public int snow_lancers;
            public int snow_marksmen;
            public long crystals;
            public int speedups;
        }
        [Serializable] private sealed class RelationalPlayerRow
        {
            public string display_name;
            public float temperature;
            public int population;
            public long wood;
            public long food;
            public long coal;
            public int generator_level;
            public float health;
            public float happiness;
            public long power;
            public long client_saved_at;
            public int snow_infantry;
            public int snow_lancers;
            public int snow_marksmen;
            public long crystals;
            public int speedups;
        }
        [Serializable] private sealed class RelationalPlayerRows { public RelationalPlayerRow[] items; }
        [Serializable] private sealed class RelationalBuildingRow
        {
            public string slot_id;
            public string building_type;
            public int level;
            public int assigned_workers;
            public string upgrade_started_at;
            public string finishes_at;
            public float pos_x;
            public float pos_z;
        }
        [Serializable] private sealed class RelationalBuildingRows { public RelationalBuildingRow[] items; }
        [Serializable] public sealed class MarchCloudState
        {
            public string id;
            public int origin_x;
            public int origin_y;
            public int target_x;
            public int target_y;
            public string march_type;
            public string res_type;
            public int payload_amount;
            public string departure_time;
            public string arrival_time;
            public string status;
            public int troop_count;
            public string hero_id;
            public string hero_key;
            public float hero_power_bonus;
            public float hero_speed_bonus;
        }
        [Serializable] public sealed class BeastBattleResult
        {
            public bool victory;
            public int casualties;
            public int wounded;
            public string loot_type;
            public int loot_amount;
            public int power_used;
        }
        [Serializable] public sealed class HeroCloudState
        {
            public string hero_id;
            public string hero_key;
            public int level;
            public int star_level;
            public float power_bonus;
            public float march_speed_bonus;
        }
        [Serializable] public sealed class HospitalCloudState
        {
            public int wounded;
            public int healing_amount;
            public string healing_started_at;
            public string healing_finishes_at;
            public int food_cost;
            public int completed;
        }
        [Serializable] public sealed class AllianceCloudState
        {
            public string alliance_id;
            public string name;
            public string tag;
            public string member_role;
            public int member_count;
            public long power_total;
            public int crystal_cost;
        }
        [Serializable] public sealed class AllianceSearchRow
        {
            public string id;
            public string name;
            public string tag;
            public long power_total;
        }
        [Serializable] public sealed class AllianceHelpRow
        {
            public string id;
            public string requester_id;
            public string target_type;
            public string target_key;
            public int help_count;
            public string created_at;
        }
        [Serializable] private sealed class AllianceSearchRows { public AllianceSearchRow[] items; }
        [Serializable] private sealed class AllianceHelpRows { public AllianceHelpRow[] items; }
        [Serializable] private sealed class CreateAllianceRequest { public string p_name; public string p_tag; }
        [Serializable] private sealed class JoinAllianceRequest { public string p_alliance_id; }
        [Serializable] private sealed class RequestHelpPayload { public string p_target_type; public string p_target_key; }
        [Serializable] private sealed class HelpActionPayload { public string help_id; public string helper_id; }
        [Serializable] public sealed class ResearchCloudState
        {
            public string tech_id;
            public string branch;
            public int level;
            public int target_level;
            public string status;
            public string research_started_at;
            public string finishes_at;
            public int wood_cost;
            public int food_cost;
            public int crystal_cost;
        }
        [Serializable] private sealed class ResearchRows { public ResearchCloudState[] items; }
        [Serializable] private sealed class ResearchRequest { public string p_tech_id; }
        [Serializable] public sealed class AllianceStructureCloudState
        {
            public string id;
            public string alliance_id;
            public string structure_type;
            public int x;
            public int y;
            public string status;
            public int territory_radius;
            public string created_by;
            public string updated_at;
        }
        [Serializable] public sealed class RallyCloudState
        {
            public string id;
            public string alliance_id;
            public string leader_id;
            public int target_x;
            public int target_y;
            public string target_type;
            public string status;
            public string rally_starts_at;
            public int member_count;
            public int troop_total;
        }
        [Serializable] public sealed class RallyJoinCloudState
        {
            public string rally_id;
            public int origin_x;
            public int origin_y;
            public int destination_x;
            public int destination_y;
            public int troop_count;
            public string status;
            public string departure_time;
            public string arrival_time;
        }
        [Serializable] public sealed class AllianceBuffCloudState
        {
            public float resource_bonus;
            public float attack_bonus;
            public int facility_count;
        }
        [Serializable] public sealed class FacilityRallyResult
        {
            public string rally_id;
            public bool victory;
            public int combined_power;
            public int defense_power;
            public int member_count;
            public string facility_key;
            public string buff_type;
            public float buff_percent;
            public string alliance_id;
        }
        [Serializable] public sealed class QuestCloudState
        {
            public string id; public string quest_key; public string title; public string objective_type;
            public int target_amount; public int progress; public int points;
            public int reward_wood; public int reward_food; public int reward_crystals; public int reward_speedups;
            public string claimed_at;
        }
        [Serializable] public sealed class AchievementCloudState
        {
            public string id; public string achievement_key; public string title; public string objective_type;
            public int target_amount; public int progress;
            public int reward_wood; public int reward_food; public int reward_crystals; public int reward_speedups;
            public string claimed_at;
        }
        [Serializable] public sealed class MailCloudState
        {
            public string id; public string category; public string subject; public string body; public string source_key;
            public int reward_wood; public int reward_food; public int reward_crystals; public int reward_speedups;
            public string read_at; public string claimed_at; public string created_at;
        }
        [Serializable] public sealed class RewardCloudState
        {
            public int claimed_count; public int wood; public int food; public int crystals; public int speedups;
        }
        [Serializable] private sealed class QuestRows { public QuestCloudState[] items; }
        [Serializable] private sealed class AchievementRows { public AchievementCloudState[] items; }
        [Serializable] private sealed class MailRows { public MailCloudState[] items; }
        [Serializable] private sealed class ObjectiveRequest { public string p_objective_type; public int p_amount; }
        [Serializable] private sealed class IdRequest { public string p_quest_id; public string p_achievement_id; public string p_mail_id; }
        [Serializable] private sealed class ChestRequest { public int p_milestone; }
        [Serializable] private sealed class BattleMailRequest { public string p_source_key; public string p_subject; public string p_body; }
        [Serializable] public sealed class InventoryRow { public string item_id; public int quantity; public string updated_at; }
        [Serializable] public sealed class ShopRow { public string item_id; public string display_name; public string category; public int honor_cost; public int quantity_per_purchase; }
        [Serializable] public sealed class ItemActionResult { public string item_id; public int quantity; public int honor_points; public int seconds_applied; public string target_type; public int donated; public string resource; public string peace_shield_until; }
        [Serializable] public sealed class CityAttackResult { public bool victory; public int attacker_power; public int defender_power; public int attacker_casualties; public int defender_casualties; public int defender_wounded; public int loot_wood; public int loot_food; public int loot_coal; public int city_health; public bool burning; public bool relocated; public int new_x; public int new_y; }
        [Serializable] public sealed class TutorialProgressState { public string user_id; public int step; public bool completed; public string updated_at; }
        [Serializable] public sealed class Hito16State { public int common_keys; public int epic_keys; public int highest_stage; public int hero_xp; public string idle_claimed_at; }
        [Serializable] public sealed class AdvancedHeroState { public string id; public string hero_id; public string hero_key; public string hero_type; public string rarity; public int level; public int star_level; public int shards_count; public bool is_new; public int shards_awarded; public int common_keys; public int epic_keys; }
        [Serializable] public sealed class ExpeditionBattleState { public bool victory; public int stage; public int team_power; public int enemy_power; public int wood; public int food; public int hero_xp; }
        [Serializable] public sealed class ExpeditionIdleState { public int wood; public int food; public int hero_xp; public int seconds; public int highest_stage; }
        [Serializable] private sealed class AdvancedHeroRows { public AdvancedHeroState[] items; }
        [Serializable] private sealed class TutorialProgressRows { public TutorialProgressState[] items; }
        [Serializable] private sealed class InventoryRows { public InventoryRow[] items; }
        [Serializable] private sealed class ShopRows { public ShopRow[] items; }
        [Serializable] private sealed class ItemRequest { public string p_item_id; public string p_target_type; public string p_target_key; }
        [Serializable] private sealed class BuyItemRequest { public string p_item_id; }
        [Serializable] private sealed class DonationRequest { public string p_resource; public int p_amount; }
        [Serializable] private sealed class AllianceStructureRows { public AllianceStructureCloudState[] items; }
        [Serializable] private sealed class RallyRows { public RallyCloudState[] items; }
        [Serializable] private sealed class PlaceStructureRequest { public string p_structure_type; public int p_x; public int p_y; }
        [Serializable] private sealed class CreateRallyRequest { public int p_target_x; public int p_target_y; public string p_target_type; public int p_troop_count; }
        [Serializable] private sealed class JoinRallyRequest { public string p_rally_id; public int p_troop_count; }
        [Serializable] private sealed class ProcessFacilityRallyRequest { public string p_rally_id; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Instance == null) new GameObject(nameof(SupabaseSyncClient)).AddComponent<SupabaseSyncClient>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            accessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
            refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            userId = PlayerPrefs.GetString(UserIdKey, string.Empty);
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => FindAnyObjectByType<FrostboundFrontierPrototype>() != null);
            if (HasSession) yield return LoadRelationalThenSync();
            else yield return SignInAnonymously();
        }

        private void Update()
        {
            if (!HasSession || requestInProgress) return;
            syncClock += Time.unscaledDeltaTime;
            if (syncClock < 15f) return;
            syncClock = 0f;
            StartCoroutine(SyncRelationalAndBackup());
        }

        private bool HasSession => !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(userId);

        public IEnumerator FetchWorldTiles(int minX, int maxX, int minY, int maxY, Action<string> onSuccess, Action<string> onError)
        {
            if (!HasSession)
            {
                onError?.Invoke("Sin sesión: mostrando terreno local");
                yield break;
            }

            string path = "/rest/v1/frostbound_world_tiles?select=id,x,y,tile_type,occupant_id,level,res_type,res_capacity,res_remaining,beast_kind,beast_power,beast_hp,beast_max_hp,reward_type,reward_amount,facility_key,facility_power,owner_alliance_id,buff_type,buff_percent,peace_shield_until,city_health,burning_until,updated_at" +
                "&x=gte." + minX + "&x=lte." + maxX + "&y=gte." + minY + "&y=lte." + maxY +
                "&order=x.asc,y.asc";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(request.downloadHandler.text);
            else onError?.Invoke("Mapa remoto: " + request.responseCode);
        }

        public IEnumerator FetchTutorialProgress(Action<TutorialProgressState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_tutorial_progress?select=user_id,step,completed,updated_at&user_id=eq." + userId + "&limit=1", UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Tutorial " + request.responseCode + ": " + SafeError(request)); yield break; }
            TutorialProgressRows rows = ParseArray<TutorialProgressRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items != null && rows.items.Length > 0 ? rows.items[0] : null);
        }

        public IEnumerator SaveTutorialProgress(int step, bool completed, Action onSuccess = null, Action<string> onError = null)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            TutorialProgressState payload = new TutorialProgressState { user_id = userId, step = Mathf.Clamp(step, 0, 5), completed = completed, updated_at = DateTime.UtcNow.ToString("O") };
            using UnityWebRequest request = CreateUpsert("/rest/v1/frostbound_tutorial_progress?on_conflict=user_id", JsonUtility.ToJson(payload));
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(); else onError?.Invoke("Tutorial " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator InitializeHito16(Action<Hito16State> onSuccess, Action<string> onError)
        {
            yield return CallHito16Rpc("frostbound_initialize_hito16", "{}", onSuccess, onError);
        }

        public IEnumerator FetchAdvancedHeroes(Action<AdvancedHeroState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_heroes?select=id,hero_key,hero_type,rarity,level,star_level,shards_count&user_id=eq." + userId + "&order=rarity.desc,hero_key";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Héroes " + request.responseCode + ": " + SafeError(request)); yield break; }
            AdvancedHeroRows rows = ParseArray<AdvancedHeroRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<AdvancedHeroState>());
        }

        public IEnumerator RecruitHero(string keyType, Action<AdvancedHeroState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_recruit_hero", UnityWebRequest.kHttpVerbPOST, "{\"p_key_type\":\"" + keyType + "\"}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<AdvancedHeroState>(request.downloadHandler.text));
            else onError?.Invoke("Reclutamiento " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator ProcessExpedition(int stage, string[] teamKeys, Action<ExpeditionBattleState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            StringBuilder json = new StringBuilder("{\"p_stage\":").Append(stage).Append(",\"p_team_keys\":[");
            for (int i = 0; i < teamKeys.Length; i++) { if (i > 0) json.Append(','); json.Append('"').Append(teamKeys[i]).Append('"'); }
            json.Append("]}");
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_process_expedition", UnityWebRequest.kHttpVerbPOST, json.ToString(), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<ExpeditionBattleState>(request.downloadHandler.text));
            else onError?.Invoke("Expedición " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator ClaimExpeditionIdle(Action<ExpeditionIdleState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_claim_expedition_idle", UnityWebRequest.kHttpVerbPOST, "{}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<ExpeditionIdleState>(request.downloadHandler.text));
            else onError?.Invoke("Recompensa idle " + request.responseCode + ": " + SafeError(request));
        }

        private IEnumerator CallHito16Rpc(string rpc, string json, Action<Hito16State> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/" + rpc, UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<Hito16State>(request.downloadHandler.text));
            else onError?.Invoke("Hito 16 " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator RelocateWorldCity(int targetX, int targetY, Action onSuccess, Action<string> onError)
        {
            if (!HasSession)
            {
                onError?.Invoke("Sin sesión de Supabase");
                yield break;
            }

            string json = "{\"p_target_x\":" + targetX + ",\"p_target_y\":" + targetY + "}";
            using UnityWebRequest request = CreateRequest(
                "/rest/v1/rpc/frostbound_relocate_city",
                UnityWebRequest.kHttpVerbPOST,
                json,
                true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke();
            else onError?.Invoke("Supabase " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator SaveMarch(MarchCloudState march, Action onSuccess, Action<string> onError)
        {
            if (!HasSession)
            {
                onError?.Invoke("Sin sesión de Supabase");
                yield break;
            }
            string json = JsonUtility.ToJson(march);
            if (string.IsNullOrWhiteSpace(march.res_type)) json = json.Replace("\"res_type\":\"\"", "\"res_type\":null");
            if (string.IsNullOrWhiteSpace(march.hero_id)) json = json.Replace("\"hero_id\":\"\"", "\"hero_id\":null");
            json = json.Insert(1, "\"user_id\":\"" + userId + "\",");
            using UnityWebRequest request = CreateUpsert("/rest/v1/frostbound_marches?on_conflict=id", json);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke();
            else onError?.Invoke("Marcha remota " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator CompleteGatherMarch(string marchId, Action<int> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string json = "{\"p_march_id\":\"" + marchId + "\"}";
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_complete_gather_march", UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request) && int.TryParse(request.downloadHandler.text, out int delivered)) onSuccess?.Invoke(delivered);
            else onError?.Invoke("Finalización atómica " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator ProcessBeastBattle(string marchId, Action<BeastBattleResult> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string json = "{\"p_march_id\":\"" + marchId + "\"}";
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_process_beast_battle", UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<BeastBattleResult>(request.downloadHandler.text));
            else onError?.Invoke("Batalla PVE " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator ProcessCityAttack(string marchId,Action<CityAttackResult> onSuccess,Action<string> onError)
        {
            if(!HasSession){onError?.Invoke("Sin sesión de Supabase");yield break;}
            using UnityWebRequest request=CreateRequest("/rest/v1/rpc/frostbound_process_city_attack",UnityWebRequest.kHttpVerbPOST,"{\"p_march_id\":\""+marchId+"\"}",true);yield return request.SendWebRequest();
            if(IsSuccess(request))onSuccess?.Invoke(JsonUtility.FromJson<CityAttackResult>(request.downloadHandler.text));else onError?.Invoke("Ataque PVP "+request.responseCode+": "+SafeError(request));
        }

        public IEnumerator InitializeHito6(Action<HeroCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_initialize_hito6", UnityWebRequest.kHttpVerbPOST, "{}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<HeroCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Inicialización Hito 6 " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchHospital(Action<HospitalCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_hospital?select=wounded_infantry,healing_amount,healing_started_at,healing_finishes_at&limit=1", UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Enfermería " + request.responseCode + ": " + SafeError(request)); yield break; }
            string json = request.downloadHandler.text.Replace("wounded_infantry", "wounded");
            HospitalRows rows = ParseArray<HospitalRows>(json);
            onSuccess?.Invoke(rows?.items != null && rows.items.Length > 0 ? rows.items[0] : new HospitalCloudState());
        }

        [Serializable] private sealed class HospitalRows { public HospitalCloudState[] items; }

        public IEnumerator StartHealing(int amount, Action<HospitalCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_start_healing", UnityWebRequest.kHttpVerbPOST, "{\"p_amount\":" + amount + "}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<HospitalCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Curación " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator CompleteHealing(Action<HospitalCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_complete_healing", UnityWebRequest.kHttpVerbPOST, "{}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<HospitalCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Finalizar curación " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchMyAlliance(Action<AllianceCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_alliance_members?select=member_role,alliance:frostbound_alliances(id,name,tag,power_total)&user_id=eq." + userId + "&limit=1";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Alianza " + request.responseCode + ": " + SafeError(request)); yield break; }
            onSuccess?.Invoke(ParseAllianceMembership(request.downloadHandler.text));
        }

        public IEnumerator SearchAlliances(string search, Action<AllianceSearchRow[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string filter = string.IsNullOrWhiteSpace(search) ? string.Empty : "&or=(name.ilike.*" + UnityWebRequest.EscapeURL(search.Trim()) + "*,tag.ilike.*" + UnityWebRequest.EscapeURL(search.Trim().ToUpperInvariant()) + "*)";
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_alliances?select=id,name,tag,power_total&order=power_total.desc&limit=12" + filter, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Buscar alianzas " + request.responseCode + ": " + SafeError(request)); yield break; }
            AllianceSearchRows rows = ParseArray<AllianceSearchRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<AllianceSearchRow>());
        }

        public IEnumerator CreateAlliance(string allianceName, string tag, Action<AllianceCloudState> onSuccess, Action<string> onError)
        {
            CreateAllianceRequest payload = new CreateAllianceRequest { p_name = allianceName, p_tag = tag };
            yield return CallAllianceRpc("frostbound_create_alliance", JsonUtility.ToJson(payload), onSuccess, onError);
        }

        public IEnumerator JoinAlliance(string allianceId, Action<AllianceCloudState> onSuccess, Action<string> onError)
        {
            JoinAllianceRequest payload = new JoinAllianceRequest { p_alliance_id = allianceId };
            yield return CallAllianceRpc("frostbound_join_alliance", JsonUtility.ToJson(payload), onSuccess, onError);
        }

        public IEnumerator RequestAllianceHelp(string targetType, string targetKey, Action onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Necesitas conexión para pedir ayuda"); yield break; }
            RequestHelpPayload payload = new RequestHelpPayload { p_target_type = targetType, p_target_key = targetKey };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_request_alliance_help", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(); else onError?.Invoke("Pedir ayuda " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchAllianceHelp(Action<AllianceHelpRow[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_alliance_help?select=id,requester_id,target_type,target_key,help_count,created_at&status=eq.Open&order=created_at.desc&limit=20", UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Ayudas " + request.responseCode + ": " + SafeError(request)); yield break; }
            AllianceHelpRows rows = ParseArray<AllianceHelpRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<AllianceHelpRow>());
        }

        public IEnumerator GiveAllianceHelp(string helpId, Action onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            HelpActionPayload payload = new HelpActionPayload { help_id = helpId, helper_id = userId };
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_alliance_help_actions", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(); else onError?.Invoke("Ayudar " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchAllianceStructures(int minX, int maxX, int minY, int maxY, Action<AllianceStructureCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_alliance_structures?select=id,alliance_id,structure_type,x,y,status,territory_radius,created_by,updated_at" +
                "&status=eq.Active&x=gte." + minX + "&x=lte." + maxX + "&y=gte." + minY + "&y=lte." + maxY + "&order=x.asc,y.asc";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Territorio " + request.responseCode + ": " + SafeError(request)); yield break; }
            AllianceStructureRows rows = ParseArray<AllianceStructureRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<AllianceStructureCloudState>());
        }

        public IEnumerator PlaceAllianceStructure(string structureType, int x, int y, Action<AllianceStructureCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            PlaceStructureRequest payload = new PlaceStructureRequest { p_structure_type = structureType, p_x = x, p_y = y };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_place_alliance_structure", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<AllianceStructureCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Colocar HQ " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchAllianceRallies(Action<RallyCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_rallies?select=id,alliance_id,leader_id,target_x,target_y,target_type,status,rally_starts_at&status=eq.Forming&order=rally_starts_at.asc&limit=12";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Rallies " + request.responseCode + ": " + SafeError(request)); yield break; }
            RallyRows rows = ParseArray<RallyRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<RallyCloudState>());
        }

        public IEnumerator CreateAllianceRally(int x, int y, string targetType, int troopCount, Action<RallyCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            CreateRallyRequest payload = new CreateRallyRequest { p_target_x = x, p_target_y = y, p_target_type = targetType, p_troop_count = troopCount };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_create_rally", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<RallyCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Crear rally " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator JoinAllianceRally(string rallyId, int troopCount, Action<RallyJoinCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            JoinRallyRequest payload = new JoinRallyRequest { p_rally_id = rallyId, p_troop_count = troopCount };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_join_rally", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<RallyJoinCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Unirse al rally " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchAllianceBuffs(Action<AllianceBuffCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_get_my_alliance_buffs", UnityWebRequest.kHttpVerbPOST, "{}", true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<AllianceBuffCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Buffs de alianza " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator ProcessFacilityRally(string rallyId, Action<FacilityRallyResult> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            ProcessFacilityRallyRequest payload = new ProcessFacilityRallyRequest { p_rally_id = rallyId };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/frostbound_process_facility_rally", UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<FacilityRallyResult>(request.downloadHandler.text));
            else onError?.Invoke("Batalla de instalación " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator InitializeHito11(Action onSuccess, Action<string> onError)
        {
            yield return CallSimpleRpc("frostbound_initialize_hito11", "{}", onSuccess, onError);
        }

        public IEnumerator FetchDailyQuests(Action<QuestCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_quests?select=id,quest_key,title,objective_type,target_amount,progress,points,reward_wood,reward_food,reward_crystals,reward_speedups,claimed_at&quest_date=eq." + DateTime.UtcNow.ToString("yyyy-MM-dd") + "&order=quest_key";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(ParseArray<QuestRows>(request.downloadHandler.text)?.items ?? Array.Empty<QuestCloudState>());
            else onError?.Invoke("Misiones " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchAchievements(Action<AchievementCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_achievements?select=id,achievement_key,title,objective_type,target_amount,progress,reward_wood,reward_food,reward_crystals,reward_speedups,claimed_at&order=achievement_key", UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(ParseArray<AchievementRows>(request.downloadHandler.text)?.items ?? Array.Empty<AchievementCloudState>());
            else onError?.Invoke("Logros " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchMail(string category, Action<MailCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            string path = "/rest/v1/frostbound_mail?select=id,category,subject,body,source_key,reward_wood,reward_food,reward_crystals,reward_speedups,read_at,claimed_at,created_at&category=eq." + UnityWebRequest.EscapeURL(category) + "&order=created_at.desc&limit=30";
            using UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(ParseArray<MailRows>(request.downloadHandler.text)?.items ?? Array.Empty<MailCloudState>());
            else onError?.Invoke("Correo " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator RecordQuestProgress(string objective, int amount, Action onSuccess = null)
        {
            ObjectiveRequest payload = new ObjectiveRequest { p_objective_type = objective, p_amount = amount };
            yield return CallSimpleRpc("frostbound_record_progress", JsonUtility.ToJson(payload), onSuccess, error => Debug.LogWarning(error));
        }

        public IEnumerator ClaimQuest(string id, Action<RewardCloudState> onSuccess, Action<string> onError)
        {
            yield return CallRewardRpc("frostbound_claim_quest", JsonUtility.ToJson(new IdRequest { p_quest_id = id }), onSuccess, onError);
        }

        public IEnumerator ClaimAchievement(string id, Action<RewardCloudState> onSuccess, Action<string> onError)
        {
            yield return CallRewardRpc("frostbound_claim_achievement", JsonUtility.ToJson(new IdRequest { p_achievement_id = id }), onSuccess, onError);
        }

        public IEnumerator ClaimDailyChest(int milestone, Action<RewardCloudState> onSuccess, Action<string> onError)
        {
            yield return CallRewardRpc("frostbound_claim_daily_chest", JsonUtility.ToJson(new ChestRequest { p_milestone = milestone }), onSuccess, onError);
        }

        public IEnumerator ClaimAllMail(Action<RewardCloudState> onSuccess, Action<string> onError)
        {
            yield return CallRewardRpc("frostbound_claim_all_mail", "{}", onSuccess, onError);
        }

        public IEnumerator MarkMailRead(string id, Action onSuccess = null)
        {
            yield return CallSimpleRpc("frostbound_mark_mail_read", JsonUtility.ToJson(new IdRequest { p_mail_id = id }), onSuccess, error => Debug.LogWarning(error));
        }

        public IEnumerator AddBattleMail(string sourceKey, string subject, string body)
        {
            BattleMailRequest payload = new BattleMailRequest { p_source_key = sourceKey, p_subject = subject, p_body = body };
            yield return CallSimpleRpc("frostbound_add_battle_mail", JsonUtility.ToJson(payload), null, error => Debug.LogWarning(error));
        }

        private IEnumerator CallRewardRpc(string rpc, string json, Action<RewardCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/" + rpc, UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<RewardCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Recompensa " + request.responseCode + ": " + SafeError(request));
        }

        private IEnumerator CallSimpleRpc(string rpc, string json, Action onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/" + rpc, UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke();
            else onError?.Invoke(rpc + " " + request.responseCode + ": " + SafeError(request));
        }

        public IEnumerator FetchInventory(Action<InventoryRow[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request=CreateRequest("/rest/v1/frostbound_inventory?select=item_id,quantity,updated_at&quantity=gt.0&order=item_id",UnityWebRequest.kHttpVerbGET,null,true);
            yield return request.SendWebRequest();
            if(IsSuccess(request)) onSuccess?.Invoke(ParseArray<InventoryRows>(request.downloadHandler.text)?.items??Array.Empty<InventoryRow>()); else onError?.Invoke("Inventario "+request.responseCode+": "+SafeError(request));
        }

        public IEnumerator FetchAllianceShop(Action<ShopRow[]> onSuccess,Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request=CreateRequest("/rest/v1/frostbound_alliance_shop?select=item_id,display_name,category,honor_cost,quantity_per_purchase&active=is.true&order=honor_cost",UnityWebRequest.kHttpVerbGET,null,true);
            yield return request.SendWebRequest();
            if(IsSuccess(request)) onSuccess?.Invoke(ParseArray<ShopRows>(request.downloadHandler.text)?.items??Array.Empty<ShopRow>()); else onError?.Invoke("Tienda "+request.responseCode+": "+SafeError(request));
        }

        public IEnumerator FetchHonor(Action<ItemActionResult> onSuccess,Action<string> onError) => CallItemRpc("frostbound_get_my_honor","{}",onSuccess,onError);
        public IEnumerator BuyAllianceItem(string itemId,Action<ItemActionResult> onSuccess,Action<string> onError) => CallItemRpc("frostbound_buy_alliance_item",JsonUtility.ToJson(new BuyItemRequest{p_item_id=itemId}),onSuccess,onError);
        public IEnumerator UseInventoryItem(string itemId,string targetType,string targetKey,Action<ItemActionResult> onSuccess,Action<string> onError) => CallItemRpc("frostbound_use_item",JsonUtility.ToJson(new ItemRequest{p_item_id=itemId,p_target_type=targetType,p_target_key=targetKey}),onSuccess,onError);
        public IEnumerator DonateAllianceResource(string resource,int amount,Action<ItemActionResult> onSuccess,Action<string> onError) => CallItemRpc("frostbound_donate_alliance_technology",JsonUtility.ToJson(new DonationRequest{p_resource=resource,p_amount=amount}),onSuccess,onError);
        private IEnumerator CallItemRpc(string rpc,string json,Action<ItemActionResult> onSuccess,Action<string> onError)
        {
            if(!HasSession){onError?.Invoke("Sin sesión de Supabase");yield break;}
            using UnityWebRequest request=CreateRequest("/rest/v1/rpc/"+rpc,UnityWebRequest.kHttpVerbPOST,json,true); yield return request.SendWebRequest();
            if(IsSuccess(request))onSuccess?.Invoke(JsonUtility.FromJson<ItemActionResult>(request.downloadHandler.text));else onError?.Invoke(rpc+" "+request.responseCode+": "+SafeError(request));
        }

        public IEnumerator FetchResearch(Action<ResearchCloudState[]> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/frostbound_research?select=tech_id,branch,level,target_level,status,research_started_at,finishes_at,wood_cost,food_cost,crystal_cost&order=branch,tech_id", UnityWebRequest.kHttpVerbGET, null, true);
            yield return request.SendWebRequest();
            if (!IsSuccess(request)) { onError?.Invoke("Investigación " + request.responseCode + ": " + SafeError(request)); yield break; }
            ResearchRows rows = ParseArray<ResearchRows>(request.downloadHandler.text);
            onSuccess?.Invoke(rows?.items ?? Array.Empty<ResearchCloudState>());
        }

        public IEnumerator StartResearch(string techId, Action<ResearchCloudState> onSuccess, Action<string> onError)
        {
            yield return CallResearchRpc("frostbound_start_research", techId, onSuccess, onError);
        }

        public IEnumerator CompleteResearch(string techId, Action<ResearchCloudState> onSuccess, Action<string> onError)
        {
            yield return CallResearchRpc("frostbound_complete_research", techId, onSuccess, onError);
        }

        private IEnumerator CallResearchRpc(string rpc, string techId, Action<ResearchCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            ResearchRequest payload = new ResearchRequest { p_tech_id = techId };
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/" + rpc, UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<ResearchCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Investigación " + request.responseCode + ": " + SafeError(request));
        }

        private IEnumerator CallAllianceRpc(string rpc, string json, Action<AllianceCloudState> onSuccess, Action<string> onError)
        {
            if (!HasSession) { onError?.Invoke("Sin sesión de Supabase"); yield break; }
            using UnityWebRequest request = CreateRequest("/rest/v1/rpc/" + rpc, UnityWebRequest.kHttpVerbPOST, json, true);
            yield return request.SendWebRequest();
            if (IsSuccess(request)) onSuccess?.Invoke(JsonUtility.FromJson<AllianceCloudState>(request.downloadHandler.text));
            else onError?.Invoke("Alianza " + request.responseCode + ": " + SafeError(request));
        }

        private static AllianceCloudState ParseAllianceMembership(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;
            string role = ExtractJsonString(json, "member_role");
            return new AllianceCloudState
            {
                alliance_id = ExtractJsonString(json, "id"), name = ExtractJsonString(json, "name"),
                tag = ExtractJsonString(json, "tag"), member_role = role,
                power_total = ExtractJsonLong(json, "power_total"), member_count = 1
            };
        }

        private static string ExtractJsonString(string json, string key)
        {
            string marker = "\"" + key + "\":"; int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty; start = json.IndexOf('"', start + marker.Length);
            if (start < 0) return string.Empty; int end = json.IndexOf('"', start + 1);
            return end > start ? json.Substring(start + 1, end - start - 1) : string.Empty;
        }

        private static long ExtractJsonLong(string json, string key)
        {
            string marker = "\"" + key + "\":"; int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return 0; start += marker.Length; int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return long.TryParse(json.Substring(start, end - start), out long value) ? value : 0;
        }

        private IEnumerator SignInAnonymously()
        {
            requestInProgress = true;
            Status = "NUBE: CONECTANDO";
            using UnityWebRequest request = CreateRequest("/auth/v1/signup", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(new AnonymousRequest()), false);
            yield return request.SendWebRequest();
            requestInProgress = false;
            if (!IsSuccess(request)) { Status = "NUBE: LOCAL"; Debug.LogWarning("Supabase anonymous sign-in failed: " + SafeError(request)); yield break; }
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            if (response?.user == null || string.IsNullOrWhiteSpace(response.access_token)) { Status = "NUBE: ERROR AUTH"; yield break; }
            accessToken = response.access_token;
            refreshToken = response.refresh_token;
            userId = response.user.id;
            PersistSession();
            yield return LoadRelationalThenSync();
        }

        private IEnumerator LoadRelationalThenSync()
        {
            requestInProgress = true;
            Status = "NUBE: CARGANDO";
            using UnityWebRequest playerRequest = CreateRequest(
                "/rest/v1/frostbound_players?select=display_name,temperature,population,wood,food,coal,generator_level,health,happiness,power,client_saved_at,snow_infantry,snow_lancers,snow_marksmen,crystals,speedups&limit=1",
                UnityWebRequest.kHttpVerbGET, null, true);
            yield return playerRequest.SendWebRequest();
            if (playerRequest.responseCode == 401) { requestInProgress = false; yield return RefreshSession(); yield break; }
            if (!IsSuccess(playerRequest)) { requestInProgress = false; Fail("Relational player load", playerRequest); yield break; }

            using UnityWebRequest buildingRequest = CreateRequest(
                "/rest/v1/frostbound_buildings?select=slot_id,building_type,level,assigned_workers,upgrade_started_at,finishes_at,pos_x,pos_z&order=slot_id",
                UnityWebRequest.kHttpVerbGET, null, true);
            yield return buildingRequest.SendWebRequest();
            requestInProgress = false;
            if (!IsSuccess(buildingRequest)) { Fail("Relational building load", buildingRequest); yield break; }

            RelationalPlayerRows players = ParseArray<RelationalPlayerRows>(playerRequest.downloadHandler.text);
            RelationalBuildingRows buildingRows = ParseArray<RelationalBuildingRows>(buildingRequest.downloadHandler.text);
            FrostboundFrontierPrototype game = FindAnyObjectByType<FrostboundFrontierPrototype>();
            if (players?.items != null && players.items.Length > 0 && players.items[0].client_saved_at > game.LocalSavedAtUtcTicks)
                game.ApplyRelationalCloudState(ToGamePlayer(players.items[0]), ToGameBuildings(buildingRows?.items));
            yield return InitializeHito6(game.ApplyHeroCloudState, error => Debug.LogWarning(error));
            yield return FetchHospital(game.ApplyHospitalCloudState, error => Debug.LogWarning(error));
            yield return SyncRelationalAndBackup();
        }

        private IEnumerator SyncRelationalAndBackup()
        {
            if (requestInProgress || !HasSession) yield break;
            FrostboundFrontierPrototype game = FindAnyObjectByType<FrostboundFrontierPrototype>();
            if (game == null) yield break;
            requestInProgress = true;
            Status = "NUBE: SINCRONIZANDO";

            FrostboundFrontierPrototype.PlayerCloudState player = game.GetPlayerCloudState();
            RelationalPlayerPayload playerPayload = new RelationalPlayerPayload
            {
                user_id = userId, display_name = player.displayName, temperature = player.temperature,
                population = player.population, wood = player.wood, food = player.food, coal = player.coal,
                generator_level = player.generatorLevel, health = player.health, happiness = player.happiness,
                power = player.power, client_saved_at = player.clientSavedAt, snow_infantry = player.snowInfantry,
                snow_lancers = player.snowLancers, snow_marksmen = player.snowMarksmen,
                crystals = player.crystals, speedups = player.speedups
            };
            using UnityWebRequest playerRequest = CreateUpsert("/rest/v1/frostbound_players?on_conflict=user_id", JsonUtility.ToJson(playerPayload));
            yield return playerRequest.SendWebRequest();
            if (!IsSuccess(playerRequest)) { requestInProgress = false; if (playerRequest.responseCode == 401) yield return RefreshSession(); else Fail("Relational player save", playerRequest); yield break; }

            using UnityWebRequest buildingsRequest = CreateUpsert(
                "/rest/v1/frostbound_buildings?on_conflict=user_id,slot_id", BuildBuildingsJson(game.GetBuildingCloudStates()));
            yield return buildingsRequest.SendWebRequest();
            if (!IsSuccess(buildingsRequest)) { requestInProgress = false; Fail("Relational building save", buildingsRequest); yield break; }

            string leaderboardJson = "{\"user_id\":\"" + userId + "\",\"display_name\":\"SUPERVIVIENTE\",\"generator_level\":" +
                player.generatorLevel + ",\"power\":" + player.power + "}";
            using UnityWebRequest leaderboardRequest = CreateUpsert("/rest/v1/frostbound_leaderboard?on_conflict=user_id", leaderboardJson);
            yield return leaderboardRequest.SendWebRequest();
            if (!IsSuccess(leaderboardRequest)) { requestInProgress = false; Fail("Leaderboard save", leaderboardRequest); yield break; }

            EmergencySavePayload backup = new EmergencySavePayload
            {
                user_id = userId, save_json = game.ExportCloudSaveJson(), client_saved_at = game.LocalSavedAtUtcTicks
            };
            using UnityWebRequest backupRequest = CreateUpsert("/rest/v1/frostbound_saves?on_conflict=user_id", JsonUtility.ToJson(backup));
            yield return backupRequest.SendWebRequest();
            requestInProgress = false;
            Status = IsSuccess(backupRequest) ? "NUBE: RELACIONAL" : "NUBE: BACKUP ERROR";
            if (!IsSuccess(backupRequest)) Debug.LogWarning("Emergency backup failed: " + SafeError(backupRequest));
        }

        private IEnumerator RefreshSession()
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) { ClearSession(); yield return SignInAnonymously(); yield break; }
            requestInProgress = true;
            Status = "NUBE: RENOVANDO";
            using UnityWebRequest request = CreateRequest("/auth/v1/token?grant_type=refresh_token", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(new RefreshRequest { refresh_token = refreshToken }), false);
            yield return request.SendWebRequest();
            requestInProgress = false;
            if (!IsSuccess(request)) { ClearSession(); yield return SignInAnonymously(); yield break; }
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            accessToken = response.access_token;
            refreshToken = response.refresh_token;
            if (response.user != null) userId = response.user.id;
            PersistSession();
            yield return LoadRelationalThenSync();
        }

        private static T ParseArray<T>(string json) => JsonUtility.FromJson<T>("{\"items\":" + json + "}");

        private static FrostboundFrontierPrototype.PlayerCloudState ToGamePlayer(RelationalPlayerRow row)
        {
            return new FrostboundFrontierPrototype.PlayerCloudState
            {
                displayName = row.display_name, temperature = row.temperature, population = row.population,
                wood = row.wood, food = row.food, coal = row.coal, generatorLevel = row.generator_level,
                health = row.health, happiness = row.happiness, power = row.power, clientSavedAt = row.client_saved_at,
                snowInfantry = row.snow_infantry, snowLancers = row.snow_lancers, snowMarksmen = row.snow_marksmen,
                crystals = row.crystals, speedups = row.speedups
            };
        }

        private static FrostboundFrontierPrototype.BuildingCloudState[] ToGameBuildings(RelationalBuildingRow[] rows)
        {
            if (rows == null) return Array.Empty<FrostboundFrontierPrototype.BuildingCloudState>();
            FrostboundFrontierPrototype.BuildingCloudState[] result = new FrostboundFrontierPrototype.BuildingCloudState[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                RelationalBuildingRow row = rows[i];
                result[i] = new FrostboundFrontierPrototype.BuildingCloudState
                {
                    slotId = row.slot_id, buildingType = row.building_type, level = row.level,
                    assignedWorkers = row.assigned_workers, upgradeStartedUtcTicks = ParseUtcTicks(row.upgrade_started_at),
                    finishesUtcTicks = ParseUtcTicks(row.finishes_at), posX = row.pos_x, posZ = row.pos_z
                };
            }
            return result;
        }

        private string BuildBuildingsJson(FrostboundFrontierPrototype.BuildingCloudState[] rows)
        {
            List<string> jsonRows = new List<string>(rows.Length);
            foreach (FrostboundFrontierPrototype.BuildingCloudState row in rows)
                jsonRows.Add("{\"user_id\":\"" + userId + "\",\"slot_id\":\"" + row.slotId +
                    "\",\"building_type\":\"" + row.buildingType + "\",\"level\":" + row.level +
                    ",\"assigned_workers\":" + row.assignedWorkers + ",\"upgrade_started_at\":" + ToNullableIso(row.upgradeStartedUtcTicks) +
                    ",\"finishes_at\":" + ToNullableIso(row.finishesUtcTicks) + ",\"pos_x\":" + row.posX.ToString(CultureInfo.InvariantCulture) +
                    ",\"pos_z\":" + row.posZ.ToString(CultureInfo.InvariantCulture) + "}");
            return "[" + string.Join(",", jsonRows) + "]";
        }

        private static string ToNullableIso(long ticks) => ticks <= 0 ? "null" : "\"" + new DateTime(ticks, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) + "\"";
        private static long ParseUtcTicks(string value) => !string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsed) ? parsed.ToUniversalTime().Ticks : 0;

        private static UnityWebRequest CreateUpsert(string path, string json)
        {
            UnityWebRequest request = CreateRequest(path, UnityWebRequest.kHttpVerbPOST, json, true);
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
            return request;
        }

        private static UnityWebRequest CreateRequest(string path, string method, string json, bool useSession)
        {
            UnityWebRequest request = new UnityWebRequest(ProjectUrl + path, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = 15 };
            if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", PublishableKey);
            if (useSession && Instance != null && !string.IsNullOrWhiteSpace(Instance.accessToken)) request.SetRequestHeader("Authorization", "Bearer " + Instance.accessToken);
            return request;
        }

        private void PersistSession()
        {
            PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken ?? string.Empty);
            PlayerPrefs.SetString(UserIdKey, userId ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void ClearSession()
        {
            accessToken = refreshToken = userId = string.Empty;
            PlayerPrefs.DeleteKey(AccessTokenKey); PlayerPrefs.DeleteKey(RefreshTokenKey); PlayerPrefs.DeleteKey(UserIdKey);
        }

        private static void Fail(string operation, UnityWebRequest request) { Status = "NUBE: ERROR"; Debug.LogWarning(operation + " failed: " + SafeError(request)); }
        private static bool IsSuccess(UnityWebRequest request) => request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
        private static string SafeError(UnityWebRequest request) => string.IsNullOrWhiteSpace(request.downloadHandler?.text) ? request.error : request.downloadHandler.text;
    }
}
