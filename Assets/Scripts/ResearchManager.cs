using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class ResearchManager : MonoBehaviour
    {
        private sealed class TechDefinition
        {
            public string Id, Name, Branch, Description;
            public TechDefinition(string id, string name, string branch, string description) { Id=id; Name=name; Branch=branch; Description=description; }
        }

        private static readonly TechDefinition[] Definitions =
        {
            new TechDefinition("CortaEficaz", "Corta eficaz", "Economy", "+5% producción de madera por nivel"),
            new TechDefinition("RacionesOptimizadas", "Raciones optimizadas", "Economy", "+5% producción de comida por nivel"),
            new TechDefinition("InfanteriaBlindada", "Infantería blindada", "Military", "+5% poder de combate por nivel"),
            new TechDefinition("MarchaForzada", "Marcha forzada", "Military", "+4% velocidad de marcha por nivel")
        };

        public static ResearchManager Instance { get; private set; }
        public static bool IsPanelOpen => Instance != null && Instance.panelOpen;
        public static float WoodProductionMultiplier => 1f + LevelOf("CortaEficaz") * .05f;
        public static float FoodProductionMultiplier => 1f + LevelOf("RacionesOptimizadas") * .05f;
        public static float CombatPowerMultiplier => 1f + LevelOf("InfanteriaBlindada") * .05f;
        public static float MarchDurationMultiplier => Mathf.Max(.35f, 1f - LevelOf("MarchaForzada") * .04f);

        private static readonly Dictionary<string,int> Levels = new Dictionary<string,int>();
        private readonly Dictionary<string,SupabaseSyncClient.ResearchCloudState> states = new Dictionary<string,SupabaseSyncClient.ResearchCloudState>();
        private bool panelOpen, busy;
        private float refreshClock;
        private string selectedBranch = "Economy";
        private string message = "Selecciona una tecnología para mejorar el asentamiento.";
        private GUIStyle panelStyle, titleStyle, bodyStyle, buttonStyle, activeButtonStyle;
        private Texture2D panelTexture, buttonTexture, activeTexture, progressTexture;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ResearchManager>() == null)
                new GameObject(nameof(ResearchManager)).AddComponent<ResearchManager>();
        }

        private void Awake() { Instance=this; }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SupabaseSyncClient.Instance != null && SupabaseSyncClient.Instance.CanQueryWorld);
            Refresh();
        }

        private void Update()
        {
            refreshClock += Time.unscaledDeltaTime;
            if (refreshClock >= 5f && (panelOpen || ActiveResearch() != null)) { refreshClock=0f; Refresh(); }
            SupabaseSyncClient.ResearchCloudState active = ActiveResearch();
            if (!busy && active != null && ParseUtc(active.finishes_at) <= DateTime.UtcNow) Complete(active.tech_id);
        }

        public void OpenPanel() { panelOpen=true; }
        public string ActiveTechId => ActiveResearch()?.tech_id ?? string.Empty;
        public void RefreshFromCloud() { Refresh(); }

        private static int LevelOf(string id) => Levels.TryGetValue(id, out int value) ? value : 0;
        private SupabaseSyncClient.ResearchCloudState ActiveResearch()
        {
            foreach (SupabaseSyncClient.ResearchCloudState row in states.Values) if (row.status == "Researching") return row;
            return null;
        }

        private void Refresh()
        {
            if (busy || SupabaseSyncClient.Instance == null) return;
            busy=true;
            StartCoroutine(SupabaseSyncClient.Instance.FetchResearch(rows =>
            {
                busy=false; states.Clear(); Levels.Clear();
                foreach (SupabaseSyncClient.ResearchCloudState row in rows) { states[row.tech_id]=row; Levels[row.tech_id]=row.level; }
            }, error => { busy=false; message=Friendly(error); }));
        }

        private void StartTech(string id)
        {
            if (busy) return; busy=true; message="Iniciando investigación...";
            StartCoroutine(SupabaseSyncClient.Instance.StartResearch(id, row =>
            {
                busy=false; states[id]=row; Levels[id]=row.level;
                FindAnyObjectByType<FrostboundFrontierPrototype>()?.SpendResearchResourcesLocally(row.wood_cost,row.food_cost,row.crystal_cost);
                message="Investigación iniciada. Puedes pedir ayuda a tu alianza.";
            }, error => { busy=false; message=Friendly(error); }));
        }

        private void Complete(string id)
        {
            busy=true;
            StartCoroutine(SupabaseSyncClient.Instance.CompleteResearch(id, row =>
            {
                busy=false; states[id]=row; Levels[id]=row.level; message="¡Tecnología completada! Bonus aplicado en tiempo real.";
            }, error => { busy=false; message=Friendly(error); Refresh(); }));
        }

        private void OnGUI()
        {
            if (AllianceManager.IsPanelOpen || QuestMailManager.IsPanelOpen || InventoryShopManager.IsPanelOpen) return;
            EnsureStyles(); GUI.depth=-200;
            float scale=Mathf.Clamp(Screen.width/1280f,.75f,1.35f); Matrix4x4 old=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            float width=Screen.width/scale, height=Screen.height/scale;
            if (GUI.Button(new Rect(470f,98f,190f,42f),"⚙ INVESTIGACIÓN",buttonStyle)) panelOpen=!panelOpen;
            if (panelOpen) DrawPanel(width,height);
            GUI.matrix=old;
        }

        private void DrawPanel(float width,float height)
        {
            Rect panel=new Rect(width*.5f-390f,height*.5f-250f,780f,500f);
            GUI.Box(panel,GUIContent.none,panelStyle);
            GUI.Label(new Rect(panel.x+28f,panel.y+18f,620f,42f),"CENTRO DE INVESTIGACIÓN",titleStyle);
            if(GUI.Button(new Rect(panel.x+704f,panel.y+16f,48f,42f),"×",buttonStyle)){panelOpen=false;return;}
            GUI.Label(new Rect(panel.x+28f,panel.y+62f,panel.width-56f,36f),message,bodyStyle);
            if(GUI.Button(new Rect(panel.x+28f,panel.y+108f,220f,46f),"ECONOMÍA",selectedBranch=="Economy"?activeButtonStyle:buttonStyle))selectedBranch="Economy";
            if(GUI.Button(new Rect(panel.x+260f,panel.y+108f,220f,46f),"MILITAR",selectedBranch=="Military"?activeButtonStyle:buttonStyle))selectedBranch="Military";
            GUI.Label(new Rect(panel.x+506f,panel.y+108f,246f,46f),selectedBranch=="Economy"?"PRODUCCIÓN GLOBAL":"PODER Y VELOCIDAD",bodyStyle);
            int index=0;
            foreach(TechDefinition def in Definitions)
            {
                if(def.Branch!=selectedBranch)continue;
                DrawTech(panel,def,index++);
            }
            SupabaseSyncClient.ResearchCloudState active=ActiveResearch();
            if(active!=null)
            {
                DateTime start=ParseUtc(active.research_started_at),finish=ParseUtc(active.finishes_at);
                double total=Math.Max(1,(finish-start).TotalSeconds),left=Math.Max(0,(finish-DateTime.UtcNow).TotalSeconds);
                float progress=1f-(float)(left/total);
                Rect bar=new Rect(panel.x+28f,panel.y+416f,480f,34f);GUI.DrawTexture(bar,panelTexture);
                GUI.color=new Color(.18f,.82f,.56f);GUI.DrawTexture(new Rect(bar.x+2f,bar.y+2f,(bar.width-4f)*Mathf.Clamp01(progress),bar.height-4f),progressTexture);GUI.color=Color.white;
                GUI.Label(bar,active.tech_id+" · "+Mathf.CeilToInt((float)left)+" s",bodyStyle);
                if(GUI.Button(new Rect(panel.x+528f,panel.y+412f,224f,42f),"PEDIR AYUDA",buttonStyle)) AllianceManager.Instance?.RequestHelp("Research",active.tech_id);
            }
        }

        private void DrawTech(Rect panel,TechDefinition def,int index)
        {
            float y=panel.y+176f+index*112f; states.TryGetValue(def.Id,out SupabaseSyncClient.ResearchCloudState row);
            int level=row?.level??0; int next=level+1; int wood=60*next,food=45*next,crystals=level>=4?5*(level-3):0;
            GUI.Label(new Rect(panel.x+34f,y,300f,34f),def.Name+" · NIVEL "+level,titleStyle);
            GUI.Label(new Rect(panel.x+34f,y+34f,420f,52f),def.Description+"\nCoste: "+wood+" madera · "+food+" comida"+(crystals>0?" · "+crystals+" cristales":""),bodyStyle);
            bool canStart=ActiveResearch()==null&&!busy&&level<20;GUI.enabled=canStart;
            if(GUI.Button(new Rect(panel.x+548f,y+18f,190f,56f),level>=20?"MÁXIMO":"INVESTIGAR NV. "+next,buttonStyle))StartTech(def.Id);
            GUI.enabled=true;
        }

        private static DateTime ParseUtc(string value)=>DateTime.TryParse(value,out DateTime parsed)?parsed.ToUniversalTime():DateTime.MinValue;
        private static string Friendly(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Error de investigación.";
            if(value.Contains("Not enough"))return "No tienes suficientes recursos para investigar.";
            if(value.Contains("queue busy"))return "Ya existe una investigación activa.";
            return value.Length<120?value:value.Substring(0,117)+"...";
        }

        private void EnsureStyles()
        {
            if(stylesReady)return;panelTexture=Make(new Color(.025f,.07f,.1f,.98f));buttonTexture=Make(new Color(.07f,.36f,.57f,1));activeTexture=Make(new Color(.08f,.62f,.72f,1));progressTexture=Make(Color.white);
            panelStyle=new GUIStyle(GUI.skin.box);panelStyle.normal.background=panelTexture;
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};titleStyle.normal.textColor=new Color(.8f,.95f,1);
            bodyStyle=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleLeft,wordWrap=true};bodyStyle.normal.textColor=Color.white;
            buttonStyle=new GUIStyle(GUI.skin.button){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};buttonStyle.normal.background=buttonTexture;buttonStyle.hover.background=buttonTexture;buttonStyle.normal.textColor=Color.white;
            FrostboundVisualTheme.ApplyPanel(panelStyle);
            FrostboundVisualTheme.ApplyButton(buttonStyle);
            activeButtonStyle=new GUIStyle(buttonStyle);activeButtonStyle.normal.background=activeTexture;activeButtonStyle.hover.background=activeTexture;stylesReady=true;
        }
        private static Texture2D Make(Color color){Texture2D t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
    }
}
