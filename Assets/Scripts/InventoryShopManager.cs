using System;
using System.Collections;
using UnityEngine;

namespace FrostboundFrontier
{
 public sealed class InventoryShopManager:MonoBehaviour
 {
  public static InventoryShopManager Instance{get;private set;} public static bool IsPanelOpen=>Instance!=null&&Instance.open;
  private SupabaseSyncClient.InventoryRow[] inventory=Array.Empty<SupabaseSyncClient.InventoryRow>(); private SupabaseSyncClient.ShopRow[] shop=Array.Empty<SupabaseSyncClient.ShopRow>();
  private bool open,busy,shopMode; private string category="Resources",message="Mochila sincronizada"; private int honor;
  private GUIStyle panel,title,body,button,active,row; private Texture2D dark,blue,cyan,rowTex;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install(){if(FindAnyObjectByType<InventoryShopManager>()==null)new GameObject(nameof(InventoryShopManager)).AddComponent<InventoryShopManager>();}
  void Awake(){Instance=this;}
  IEnumerator Start(){yield return new WaitUntil(()=>SupabaseSyncClient.Instance!=null&&SupabaseSyncClient.Instance.CanQueryWorld);Refresh();}
  void Refresh(){if(!busy&&SupabaseSyncClient.Instance!=null)StartCoroutine(RefreshRoutine());}
  IEnumerator RefreshRoutine(){busy=true;yield return SupabaseSyncClient.Instance.FetchInventory(x=>inventory=x,Error);yield return SupabaseSyncClient.Instance.FetchAllianceShop(x=>shop=x,Error);yield return SupabaseSyncClient.Instance.FetchHonor(x=>honor=x.honor_points,Error);busy=false;}
  void OnGUI(){if(WorldMapManager.IsWorldMapActive)return;Ensure();float s=Mathf.Clamp(Screen.width/1280f,.75f,1.35f);Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*s);float w=Screen.width/s,h=Screen.height/s;GUI.depth=-40;
   if(!open&&!AllianceManager.IsPanelOpen&&!ResearchManager.IsPanelOpen&&!QuestMailManager.IsPanelOpen&&GUI.Button(new Rect(608f,148f,150f,40f),"MOCHILA",button)){open=true;Refresh();}
   if(open)Draw(w,h);GUI.matrix=old;}
  void Draw(float w,float h){Rect p=new Rect((w-920)*.5f,(h-590)*.5f,920,590);GUI.Box(p,GUIContent.none,panel);GUI.Label(new Rect(p.x+28,p.y+16,470,48),shopMode?"TIENDA DE ALIANZA":"MOCHILA / INVENTARIO",title);GUI.Label(new Rect(p.x+540,p.y+20,280,38),"HONOR DE ALIANZA  "+honor,body);if(GUI.Button(new Rect(p.x+846,p.y+16,48,42),"X",button)){open=false;return;}
   if(GUI.Button(new Rect(p.x+28,p.y+72,190,44),"MOCHILA",shopMode?button:active))shopMode=false;if(GUI.Button(new Rect(p.x+228,p.y+72,190,44),"TIENDA",shopMode?active:button))shopMode=true;
   string[] cats={"Resources","Speedups","Combat","Special"};string[] labels={"RECURSOS","ACELERADORES","COMBATE","ESPECIALES"};for(int i=0;i<4;i++)if(GUI.Button(new Rect(p.x+28+i*218,p.y+128,205,40),labels[i],category==cats[i]?active:button))category=cats[i];
   if(shopMode){if(GUI.Button(new Rect(p.x+28,p.y+180,190,38),"DONAR 100 MADERA",button))Donate("Wood");if(GUI.Button(new Rect(p.x+228,p.y+180,190,38),"DONAR 100 COMIDA",button))Donate("Food");DrawShop(p);}else DrawInventory(p);
   GUI.Label(new Rect(p.x+28,p.y+548,850,28),busy?"SINCRONIZANDO...":message,body);}
  void DrawInventory(Rect p){int i=0;foreach(var x in inventory){if(CategoryOf(x.item_id)!=category)continue;float y=p.y+190+i++*70;GUI.Box(new Rect(p.x+28,y,864,60),GUIContent.none,row);GUI.Label(new Rect(p.x+44,y+5,470,48),NameOf(x.item_id)+"   x"+x.quantity,body);bool old=GUI.enabled;GUI.enabled=!busy;if(GUI.Button(new Rect(p.x+720,y+10,150,40),"USAR",button))Use(x.item_id);GUI.enabled=old;}if(i==0)GUI.Label(new Rect(p.x+40,p.y+220,600,40),"No tienes objetos en esta categoría.",body);}
  void DrawShop(Rect p){int i=0;foreach(var x in shop){if(x.category!=category)continue;float y=p.y+230+i++*70;GUI.Box(new Rect(p.x+28,y,864,60),GUIContent.none,row);GUI.Label(new Rect(p.x+44,y+5,500,48),x.display_name+"  ·  "+x.honor_cost+" honor",body);bool old=GUI.enabled;GUI.enabled=!busy&&honor>=x.honor_cost;if(GUI.Button(new Rect(p.x+720,y+10,150,40),"COMPRAR",button))Buy(x.item_id);GUI.enabled=old;}}
  void Buy(string id){busy=true;StartCoroutine(SupabaseSyncClient.Instance.BuyAllianceItem(id,r=>{busy=false;honor=r.honor_points;message="Compra realizada: "+NameOf(id);Refresh();},Error));}
  void Donate(string resource){busy=true;StartCoroutine(SupabaseSyncClient.Instance.DonateAllianceResource(resource,100,r=>{busy=false;honor=r.honor_points;FindAnyObjectByType<FrostboundFrontierPrototype>()?.SpendResearchResourcesLocally(resource=="Wood"?100:0,resource=="Food"?100:0,0);message="Donación registrada. Honor +10";Refresh();},Error));}
  void Use(string id){string type=null,key=null;var game=FindAnyObjectByType<FrostboundFrontierPrototype>();if(id.StartsWith("speedup_")){if(game!=null&&game.HasActiveTraining){type="Training";key="barracks_01";}else if(game!=null&&game.HasActiveUpgrade){type="Building";key=game.ActiveUpgradeSlot;}else if(ResearchManager.Instance!=null&&!string.IsNullOrEmpty(ResearchManager.Instance.ActiveTechId)){type="Research";key=ResearchManager.Instance.ActiveTechId;}else{message="Inicia una mejora, investigación o entrenamiento.";return;}}
   busy=true;StartCoroutine(SupabaseSyncClient.Instance.UseInventoryItem(id,type,key,r=>{busy=false;if(type=="Training")game?.ApplyTrainingSpeedup(r.seconds_applied);if(type=="Research")ResearchManager.Instance?.RefreshFromCloud();if(id=="rss_wood_1k")game?.ApplyClaimedRewards(1000,0,0,0);else if(id=="rss_food_1k")game?.ApplyClaimedRewards(0,1000,0,0);else if(id=="alliance_chest")game?.ApplyClaimedRewards(500,500,2,0);if(id=="shield_8h"){message="Escudo de Paz activo durante 8 horas";FindAnyObjectByType<WorldMapManager>()?.ForceWorldRefresh();}else message="Objeto usado correctamente";Refresh();},Error));}
  void Error(string e){busy=false;message=e;}
  static string CategoryOf(string id)=>id.StartsWith("rss_")?"Resources":id.StartsWith("speedup_")?"Speedups":id.StartsWith("shield_")?"Combat":"Special";
  static string NameOf(string id){if(id=="speedup_1m")return "Acelerador 1 minuto";if(id=="speedup_5m")return "Acelerador 5 minutos";if(id=="rss_wood_1k")return "Caja 1,000 Madera";if(id=="rss_food_1k")return "Caja 1,000 Comida";if(id=="teleport_advanced")return "Teletransporte avanzado";if(id=="shield_8h")return "Escudo 8 horas";return "Cofre de Alianza";}
  void Ensure(){if(panel!=null)return;dark=Make(new Color(.02f,.065f,.095f,.99f));blue=Make(new Color(.07f,.32f,.5f,1));cyan=Make(new Color(.12f,.72f,.82f,1));rowTex=Make(new Color(.065f,.17f,.235f,1));panel=new GUIStyle(GUI.skin.box);panel.normal.background=dark;row=new GUIStyle(GUI.skin.box);row.normal.background=rowTex;body=new GUIStyle(GUI.skin.label){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,wordWrap=true};body.normal.textColor=Color.white;title=new GUIStyle(body){fontSize=26};button=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold};button.normal.background=blue;button.hover.background=blue;button.normal.textColor=Color.white;active=new GUIStyle(button);active.normal.background=cyan;FrostboundVisualTheme.ApplyPanel(panel);FrostboundVisualTheme.ApplyPanel(row,true);FrostboundVisualTheme.ApplyButton(button);FrostboundVisualTheme.ApplyButton(active,true);}
  static Texture2D Make(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}
 }
}
