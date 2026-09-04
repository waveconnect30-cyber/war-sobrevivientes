using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class WorldMapManager : MonoBehaviour
    {
        private const int MapSize = 1200;
        private const int MapCenter = MapSize / 2;
        private const int ChunkSize = 8;
        private const float TileSize = 1.35f;
        private const int MinimumRadius = 9;
        private const float WorldMinZoom = 2.8f;
        // Ten additional 1.6-unit steps beyond the previous far limit (22).
        private const float WorldMaxZoom = 38f;
        private const float ZoomButtonStep = 1.6f;
        private const float HighDetailZoomLimit = 14f;

        [Serializable] private sealed class WorldTileRow
        {
            public long id;
            public int x;
            public int y;
            public string tile_type;
            public string occupant_id;
            public int level;
            public string res_type;
            public int res_capacity;
            public int res_remaining;
            public string beast_kind;
            public int beast_power;
            public int beast_hp;
            public int beast_max_hp;
            public string reward_type;
            public int reward_amount;
            public string facility_key;
            public int facility_power;
            public string owner_alliance_id;
            public string buff_type;
            public float buff_percent;
            public string peace_shield_until;
            public int city_health;
            public string burning_until;
            public string updated_at;
        }
        [Serializable] private sealed class WorldTileRows { public WorldTileRow[] items; }

        private sealed class TileVisual
        {
            public GameObject root;
            public GameObject marker;
            public WorldTileRow data;
            public string markerSignature;
        }

        [Serializable] private sealed class LocalMarch
        {
            public string id;
            public int originX;
            public int originY;
            public int targetX;
            public int targetY;
            public string resourceType;
            public int payloadAmount;
            public string marchKind;
            public string beastKind;
            public int beastLevel;
            public int troopCount;
            public bool victory;
            public int casualties;
            public int wounded;
            public string lootType;
            public int lootAmount;
            public string heroId;
            public string heroKey;
            public float heroPowerBonus;
            public float heroSpeedBonus;
            public string status;
            public long phaseStartedTicks;
            public long phaseEndsTicks;
        }

        public static bool IsWorldMapActive { get; private set; }
        public static bool IsSelectionPanelOpen { get; private set; }

        private readonly Dictionary<Vector2Int, TileVisual> visibleTiles = new Dictionary<Vector2Int, TileVisual>();
        private readonly Dictionary<PrimitiveType, GameObjectPool> markerPools = new Dictionary<PrimitiveType, GameObjectPool>();
        private readonly Dictionary<GameObject, PrimitiveType> markerTypes = new Dictionary<GameObject, PrimitiveType>();
        private FrostboundFrontierPrototype prototype;
        private Camera mapCamera;
        private GameObject mapRoot;
        private GameObject worldBackdrop;
        private GameObject allianceTerritoryRoot;
        private GameObject poolRoot;
        private GameObjectPool tilePool;
        private GameObjectPool marchPool;
        private Vector3 colonyCameraPosition;
        private Quaternion colonyCameraRotation;
        private bool colonyOrthographic;
        private float colonyFieldOfView;
        private Rect colonyCameraRect;
        private Vector2Int selectedCoordinate = new Vector2Int(-1, -1);
        private GameObject selectionHighlight;
        private Vector2Int loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
        private int loadedRadiusX = -1;
        private int loadedRadiusY = -1;
        private bool loadedHighDetail;
        private Vector2 pointerDown;
        private Vector3 cameraDown;
        private bool pointerTracking;
        private float selectionInputBlockedUntil;
        private bool waitForPointerRelease;
        private bool selectionPanelPointerCaptured;
        private bool relocationMode;
        private bool relocationConfirmationOpen;
        private bool relocationRequestPending;
        private bool alliancePlacementMode;
        private bool alliancePlacementConfirmationOpen;
        private bool alliancePlacementPending;
        private Vector2Int alliancePlacementCoordinate = new Vector2Int(-1, -1);
        private string alliancePlacementType = "HQ";
        private bool cinematicZoomActive;
        private float cinematicZoomStartedAt;
        private float cinematicStartZoom;
        private float cinematicTargetZoom;
        private bool cinematicReturnsToGlobal;
        private Vector3 cinematicStartPosition;
        private Vector3 cinematicTargetPosition;
        private Vector2Int relocationCoordinate = new Vector2Int(-1, -1);
        private GameObject relocationPreview;
        private string mapStatus = "TERRENO LOCAL";
        private string actionMessage = "Arrastra para explorar los 1.440.000 sectores";
        private float actionMessageUntil;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle coordinateStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardBodyStyle;
        private GUIStyle cardCoordinateStyle;
        private GUIStyle cityPinStyle;
        private GUIStyle searchPinStyle;
        private Texture2D darkPanel;
        private Texture2D blueButton;
        private Texture2D orangeButton;
        private Texture2D cardPanel;
        private Texture2D cardPreview;
        private Texture2D cardInfo;
        private Texture2D greenPin;
        private Texture2D orangePin;
        private Material snowA;
        private Material snowB;
        private Material gridMaterial;
        private Material cityMaterial;
        private Material resourceMaterial;
        private Material beastMaterial;
        private Material fortressMaterial;
        private Material selectionMaterial;
        private Material validRelocationMaterial;
        private Material invalidRelocationMaterial;
        private Material allianceTerritoryMaterial;
        private LocalMarch activeMarch;
        private GameObject marchVisual;
        private int troopsToSend = 5;
        private bool assignElena = true;
        private const int LoadPerSnowInfantry = 50;
        private const float MarchSecondsPerTile = 0.12f;
        private const float GatheringSeconds = 8f;
        private const int GatherAmount = 250;
        private bool battleReportOpen;
        private string battleReportTitle;
        private string battleReportBody;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WorldMapManager>() == null)
                new GameObject(nameof(WorldMapManager)).AddComponent<WorldMapManager>();
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => FindAnyObjectByType<FrostboundFrontierPrototype>() != null);
            prototype = FindAnyObjectByType<FrostboundFrontierPrototype>();
            mapCamera = prototype.WorldCamera;
            mapRoot = new GameObject("Virtual World Map Chunks");
            mapRoot.SetActive(false);
            poolRoot = new GameObject("World Visual Pool");
            poolRoot.transform.SetParent(mapRoot.transform, false);
            poolRoot.SetActive(false);
            tilePool = new GameObjectPool(CreatePooledTile, poolRoot.transform);
            marchPool = new GameObjectPool(CreatePooledMarch, poolRoot.transform);
            allianceTerritoryRoot = new GameObject("Alliance Territory Overlays");
            allianceTerritoryRoot.transform.SetParent(mapRoot.transform, false);
            CreateMaterials();
            CreateWorldBackdrop();
            LoadLocalMarch();
        }

        private void OnDestroy()
        {
            IsWorldMapActive = false;
            IsSelectionPanelOpen = false;
        }

        private void Update()
        {
            if (mapCamera == null) return;
            UpdateMarch();
            if (!IsWorldMapActive) return;
            if (cinematicZoomActive) UpdateCinematicZoom();
            else HandleMapCamera();
            RefreshChunkIfNeeded();
            UpdateDynamicCulling();
            if (selectionHighlight != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.045f;
                selectionHighlight.transform.localScale = new Vector3(pulse, 1f, pulse);
            }
        }

        private bool HighDetailActive => mapCamera != null && mapCamera.orthographicSize <= HighDetailZoomLimit;

        private void ToggleWorldMap()
        {
            if (prototype == null || mapCamera == null) return;
            IsWorldMapActive = !IsWorldMapActive;
            if (IsWorldMapActive) EnterWorldMap();
            else ExitWorldMap();
        }

        private void EnterWorldMap()
        {
            ResetRelocationStateSilently();
            ClearSelection();
            colonyCameraPosition = mapCamera.transform.position;
            colonyCameraRotation = mapCamera.transform.rotation;
            colonyOrthographic = mapCamera.orthographic;
            colonyFieldOfView = mapCamera.fieldOfView;
            colonyCameraRect = mapCamera.rect;
            prototype.ColonyRoot.SetActive(false);
            mapRoot.SetActive(true);
            RenderSettings.fog = false;
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = WorldMinZoom;
            mapCamera.rect = new Rect(0f, 0f, 1f, 1f);
            Vector2Int cityCoordinate = GetCityCoordinate();
            Vector3 cityWorldPosition = CoordinateToWorld(cityCoordinate.x, cityCoordinate.y);
            mapCamera.transform.SetPositionAndRotation(
                new Vector3(cityWorldPosition.x, 60f, cityWorldPosition.z),
                Quaternion.Euler(90f, 0f, 0f));
            mapCamera.backgroundColor = new Color(0.08f, 0.14f, 0.19f);
            loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
            loadedRadiusX = -1;
            loadedRadiusY = -1;
            selectionInputBlockedUntil = Time.unscaledTime + 0.35f;
            waitForPointerRelease = true;
            RefreshChunkIfNeeded();
        }

        private void ExitWorldMap()
        {
            ResetRelocationStateSilently();
            ClearSelection();
            mapRoot.SetActive(false);
            prototype.ColonyRoot.SetActive(true);
            RenderSettings.fog = true;
            mapCamera.orthographic = colonyOrthographic;
            mapCamera.fieldOfView = colonyFieldOfView;
            mapCamera.rect = colonyCameraRect;
            mapCamera.transform.SetPositionAndRotation(colonyCameraPosition, colonyCameraRotation);
            mapCamera.backgroundColor = new Color(0.37f, 0.5f, 0.62f);
            pointerTracking = false;
        }

        private void ResetRelocationStateSilently()
        {
            relocationMode = false;
            relocationConfirmationOpen = false;
            relocationRequestPending = false;
            relocationCoordinate = new Vector2Int(-1, -1);
            selectionPanelPointerCaptured = false;
            if (relocationPreview != null) relocationPreview.SetActive(false);
            cinematicZoomActive = false;
            alliancePlacementMode = false;
            alliancePlacementConfirmationOpen = false;
            alliancePlacementPending = false;
            alliancePlacementCoordinate = new Vector2Int(-1, -1);
        }

        public void BeginAllianceHQPlacement()
        {
            BeginAllianceStructurePlacement("HQ");
        }

        public void BeginAllianceFlagPlacement()
        {
            BeginAllianceStructurePlacement("Flag");
        }

        private void BeginAllianceStructurePlacement(string structureType)
        {
            if (!AllianceManager.CanBuildTerritory)
            {
                actionMessage = "SOLO LÍDERES U OFICIALES PUEDEN CONSTRUIR";
                actionMessageUntil = Time.unscaledTime + 4f;
                return;
            }
            if (!IsWorldMapActive) ToggleWorldMap();
            ResetRelocationStateSilently();
            ClearSelection();
            alliancePlacementMode = true;
            alliancePlacementType = structureType;
            Vector2Int city = GetCityCoordinate();
            SetAlliancePlacementCandidate(Mathf.Clamp(city.x + 3, 0, MapSize - 1), city.y);
            actionMessage = structureType == "Flag" ? "TOCA UNA TILE VACÍA CONECTADA AL TERRITORIO" : "TOCA UNA TILE VACÍA PARA DESPLEGAR EL HQ";
            actionMessageUntil = Time.unscaledTime + 5f;
        }

        public void ForceWorldRefresh()
        {
            loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
            if (IsWorldMapActive) RefreshChunkIfNeeded();
        }

        private bool IsMaximumZoomOut => mapCamera != null && mapCamera.orthographicSize >= WorldMaxZoom - 0.25f;

        private void StartCinematicZoom(Vector2Int destination)
        {
            Vector3 world = CoordinateToWorld(destination.x, destination.y);
            cinematicStartPosition = mapCamera.transform.position;
            cinematicTargetPosition = new Vector3(world.x, 60f, world.z);
            cinematicStartZoom = mapCamera.orthographicSize;
            cinematicTargetZoom = WorldMinZoom;
            cinematicReturnsToGlobal = false;
            cinematicZoomStartedAt = Time.unscaledTime;
            cinematicZoomActive = true;
            pointerTracking = false;
            actionMessage = "LOCALIZANDO SECTOR X:" + destination.x + "  Y:" + destination.y;
            actionMessageUntil = Time.unscaledTime + 1.5f;
        }

        private void UpdateCinematicZoom()
        {
            const float duration = 1.65f;
            float t = Mathf.Clamp01((Time.unscaledTime - cinematicZoomStartedAt) / duration);
            float eased = t * t * (3f - 2f * t);
            mapCamera.transform.position = Vector3.Lerp(cinematicStartPosition, cinematicTargetPosition, eased);
            mapCamera.orthographicSize = Mathf.Lerp(cinematicStartZoom, cinematicTargetZoom, eased);
            if (t < 1f) return;
            cinematicZoomActive = false;
            mapCamera.transform.position = cinematicTargetPosition;
            mapCamera.orthographicSize = cinematicTargetZoom;
            if (cinematicReturnsToGlobal)
            {
                ClearSelection();
                actionMessage = "VISTA GLOBAL DEL MUNDO";
                actionMessageUntil = Time.unscaledTime + 2f;
            }
            else
            {
                ShowSelectionHighlight(selectedCoordinate.x, selectedCoordinate.y);
            }
        }

        private void StartGlobalView()
        {
            cinematicStartPosition = mapCamera.transform.position;
            cinematicTargetPosition = mapCamera.transform.position;
            cinematicStartZoom = mapCamera.orthographicSize;
            cinematicTargetZoom = WorldMaxZoom;
            cinematicReturnsToGlobal = true;
            cinematicZoomStartedAt = Time.unscaledTime;
            cinematicZoomActive = true;
            pointerTracking = false;
        }

        private void HandleMapCamera()
        {
            float keyboardX = Input.GetAxisRaw("Horizontal");
            float keyboardY = Input.GetAxisRaw("Vertical");
            mapCamera.transform.position += new Vector3(keyboardX, 0f, keyboardY) * (12f * Time.unscaledDeltaTime);

            Vector2 pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            bool pressed = Input.touchCount == 1 || Input.GetMouseButton(0);
            // OnGUI processes the release after Update. Once a press begins on the
            // card, keep the world input disabled through the complete click so the
            // tile below it can never receive the same release event.
            if (selectionPanelPointerCaptured)
            {
                pointerTracking = false;
                if (!pressed)
                {
                    selectionPanelPointerCaptured = false;
                    selectionInputBlockedUntil = Time.unscaledTime + 0.08f;
                }
                return;
            }
            if (waitForPointerRelease)
            {
                if (!pressed) waitForPointerRelease = false;
                pointerTracking = false;
                return;
            }
            if (Time.unscaledTime < selectionInputBlockedUntil)
            {
                pointerTracking = false;
                return;
            }
            Rect viewport = mapCamera.pixelRect;
            if (pressed && viewport.Contains(pointer) && Screen.safeArea.Contains(pointer) && pointer.y > Screen.safeArea.yMin + 80f && pointer.y < Screen.safeArea.yMax - 80f && !IsPointerOverSelectionCard(pointer) && !MobilePerformanceManager.IsPointerOverUi(pointer))
            {
                if (!pointerTracking)
                {
                    pointerTracking = true;
                    pointerDown = pointer;
                    cameraDown = mapCamera.transform.position;
                }
                else
                {
                    Vector2 delta = pointer - pointerDown;
                    mapCamera.transform.position = cameraDown + new Vector3(-delta.x, 0f, -delta.y) * (mapCamera.orthographicSize / 420f);
                }
            }
            else if (pointerTracking)
            {
                if ((pointer - pointerDown).sqrMagnitude < 64f) SelectAtScreenPoint(pointer);
                pointerTracking = false;
            }

            float zoom = -Input.mouseScrollDelta.y;
            if (Input.touchCount == 2)
            {
                pointerTracking = false;
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                float previous = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                zoom = (previous - (a.position - b.position).magnitude) * 0.02f;
            }
            mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize + zoom, WorldMinZoom, WorldMaxZoom);

            Vector3 p = mapCamera.transform.position;
            float extent = (MapCenter - 1) * TileSize;
            p.x = Mathf.Clamp(p.x, -extent, extent);
            p.z = Mathf.Clamp(p.z, -extent, extent);
            p.y = 60f;
            mapCamera.transform.position = p;
        }

        private void UpdateDynamicCulling()
        {
            float halfX = mapCamera.orthographicSize * mapCamera.aspect + TileSize;
            float halfY = mapCamera.orthographicSize + TileSize;
            Vector3 cameraPosition = mapCamera.transform.position;
            foreach (TileVisual visual in visibleTiles.Values)
            {
                Vector3 position = CoordinateToWorld(visual.data.x, visual.data.y);
                bool visible = Mathf.Abs(position.x - cameraPosition.x) <= halfX && Mathf.Abs(position.z - cameraPosition.z) <= halfY;
                if (visual.root != null && visual.root.activeSelf != visible) visual.root.SetActive(visible);
                if (visual.root == null && visual.marker != null && visual.marker.activeSelf != visible) visual.marker.SetActive(visible);
            }
            if (marchVisual != null)
            {
                Vector3 position = marchVisual.transform.position;
                bool visible = Mathf.Abs(position.x - cameraPosition.x) <= halfX && Mathf.Abs(position.z - cameraPosition.z) <= halfY;
                marchVisual.SetActive(IsWorldMapActive && visible);
            }
        }

        private bool IsPointerOverSelectionCard(Vector2 screenPointer)
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(safe.width / 1280f, 0.75f, 1.35f);
            float logicalWidth = safe.width / scale;
            float logicalHeight = safe.height / scale;
            Vector2 guiPoint = new Vector2((screenPointer.x - safe.x) / scale, (safe.yMax - screenPointer.y) / scale);
            if (relocationMode)
            {
                if (new Rect(logicalWidth * 0.5f - 310f, logicalHeight - 166f, 620f, 94f).Contains(guiPoint)) return true;
                if (relocationConfirmationOpen && new Rect(logicalWidth * 0.5f - 235f, logicalHeight * 0.5f - 105f, 470f, 210f).Contains(guiPoint)) return true;
            }
            if (alliancePlacementMode)
            {
                if (new Rect(logicalWidth * 0.5f - 310f, logicalHeight - 166f, 620f, 94f).Contains(guiPoint)) return true;
                if (alliancePlacementConfirmationOpen && new Rect(logicalWidth * 0.5f - 235f, logicalHeight * 0.5f - 105f, 470f, 210f).Contains(guiPoint)) return true;
            }
            if (selectedCoordinate.x < 0) return false;
            float cardHeight = 344f;
            if (visibleTiles.TryGetValue(selectedCoordinate, out TileVisual selected) && selected?.data != null)
            {
                bool rallyTarget = selected.data.tile_type == "Fortress" || (selected.data.tile_type == "Beast" && selected.data.level >= 2);
                if (selected.data.tile_type == "ResourceNode" || selected.data.tile_type == "Beast")
                    cardHeight = 452f;
            }
            return new Rect(logicalWidth * 0.5f - 245f, 98f, 490f, cardHeight).Contains(guiPoint);
        }

        private void RefreshChunkIfNeeded()
        {
            Vector2Int center = CameraCoordinate();
            Vector2Int chunk = new Vector2Int(center.x / ChunkSize, center.y / ChunkSize);
            int radiusX = Mathf.Max(MinimumRadius, Mathf.CeilToInt(mapCamera.orthographicSize * mapCamera.aspect / TileSize) + 2);
            int radiusY = Mathf.Max(MinimumRadius, Mathf.CeilToInt(mapCamera.orthographicSize / TileSize) + 2);
            bool highDetail = HighDetailActive;
            // Zooming changes the visible bounds even while the camera remains in
            // the same chunk. Rebuild when either radius changes so the detailed
            // terrain always fills the viewport instead of remaining a tiny island.
            if (chunk == loadedChunk && radiusX == loadedRadiusX && radiusY == loadedRadiusY && highDetail == loadedHighDetail) return;
            if (highDetail != loadedHighDetail) ReleaseAllVisibleTiles();
            loadedChunk = chunk;
            loadedRadiusX = radiusX;
            loadedRadiusY = radiusY;
            loadedHighDetail = highDetail;

            int minX = Mathf.Max(0, center.x - radiusX);
            int maxX = Mathf.Min(MapSize - 1, center.x + radiusX);
            int minY = Mathf.Max(0, center.y - radiusY);
            int maxY = Mathf.Min(MapSize - 1, center.y + radiusY);
            CullAndCreate(minX, maxX, minY, maxY);

            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud != null && cloud.CanQueryWorld)
            {
                StartCoroutine(cloud.FetchWorldTiles(minX, maxX, minY, maxY, ApplyRemoteTiles, error => mapStatus = error));
                StartCoroutine(cloud.FetchAllianceStructures(Mathf.Max(0, minX - 12), Mathf.Min(MapSize - 1, maxX + 12),
                    Mathf.Max(0, minY - 12), Mathf.Min(MapSize - 1, maxY + 12), ApplyAllianceStructures, error => mapStatus = error));
            }
            else mapStatus = "TERRENO LOCAL · SIN SESIÓN";
        }

        private void ApplyAllianceStructures(SupabaseSyncClient.AllianceStructureCloudState[] rows)
        {
            if (allianceTerritoryRoot == null) return;
            for (int i = allianceTerritoryRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(allianceTerritoryRoot.transform.GetChild(i).gameObject);
            foreach (SupabaseSyncClient.AllianceStructureCloudState row in rows)
            {
                CreateTerritoryBorder(row);
                CreateAllianceStructureMarker(row);
            }
        }

        private void CreateTerritoryBorder(SupabaseSyncClient.AllianceStructureCloudState row)
        {
            float radius = Mathf.Max(1, row.territory_radius) * TileSize + TileSize * .5f;
            Vector3 center = CoordinateToWorld(row.x, row.y) + Vector3.up * .16f;
            GameObject border = new GameObject("[" + AllianceManager.LocalTag + "] Territory " + row.x + "," + row.y);
            border.transform.SetParent(allianceTerritoryRoot.transform, false);
            LineRenderer line = border.AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = 4;
            line.useWorldSpace = true;
            line.widthMultiplier = .12f;
            line.sharedMaterial = allianceTerritoryMaterial;
            line.startColor = line.endColor = new Color(.08f, .95f, 1f, 1f);
            line.SetPositions(new[] { center + new Vector3(-radius,0f,-radius), center + new Vector3(-radius,0f,radius),
                center + new Vector3(radius,0f,radius), center + new Vector3(radius,0f,-radius) });
        }

        private void CreateAllianceStructureMarker(SupabaseSyncClient.AllianceStructureCloudState row)
        {
            GameObject marker = GameObject.CreatePrimitive(row.structure_type == "HQ" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            marker.name = "Alliance " + row.structure_type + " " + row.x + "," + row.y;
            marker.transform.SetParent(allianceTerritoryRoot.transform, false);
            marker.transform.position = CoordinateToWorld(row.x, row.y) + new Vector3(0f, 1.05f, 0f);
            marker.transform.localScale = row.structure_type == "HQ" ? new Vector3(1.05f,.85f,1.05f) : new Vector3(.4f,1.5f,.4f);
            marker.GetComponent<Renderer>().sharedMaterial = allianceTerritoryMaterial;
            Destroy(marker.GetComponent<Collider>());
        }

        private void CullAndCreate(int minX, int maxX, int minY, int maxY)
        {
            List<Vector2Int> remove = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, TileVisual> pair in visibleTiles)
                if (pair.Key.x < minX || pair.Key.x > maxX || pair.Key.y < minY || pair.Key.y > maxY)
                    remove.Add(pair.Key);
            foreach (Vector2Int key in remove)
            {
                ReleaseTile(visibleTiles[key]);
                visibleTiles.Remove(key);
            }

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int key = new Vector2Int(x, y);
                if (!visibleTiles.ContainsKey(key)) visibleTiles.Add(key, CreateTile(x, y));
            }
        }

        private TileVisual CreateTile(int x, int y)
        {
            GameObject tile = null;
            if (HighDetailActive)
            {
                tile = tilePool.Get(mapRoot.transform);
                tile.name = "Sector " + x + "," + y;
                tile.transform.position = CoordinateToWorld(x, y);
                tile.GetComponent<Renderer>().sharedMaterial = ((x + y) & 1) == 0 ? snowA : snowB;
            }
            TileVisual visual = new TileVisual { root = tile, data = new WorldTileRow { x = x, y = y, tile_type = "Empty", level = 1 } };
            Vector2Int city = GetCityCoordinate();
            if (x == city.x && y == city.y)
            {
                visual.data.tile_type = "PlayerCity";
                visual.data.occupant_id = SupabaseSyncClient.Instance?.CurrentUserId;
                UpdateMarker(visual);
            }
            else if ((x == city.x + 2 && y == city.y) ||
                     (x == city.x - 2 && y == city.y + 1) ||
                     (x == city.x + 1 && y == city.y - 2) ||
                     IsProceduralResourceNode(x, y))
            {
                int kind = Mathf.Abs((x * 17 + y * 31) % 3);
                visual.data.tile_type = "ResourceNode";
                visual.data.res_type = kind == 0 ? "Wood" : kind == 1 ? "Food" : "Coal";
                visual.data.level = 1 + Mathf.Abs((x + y) % 5);
                visual.data.res_capacity = 5000;
                visual.data.res_remaining = 5000;
                UpdateMarker(visual);
            }
            return visual;
        }

        private static bool IsProceduralResourceNode(int x, int y)
        {
            return Mathf.Abs((x * 73856093) ^ (y * 19349663)) % 97 == 0;
        }

        private Vector2Int GetCityCoordinate()
        {
            string currentUser = SupabaseSyncClient.Instance?.CurrentUserId;
            foreach (KeyValuePair<Vector2Int, TileVisual> entry in visibleTiles)
            {
                WorldTileRow row = entry.Value?.data;
                if (row?.tile_type == "PlayerCity" &&
                    (string.IsNullOrWhiteSpace(currentUser) || row.occupant_id == currentUser))
                    return entry.Key;
            }

            return new Vector2Int(
                Mathf.Clamp(PlayerPrefs.GetInt("frostbound-world-city-x", MapCenter), 0, MapSize - 1),
                Mathf.Clamp(PlayerPrefs.GetInt("frostbound-world-city-y", MapCenter), 0, MapSize - 1));
        }

        private void ApplyRemoteTiles(string json)
        {
            WorldTileRows wrapper = JsonUtility.FromJson<WorldTileRows>("{\"items\":" + json + "}");
            if (wrapper?.items == null) return;
            foreach (WorldTileRow row in wrapper.items)
            {
                Vector2Int key = new Vector2Int(row.x, row.y);
                if (!visibleTiles.TryGetValue(key, out TileVisual visual)) continue;
                visual.data = row;
                UpdateMarker(visual);
            }
            mapStatus = "SUPABASE · " + wrapper.items.Length + " OBJETOS EN RANGO";
        }

        private void UpdateMarker(TileVisual visual)
        {
            bool detailed = HighDetailActive;
            string signature = visual.data.tile_type + ":" + detailed + ":" + IsFuture(visual.data.peace_shield_until) + ":" + IsFuture(visual.data.burning_until);
            if (visual.marker != null && visual.markerSignature == signature) return;
            ReleaseMarker(visual);
            if (visual.data.tile_type == "Empty") return;

            PrimitiveType shape = visual.data.tile_type == "PlayerCity" || visual.data.tile_type == "Fortress"
                ? PrimitiveType.Cube : visual.data.tile_type == "ResourceNode" ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
            GameObject marker = GetMarkerPool(shape).Get(visual.root != null ? visual.root.transform : mapRoot.transform);
            marker.name = visual.data.tile_type;
            marker.transform.position = CoordinateToWorld(visual.data.x, visual.data.y) + new Vector3(0f, detailed ? 1.6f : .32f, 0f);
            float size = detailed ? (visual.data.tile_type == "Fortress" ? 0.85f : 0.58f) : .22f;
            marker.transform.localScale = detailed ? new Vector3(size, visual.data.tile_type == "PlayerCity" ? 2.2f : 1.35f, size) : new Vector3(size, .08f, size);
            Sprite nodeSprite = FrostboundVisualTheme.NodeSprite(visual.data.tile_type, visual.data.res_type);
            Renderer meshRenderer = marker.GetComponent<Renderer>();
            SpriteRenderer spriteRenderer = marker.GetComponentInChildren<SpriteRenderer>(true);
            if (nodeSprite != null)
            {
                if (spriteRenderer == null)
                {
                    GameObject iconObject = new GameObject("Casual GUI Node Icon");
                    iconObject.transform.SetParent(marker.transform, false);
                    spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
                }
                spriteRenderer.sprite = nodeSprite;
                spriteRenderer.color = visual.data.tile_type == "Beast" ? new Color(.45f, .9f, 1f) :
                    visual.data.res_type == "Wood" ? new Color(.35f, .85f, .42f) :
                    visual.data.res_type == "Food" ? new Color(1f, .62f, .28f) : new Color(.62f, .7f, .78f);
                spriteRenderer.enabled = true;
                if (meshRenderer != null) meshRenderer.enabled = false;
                marker.transform.localScale = Vector3.one;
                marker.transform.rotation = Quaternion.identity;
                spriteRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float spriteSize = Mathf.Max(.001f, Mathf.Max(nodeSprite.bounds.size.x, nodeSprite.bounds.size.y));
                float desiredSize = detailed ? .82f : .24f;
                spriteRenderer.transform.localScale = Vector3.one * (desiredSize / spriteSize);
            }
            else
            {
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (meshRenderer != null) meshRenderer.enabled = true;
            }
            marker.GetComponent<Renderer>().sharedMaterial = MaterialFor(visual.data.tile_type);
            if (detailed && visual.data.tile_type == "PlayerCity")
            {
                if (IsFuture(visual.data.peace_shield_until))
                {
                    GameObject dome=GameObject.CreatePrimitive(PrimitiveType.Sphere);dome.name="Peace Shield";dome.transform.SetParent(marker.transform,false);dome.transform.localPosition=Vector3.zero;dome.transform.localScale=new Vector3(2.1f,1.15f,2.1f);dome.GetComponent<Renderer>().sharedMaterial=resourceMaterial;Destroy(dome.GetComponent<Collider>());
                }
                if (IsFuture(visual.data.burning_until))
                {
                    GameObject fire=GameObject.CreatePrimitive(PrimitiveType.Sphere);fire.name="City Fire";fire.transform.SetParent(marker.transform,false);fire.transform.localPosition=new Vector3(0,.7f,0);fire.transform.localScale=Vector3.one*.45f;fire.GetComponent<Renderer>().sharedMaterial=beastMaterial;Destroy(fire.GetComponent<Collider>());
                }
            }
            visual.marker = marker;
            visual.markerSignature = signature;
        }

        private GameObject CreatePooledTile()
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.localScale = new Vector3(TileSize * .94f, .08f, TileSize * .94f);
            Renderer renderer = tile.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return tile;
        }

        private GameObjectPool GetMarkerPool(PrimitiveType shape)
        {
            if (markerPools.TryGetValue(shape, out GameObjectPool pool)) return pool;
            pool = new GameObjectPool(() =>
            {
                GameObject marker = GameObject.CreatePrimitive(shape);
                markerTypes[marker] = shape;
                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return marker;
            }, poolRoot.transform);
            markerPools.Add(shape, pool);
            return pool;
        }

        private void ReleaseMarker(TileVisual visual)
        {
            if (visual?.marker == null) return;
            for (int i = visual.marker.transform.childCount - 1; i >= 0; i--)
                Destroy(visual.marker.transform.GetChild(i).gameObject);
            if (markerTypes.TryGetValue(visual.marker, out PrimitiveType shape)) GetMarkerPool(shape).Release(visual.marker);
            else visual.marker.SetActive(false);
            visual.marker = null;
            visual.markerSignature = null;
        }

        private void ReleaseTile(TileVisual visual)
        {
            if (visual == null) return;
            ReleaseMarker(visual);
            if (visual.root != null) tilePool.Release(visual.root);
            visual.root = null;
        }

        private void ReleaseAllVisibleTiles()
        {
            foreach (TileVisual visual in visibleTiles.Values) ReleaseTile(visual);
            visibleTiles.Clear();
        }

        private static bool IsFuture(string value)=>!string.IsNullOrWhiteSpace(value)&&DateTime.TryParse(value,out DateTime date)&&date.ToUniversalTime()>DateTime.UtcNow;

        private Material MaterialFor(string type)
        {
            if (type == "PlayerCity") return cityMaterial;
            if (type == "ResourceNode") return resourceMaterial;
            if (type == "Beast") return beastMaterial;
            return fortressMaterial;
        }

        private void StartGatheringMarch(TileVisual node)
        {
            if (activeMarch != null)
            {
                actionMessage = "YA HAY UNA MARCHA ACTIVA";
                actionMessageUntil = Time.unscaledTime + 3f;
                return;
            }
            if (node?.data == null || node.data.tile_type != "ResourceNode" || node.data.res_remaining <= 0) return;
            if (prototype.SnowInfantry <= 0) { actionMessage = "NECESITAS INFANTERÍA DE NIEVE"; actionMessageUntil = Time.unscaledTime + 3f; return; }
            troopsToSend = Mathf.Clamp(troopsToSend, 1, prototype.SnowInfantry);
            Vector2Int city = GetCityCoordinate();
            float distance = Vector2.Distance(city, selectedCoordinate);
            float speedMultiplier = (assignElena && prototype.ElenaUnlocked ? 0.8f : 1f) * ResearchManager.MarchDurationMultiplier;
            float seconds = Mathf.Max(2f, distance * MarchSecondsPerTile * speedMultiplier);
            activeMarch = new LocalMarch
            {
                id = Guid.NewGuid().ToString(), originX = city.x, originY = city.y,
                targetX = selectedCoordinate.x, targetY = selectedCoordinate.y,
                resourceType = node.data.res_type, payloadAmount = Mathf.Min(troopsToSend * LoadPerSnowInfantry, node.data.res_remaining),
                marchKind = "Gathering", troopCount = troopsToSend,
                heroId = assignElena ? prototype.ElenaHeroId : null, heroKey = assignElena ? "elena_ice_huntress" : null,
                heroPowerBonus = assignElena ? 0.15f : 0f, heroSpeedBonus = assignElena ? 0.20f : 0f,
                status = "Marching", phaseStartedTicks = DateTime.UtcNow.Ticks,
                phaseEndsTicks = DateTime.UtcNow.AddSeconds(seconds).Ticks
            };
            SaveActiveMarch();
            ClearSelection();
            actionMessage = "MARCHA EN CAMINO · " + Mathf.CeilToInt(seconds) + " S";
            actionMessageUntil = Time.unscaledTime + 3f;
        }

        private void StartBeastMarch(TileVisual beast)
        {
            if (activeMarch != null) { actionMessage = "YA HAY UNA MARCHA ACTIVA"; actionMessageUntil = Time.unscaledTime + 3f; return; }
            if (beast?.data == null || beast.data.tile_type != "Beast") return;
            int available = prototype.SnowInfantry;
            if (available <= 0) { actionMessage = "NECESITAS INFANTERÍA DE NIEVE"; actionMessageUntil = Time.unscaledTime + 3f; return; }
            troopsToSend = Mathf.Clamp(troopsToSend, 1, available);
            Vector2Int city = GetCityCoordinate();
            float speedMultiplier = (assignElena && prototype.ElenaUnlocked ? 0.8f : 1f) * ResearchManager.MarchDurationMultiplier;
            float seconds = Mathf.Max(2f, Vector2.Distance(city, selectedCoordinate) * MarchSecondsPerTile * speedMultiplier);
            activeMarch = new LocalMarch
            {
                id = Guid.NewGuid().ToString(), originX = city.x, originY = city.y,
                targetX = selectedCoordinate.x, targetY = selectedCoordinate.y,
                marchKind = "Attack", beastKind = beast.data.beast_kind, beastLevel = beast.data.level,
                troopCount = troopsToSend, resourceType = null, payloadAmount = 0,
                heroId = assignElena ? prototype.ElenaHeroId : null, heroKey = assignElena ? "elena_ice_huntress" : null,
                heroPowerBonus = assignElena ? 0.15f : 0f, heroSpeedBonus = assignElena ? 0.20f : 0f,
                status = "Marching", phaseStartedTicks = DateTime.UtcNow.Ticks,
                phaseEndsTicks = DateTime.UtcNow.AddSeconds(seconds).Ticks
            };
            SaveActiveMarch();
            ClearSelection();
            actionMessage = "MARCHA PVE EN CAMINO · " + Mathf.CeilToInt(seconds) + " S";
            actionMessageUntil = Time.unscaledTime + 3f;
        }

        private void StartCityMarch(TileVisual city)
        {
            if(activeMarch!=null||city?.data==null||city.data.tile_type!="PlayerCity")return;
            if(city.data.occupant_id==SupabaseSyncClient.Instance?.CurrentUserId){actionMessage="ESTA ES TU CIUDAD";actionMessageUntil=Time.unscaledTime+3;return;}
            if(IsFuture(city.data.peace_shield_until)){actionMessage="CIUDAD PROTEGIDA POR ESCUDO";actionMessageUntil=Time.unscaledTime+3;return;}
            int available=prototype.SnowInfantry;if(available<=0){actionMessage="NECESITAS INFANTERÍA";actionMessageUntil=Time.unscaledTime+3;return;}troopsToSend=Mathf.Clamp(troopsToSend,1,available);Vector2Int origin=GetCityCoordinate();float seconds=Mathf.Max(2f,Vector2.Distance(origin,selectedCoordinate)*MarchSecondsPerTile*ResearchManager.MarchDurationMultiplier);
            activeMarch=new LocalMarch{id=Guid.NewGuid().ToString(),originX=origin.x,originY=origin.y,targetX=selectedCoordinate.x,targetY=selectedCoordinate.y,marchKind="CityAttack",troopCount=troopsToSend,heroId=assignElena?prototype.ElenaHeroId:null,heroKey=assignElena?"elena_ice_huntress":null,heroPowerBonus=assignElena ? .15f : 0f,heroSpeedBonus=assignElena ? .20f : 0f,status="Marching",phaseStartedTicks=DateTime.UtcNow.Ticks,phaseEndsTicks=DateTime.UtcNow.AddSeconds(seconds).Ticks};SaveActiveMarch();ClearSelection();actionMessage="MARCHA PVP EN CAMINO · "+Mathf.CeilToInt(seconds)+" S";actionMessageUntil=Time.unscaledTime+3;
        }

        private void UpdateMarch()
        {
            if (activeMarch == null) return;
            long now = DateTime.UtcNow.Ticks;
            double duration = Math.Max(1d, activeMarch.phaseEndsTicks - activeMarch.phaseStartedTicks);
            float progress = Mathf.Clamp01((float)((now - activeMarch.phaseStartedTicks) / duration));
            Vector3 origin = CoordinateToWorld(activeMarch.originX, activeMarch.originY) + Vector3.up * 0.55f;
            Vector3 target = CoordinateToWorld(activeMarch.targetX, activeMarch.targetY) + Vector3.up * 0.55f;
            EnsureMarchVisual();
            if (marchVisual != null)
            {
                marchVisual.SetActive(IsWorldMapActive);
                marchVisual.transform.position = activeMarch.status == "Return"
                    ? Vector3.Lerp(target, origin, progress)
                    : activeMarch.status == "Gathering" ? target : Vector3.Lerp(origin, target, progress);
            }
            if (now < activeMarch.phaseEndsTicks) return;

            if (activeMarch.status == "Marching")
            {
                if (activeMarch.marchKind == "Attack" || activeMarch.marchKind == "CityAttack")
                {
                    activeMarch.status = "Battle";
                    LocalMarch battleMarch = activeMarch;
                    SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
                    if (cloud != null && cloud.CanQueryWorld)
                    {
                        if(battleMarch.marchKind=="CityAttack")SaveActiveMarch(()=>StartCoroutine(cloud.ProcessCityAttack(battleMarch.id,result=>BeginCityBattleReturn(battleMarch,result),error=>CancelFailedCityAttack(battleMarch,error))));
                        else {SaveActiveMarch();StartCoroutine(cloud.ProcessBeastBattle(battleMarch.id,result => BeginBattleReturn(battleMarch, result),error => { Debug.LogWarning(error); actionMessage = "ERROR EN BATALLA PVE"; actionMessageUntil = Time.unscaledTime + 4f; }));}
                    }
                    else if (battleMarch.marchKind == "CityAttack")
                    {
                        CancelFailedCityAttack(battleMarch, "PVP_REQUIRES_CONNECTION");
                        return;
                    }
                    activeMarch.phaseEndsTicks = DateTime.UtcNow.AddYears(1).Ticks;
                    return;
                }
                activeMarch.status = "Gathering";
                activeMarch.phaseStartedTicks = now;
                activeMarch.phaseEndsTicks = DateTime.UtcNow.AddSeconds(GatheringSeconds).Ticks;
                SaveActiveMarch();
            }
            else if (activeMarch.status == "Gathering")
            {
                Vector2Int targetKey = new Vector2Int(activeMarch.targetX, activeMarch.targetY);
                if (visibleTiles.TryGetValue(targetKey, out TileVisual node))
                    node.data.res_remaining = Mathf.Max(0, node.data.res_remaining - activeMarch.payloadAmount);
                float distance = Vector2.Distance(new Vector2(activeMarch.originX, activeMarch.originY), new Vector2(activeMarch.targetX, activeMarch.targetY));
                activeMarch.status = "Return";
                activeMarch.phaseStartedTicks = now;
                activeMarch.phaseEndsTicks = DateTime.UtcNow.AddSeconds(Mathf.Max(2f, distance * MarchSecondsPerTile * (1f - activeMarch.heroSpeedBonus) * ResearchManager.MarchDurationMultiplier)).Ticks;
                SaveActiveMarch();
            }
            else
            {
                if (activeMarch.marchKind == "Attack" || activeMarch.marchKind == "CityAttack") { FinishBattleMarch(activeMarch); return; }
                SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
                LocalMarch completedMarch = activeMarch;
                if (cloud != null && cloud.CanQueryWorld)
                    StartCoroutine(cloud.CompleteGatherMarch(completedMarch.id,
                        delivered => FinishMarchLocally(completedMarch, delivered),
                        error => { Debug.LogWarning(error); actionMessage = "ERROR AL ENTREGAR RECURSOS"; actionMessageUntil = Time.unscaledTime + 4f; }));
                else FinishMarchLocally(completedMarch, completedMarch.payloadAmount);
                if (activeMarch != null) activeMarch.phaseEndsTicks = DateTime.UtcNow.AddYears(1).Ticks;
            }
        }

        private void BeginBattleReturn(LocalMarch march, SupabaseSyncClient.BeastBattleResult result)
        {
            if (activeMarch != march || result == null) return;
            march.victory = result.victory;
            march.casualties = result.casualties;
            march.wounded = result.wounded;
            march.lootType = result.loot_type;
            march.lootAmount = result.loot_amount;
            prototype.ApplyBattleOutcome(result.casualties, result.wounded, result.loot_type, result.loot_amount);
            if (result.victory && march.beastLevel == 1 && (string.IsNullOrEmpty(march.beastKind) || march.beastKind.IndexOf("Wolf", StringComparison.OrdinalIgnoreCase) >= 0 || march.beastKind.IndexOf("Lobo", StringComparison.OrdinalIgnoreCase) >= 0))
                OnboardingManager.Notify("BeastDefeated");
            float distance = Vector2.Distance(new Vector2(march.originX, march.originY), new Vector2(march.targetX, march.targetY));
            march.status = "Return";
            march.phaseStartedTicks = DateTime.UtcNow.Ticks;
            march.phaseEndsTicks = DateTime.UtcNow.AddSeconds(Mathf.Max(2f, distance * MarchSecondsPerTile * (1f - march.heroSpeedBonus) * ResearchManager.MarchDurationMultiplier)).Ticks;
            SaveActiveMarch();
        }

        private void BeginCityBattleReturn(LocalMarch march,SupabaseSyncClient.CityAttackResult result)
        {
            if(activeMarch!=march||result==null)return;march.victory=result.victory;march.casualties=result.attacker_casualties;march.lootType="recursos";march.lootAmount=result.loot_wood+result.loot_food+result.loot_coal;
            battleReportBody="Poder atacante: "+result.attacker_power+" · Defensa: "+result.defender_power+"\nBajas: "+result.attacker_casualties+"\nSaqueo: "+result.loot_wood+" madera, "+result.loot_food+" comida, "+result.loot_coal+" carbón\nSalud ciudad: "+result.city_health+(result.relocated?" · REUBICADA":"");prototype.ApplyCityAttackOutcome(result.attacker_casualties,result.loot_wood,result.loot_food,result.loot_coal);
            float d=Vector2.Distance(new Vector2(march.originX,march.originY),new Vector2(march.targetX,march.targetY));march.status="Return";march.phaseStartedTicks=DateTime.UtcNow.Ticks;march.phaseEndsTicks=DateTime.UtcNow.AddSeconds(Mathf.Max(2f,d*MarchSecondsPerTile*(1f-march.heroSpeedBonus)*ResearchManager.MarchDurationMultiplier)).Ticks;SaveActiveMarch();loadedChunk=new Vector2Int(int.MinValue,int.MinValue);
        }

        private void CancelFailedCityAttack(LocalMarch march, string error)
        {
            Debug.LogWarning(error);
            if (activeMarch != march) return;
            actionMessage = error.Contains("PEACE_SHIELD_ACTIVE")
                ? "ATAQUE CANCELADO: ESCUDO ACTIVO"
                : error.Contains("PVP_REQUIRES_CONNECTION") ? "PVP REQUIERE CONEXIÓN" : "ATAQUE PVP CANCELADO";
            actionMessageUntil = Time.unscaledTime + 4f;
            activeMarch = null;
            PlayerPrefs.DeleteKey("frostbound-active-march");
            ReleaseMarchVisual();
            loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
            RefreshChunkIfNeeded();
        }

        private void FinishBattleMarch(LocalMarch march)
        {
            if (activeMarch != march) return;
            battleReportTitle = march.victory ? "VICTORIA" : "DERROTA";
            if(march.marchKind!="CityAttack")battleReportBody = "Tropas enviadas: " + march.troopCount + "\nBajas: " + march.casualties +
                "   Heridos: " + march.wounded + "\nBotín: " + (march.lootAmount > 0 ? march.lootAmount + " " + RewardLabel(march.lootType) : "Ninguno");
            battleReportOpen = true;
            QuestMailManager.Instance?.AddBattleReport("beast_" + march.id, "Combate PVE: " + battleReportTitle, battleReportBody);
            march.status = "Completed";
            SaveActiveMarch();
            activeMarch = null;
            PlayerPrefs.DeleteKey("frostbound-active-march");
            ReleaseMarchVisual();
            loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
            RefreshChunkIfNeeded();
        }

        private void FinishMarchLocally(LocalMarch march, int delivered)
        {
            if (activeMarch != march) return;
            prototype.AddGatheredResources(march.resourceType, delivered);
            march.status = "Completed";
            SaveActiveMarch();
            activeMarch = null;
            PlayerPrefs.DeleteKey("frostbound-active-march");
            ReleaseMarchVisual();
        }

        private void EnsureMarchVisual()
        {
            if (marchVisual != null || mapRoot == null || marchPool == null) return;
            marchVisual = marchPool.Get(mapRoot.transform);
        }

        private GameObject CreatePooledMarch()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "World March";
            visual.transform.localScale = new Vector3(0.22f, 0.25f, 0.22f);
            visual.GetComponent<Renderer>().sharedMaterial = cityMaterial;
            Destroy(visual.GetComponent<Collider>());
            return visual;
        }

        private void ReleaseMarchVisual()
        {
            if (marchVisual == null) return;
            if (marchPool != null) marchPool.Release(marchVisual);
            else Destroy(marchVisual);
            marchVisual = null;
        }

        private void SaveActiveMarch(Action onSaved=null)
        {
            if (activeMarch == null) return;
            PlayerPrefs.SetString("frostbound-active-march", JsonUtility.ToJson(activeMarch));
            PlayerPrefs.Save();
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud == null || !cloud.CanQueryWorld) return;
            SupabaseSyncClient.MarchCloudState payload = new SupabaseSyncClient.MarchCloudState
            {
                id = activeMarch.id, origin_x = activeMarch.originX, origin_y = activeMarch.originY,
                target_x = activeMarch.targetX, target_y = activeMarch.targetY,
                march_type = activeMarch.status == "Return" || activeMarch.status == "Completed" ? "Return" : activeMarch.marchKind == "CityAttack" ? "Attack" : activeMarch.marchKind,
                res_type = activeMarch.resourceType, payload_amount = activeMarch.payloadAmount,
                troop_count = activeMarch.troopCount,
                hero_id = activeMarch.heroId, hero_key = activeMarch.heroKey,
                hero_power_bonus = activeMarch.heroPowerBonus, hero_speed_bonus = activeMarch.heroSpeedBonus,
                departure_time = new DateTime(activeMarch.phaseStartedTicks, DateTimeKind.Utc).ToString("O"),
                arrival_time = new DateTime(activeMarch.phaseEndsTicks, DateTimeKind.Utc).ToString("O"), status = activeMarch.status
            };
            StartCoroutine(cloud.SaveMarch(payload, onSaved, error => Debug.LogWarning(error)));
        }

        private void LoadLocalMarch()
        {
            string json = PlayerPrefs.GetString("frostbound-active-march", string.Empty);
            if (!string.IsNullOrWhiteSpace(json)) activeMarch = JsonUtility.FromJson<LocalMarch>(json);
            if (activeMarch != null && activeMarch.phaseEndsTicks > DateTime.UtcNow.AddDays(1).Ticks)
            {
                activeMarch = null;
                PlayerPrefs.DeleteKey("frostbound-active-march");
            }
        }

        private void SelectAtScreenPoint(Vector2 point)
        {
            Ray ray = mapCamera.ScreenPointToRay(point);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            int x = Mathf.Clamp(Mathf.RoundToInt(hit.x / TileSize) + MapCenter, 0, MapSize - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(hit.z / TileSize) + MapCenter, 0, MapSize - 1);
            if (relocationMode)
            {
                SetRelocationCandidate(x, y);
                return;
            }
            if (alliancePlacementMode)
            {
                SetAlliancePlacementCandidate(x, y);
                return;
            }
            selectedCoordinate = new Vector2Int(x, y);
            ShowSelectionHighlight(x, y);
        }

        private void BeginRelocation()
        {
            Vector2Int target = selectedCoordinate;
            ClearSelection();
            relocationMode = true;
            relocationConfirmationOpen = false;
            SetRelocationCandidate(target.x, target.y);
            actionMessage = "TOCA UNA TILE PARA MOVER LA VISTA PREVIA DE TU BASE";
            actionMessageUntil = Time.unscaledTime + 4f;
        }

        private void SetAlliancePlacementCandidate(int x, int y)
        {
            alliancePlacementCoordinate = new Vector2Int(x, y);
            alliancePlacementConfirmationOpen = false;
            ShowSelectionHighlight(x, y);
        }

        private bool IsAlliancePlacementValid()
        {
            return visibleTiles.TryGetValue(alliancePlacementCoordinate, out TileVisual visual) &&
                visual.data != null && visual.data.tile_type == "Empty";
        }

        private void CancelAlliancePlacement()
        {
            alliancePlacementMode = false;
            alliancePlacementConfirmationOpen = false;
            alliancePlacementPending = false;
            alliancePlacementCoordinate = new Vector2Int(-1, -1);
            ClearSelection();
            actionMessage = "DESPLIEGUE DE HQ CANCELADO";
            actionMessageUntil = Time.unscaledTime + 2f;
        }

        private void ConfirmAlliancePlacement()
        {
            if (alliancePlacementPending || !IsAlliancePlacementValid()) return;
            alliancePlacementPending = true;
            Vector2Int target = alliancePlacementCoordinate;
            string structureType = alliancePlacementType;
            StartCoroutine(SupabaseSyncClient.Instance.PlaceAllianceStructure(structureType, target.x, target.y, row =>
            {
                alliancePlacementPending = false;
                alliancePlacementMode = false;
                alliancePlacementConfirmationOpen = false;
                ClearSelection();
                actionMessage = row.structure_type + " DE ALIANZA ACTIVO · TERRITORIO RADIO " + row.territory_radius;
                actionMessageUntil = Time.unscaledTime + 5f;
                loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
                RefreshChunkIfNeeded();
            }, error =>
            {
                alliancePlacementPending = false;
                alliancePlacementConfirmationOpen = false;
                actionMessage = error;
                actionMessageUntil = Time.unscaledTime + 5f;
            }));
        }

        private void SetRelocationCandidate(int x, int y)
        {
            relocationCoordinate = new Vector2Int(x, y);
            relocationConfirmationOpen = false;
            ShowSelectionHighlight(x, y);
            EnsureRelocationPreview();
            relocationPreview.transform.position = CoordinateToWorld(x, y) + Vector3.up * 0.16f;
            relocationPreview.SetActive(true);
            SetPreviewMaterial(IsRelocationCandidateValid() ? validRelocationMaterial : invalidRelocationMaterial);
        }

        private bool IsRelocationCandidateValid()
        {
            return visibleTiles.TryGetValue(relocationCoordinate, out TileVisual visual) &&
                (visual.data == null || visual.data.tile_type == "Empty" ||
                 (visual.data.tile_type == "PlayerCity" && visual.data.occupant_id == SupabaseSyncClient.Instance?.CurrentUserId));
        }

        private void EnsureRelocationPreview()
        {
            if (relocationPreview != null) return;
            relocationPreview = new GameObject("Relocation Base Preview");
            relocationPreview.transform.SetParent(mapRoot.transform, false);
            CreatePreviewPart(PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.62f, 0.18f, 0.62f));
            CreatePreviewPart(PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(0.48f, 0.48f, 0.48f));
            CreatePreviewPart(PrimitiveType.Cylinder, new Vector3(-0.43f, 0.36f, 0.28f), new Vector3(0.2f, 0.32f, 0.2f));
            CreatePreviewPart(PrimitiveType.Cylinder, new Vector3(0.43f, 0.36f, 0.28f), new Vector3(0.2f, 0.32f, 0.2f));
            relocationPreview.SetActive(false);
        }

        private void CreatePreviewPart(PrimitiveType primitive, Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.transform.SetParent(relocationPreview.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Destroy(part.GetComponent<Collider>());
        }

        private void SetPreviewMaterial(Material material)
        {
            foreach (Renderer renderer in relocationPreview.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }

        private void CancelRelocation()
        {
            relocationMode = false;
            relocationConfirmationOpen = false;
            relocationRequestPending = false;
            relocationCoordinate = new Vector2Int(-1, -1);
            if (relocationPreview != null) relocationPreview.SetActive(false);
            ClearSelection();
            actionMessage = "REUBICACIÓN CANCELADA";
            actionMessageUntil = Time.unscaledTime + 2f;
        }

        private void ConfirmRelocation()
        {
            if (relocationRequestPending || !IsRelocationCandidateValid()) return;
            relocationRequestPending = true;
            Vector2Int destination = relocationCoordinate;
            ApplyRelocationLocally(destination);
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud != null && cloud.CanQueryWorld)
                StartCoroutine(cloud.RelocateWorldCity(destination.x, destination.y, RelocationSucceeded, RelocationFailed));
            else
                RelocationSucceeded();
        }

        private void ApplyRelocationLocally(Vector2Int destination)
        {
            string currentUser = SupabaseSyncClient.Instance?.CurrentUserId;
            foreach (TileVisual visual in visibleTiles.Values)
            {
                if (visual.data?.tile_type == "PlayerCity" && visual.data.occupant_id == currentUser)
                {
                    visual.data.tile_type = "Empty";
                    visual.data.occupant_id = null;
                    UpdateMarker(visual);
                }
            }
            if (visibleTiles.TryGetValue(destination, out TileVisual target))
            {
                target.data.tile_type = "PlayerCity";
                target.data.occupant_id = currentUser;
                target.data.level = Mathf.Max(1, target.data.level);
                UpdateMarker(target);
            }
            PlayerPrefs.SetInt("frostbound-world-city-x", destination.x);
            PlayerPrefs.SetInt("frostbound-world-city-y", destination.y);
            PlayerPrefs.Save();
        }

        private void RelocationSucceeded()
        {
            Vector2Int destination = relocationCoordinate;
            relocationMode = false;
            relocationConfirmationOpen = false;
            relocationRequestPending = false;
            if (relocationPreview != null) relocationPreview.SetActive(false);
            ClearSelection();
            actionMessage = "BASE REUBICADA EN X:" + destination.x + "  Y:" + destination.y;
            actionMessageUntil = Time.unscaledTime + 4f;
            loadedChunk = new Vector2Int(int.MinValue, int.MinValue);
            RefreshChunkIfNeeded();
        }

        private void RelocationFailed(string error)
        {
            relocationRequestPending = false;
            relocationConfirmationOpen = false;
            actionMessage = "NO SE PUDO REUBICAR · " + error;
            actionMessageUntil = Time.unscaledTime + 4f;
        }

        private void ShowSelectionHighlight(int x, int y)
        {
            if (selectionHighlight == null)
            {
                selectionHighlight = new GameObject("Selected Tile Glow");
                selectionHighlight.transform.SetParent(mapRoot.transform, false);
                float half = TileSize * 0.5f;
                CreateHighlightBar(new Vector3(0f, 0.13f, half), new Vector3(TileSize * 1.05f, 0.07f, 0.08f));
                CreateHighlightBar(new Vector3(0f, 0.13f, -half), new Vector3(TileSize * 1.05f, 0.07f, 0.08f));
                CreateHighlightBar(new Vector3(half, 0.13f, 0f), new Vector3(0.08f, 0.07f, TileSize * 1.05f));
                CreateHighlightBar(new Vector3(-half, 0.13f, 0f), new Vector3(0.08f, 0.07f, TileSize * 1.05f));
                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glow.name = "Selection Core";
                glow.transform.SetParent(selectionHighlight.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                glow.transform.localScale = new Vector3(TileSize * 0.9f, 0.025f, TileSize * 0.9f);
                glow.GetComponent<Renderer>().sharedMaterial = selectionMaterial;
                Destroy(glow.GetComponent<Collider>());
            }
            selectionHighlight.transform.position = CoordinateToWorld(x, y);
            selectionHighlight.SetActive(true);
        }

        private void CreateHighlightBar(Vector3 localPosition, Vector3 scale)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Cyan Border";
            bar.transform.SetParent(selectionHighlight.transform, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = scale;
            bar.GetComponent<Renderer>().sharedMaterial = selectionMaterial;
            Destroy(bar.GetComponent<Collider>());
        }

        private void ClearSelection()
        {
            selectedCoordinate = new Vector2Int(-1, -1);
            if (selectionHighlight != null) selectionHighlight.SetActive(false);
            pointerTracking = false;
        }

        private Vector2Int CameraCoordinate()
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(mapCamera.transform.position.x / TileSize) + MapCenter, 0, MapSize - 1),
                Mathf.Clamp(Mathf.RoundToInt(mapCamera.transform.position.z / TileSize) + MapCenter, 0, MapSize - 1));
        }

        private static Vector3 CoordinateToWorld(int x, int y) => new Vector3((x - MapCenter) * TileSize, 0f, (y - MapCenter) * TileSize);

        private void OnGUI()
        {
            IsSelectionPanelOpen = IsWorldMapActive && selectedCoordinate.x >= 0 && !IsMaximumZoomOut && !cinematicZoomActive && !relocationMode && !alliancePlacementMode;
            GUI.depth = 100;
            EnsureStyles();
            if (AllianceManager.IsPanelOpen || ResearchManager.IsPanelOpen || QuestMailManager.IsPanelOpen || InventoryShopManager.IsPanelOpen || Hito16Manager.IsPanelOpen) return;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(safe.width / 1280f, 0.75f, 1.35f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0f), Quaternion.identity, Vector3.one * scale);
            float width = safe.width / scale;
            float height = safe.height / scale;

            if (!IsWorldMapActive)
            {
                if (GUI.Button(new Rect(width - 250f, height - 220f, 210f, 50f), "MAPA MUNDIAL  ›", buttonStyle)) ToggleWorldMap();
                GUI.matrix = previousMatrix;
                return;
            }

            GUI.Box(new Rect(18f, 16f, width - 36f, 68f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(38f, 26f, 265f, 42f), "MUNDO · 1200 × 1200", headerStyle);
            Vector2Int center = CameraCoordinate();
            GUI.Label(new Rect(width * 0.5f - 110f, 28f, 220f, 38f), "CENTRO  X " + center.x + " · Y " + center.y, coordinateStyle);
            GUI.Label(new Rect(width - 330f, 28f, 290f, 38f), mapStatus, coordinateStyle);
            GUI.Label(new Rect(width * .5f - 105f, 62f, 210f, 24f), HighDetailActive ? "LOD ALTO · MODELOS 3D" : "LOD BAJO · VISTA ESTRATÉGICA", bodyStyle);

            GUI.Box(new Rect(18f, height - 82f, width - 36f, 64f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(38f, height - 72f, 620f, 26f), "ORIGEN (0,0)  ·  X → DERECHA  ·  Y ↑ ARRIBA  ·  FINAL (1199,1199)", coordinateStyle);
            GUI.Label(new Rect(38f, height - 47f, 600f, 24f), "ARRASTRAR: MOVER  ·  PINZA/RUEDA: ZOOM  ·  TOCAR: INSPECCIONAR", bodyStyle);
            GUI.Label(new Rect(width - 430f, height - 68f, 78f, 34f), "ZOOM", coordinateStyle);
            if (GUI.Button(new Rect(width - 350f, height - 71f, 42f, 42f), "−", buttonStyle))
                mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize + ZoomButtonStep, WorldMinZoom, WorldMaxZoom);
            if (GUI.Button(new Rect(width - 302f, height - 71f, 42f, 42f), "+", buttonStyle))
                mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize - ZoomButtonStep, WorldMinZoom, WorldMaxZoom);
            if (GUI.Button(new Rect(width - 250f, height - 71f, 210f, 42f), "‹  ASENTAMIENTO", buttonStyle)) ToggleWorldMap();
            if (!relocationMode && !IsMaximumZoomOut && !cinematicZoomActive &&
                GUI.Button(new Rect(width - 600f, height - 71f, 158f, 42f), "VISTA GLOBAL", buttonStyle))
                StartGlobalView();

            if (relocationMode) DrawRelocationControls(width, height);
            else if (alliancePlacementMode) DrawAlliancePlacementControls(width, height);
            else if (selectedCoordinate.x >= 0 && !IsMaximumZoomOut && !cinematicZoomActive) DrawSelectionPanel(width, height);
            if (battleReportOpen) DrawBattleReport(width, height);
            if (IsMaximumZoomOut && !relocationMode && !alliancePlacementMode) DrawMaximumZoomMarkers(scale, width, height);
            if (Time.unscaledTime < actionMessageUntil)
                GUI.Label(new Rect(width * 0.5f - 220f, 96f, 440f, 42f), actionMessage, coordinateStyle);

            GUI.matrix = previousMatrix;
        }

        private void DrawMaximumZoomMarkers(float scale, float width, float height)
        {
            Vector2Int city = GetCityCoordinate();
            DrawWorldPin(city, cityPinStyle, scale, width, height, false);
            if (selectedCoordinate.x >= 0 && selectedCoordinate != city)
                DrawWorldPin(selectedCoordinate, searchPinStyle, scale, width, height, true);
        }

        private void DrawWorldPin(Vector2Int coordinate, GUIStyle style, float scale, float width, float height, bool interactive)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(CoordinateToWorld(coordinate.x, coordinate.y));
            if (screen.z <= 0f) return;
            Vector2 guiPoint = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            guiPoint.x = Mathf.Clamp(guiPoint.x, 48f, width - 48f);
            guiPoint.y = Mathf.Clamp(guiPoint.y, 96f, height - 104f);
            Rect pin = new Rect(guiPoint.x - 30f, guiPoint.y - 52f, 60f, 64f);
            if (!interactive)
            {
                GUI.DrawTexture(pin, greenPin);
                string tag = AllianceManager.LocalTag;
                GUI.Label(new Rect(pin.x - 62f, pin.y + 48f, 184f, 24f),
                    string.IsNullOrEmpty(tag) ? "TU CIUDAD" : "[" + tag + "] TU CIUDAD", coordinateStyle);
                return;
            }

            Event guiEvent = Event.current;
            if (guiEvent.type == EventType.MouseDown && pin.Contains(guiEvent.mousePosition))
                selectionPanelPointerCaptured = true;
            GUI.DrawTexture(pin, orangePin);
            if (GUI.Button(pin, GUIContent.none, style)) StartCinematicZoom(coordinate);
            GUI.Label(new Rect(pin.x - 52f, pin.y + 50f, 164f, 24f),
                "X:" + coordinate.x + " Y:" + coordinate.y, coordinateStyle);
        }

        private void DrawRelocationControls(float width, float height)
        {
            bool valid = IsRelocationCandidateValid();
            Rect controls = new Rect(width * 0.5f - 310f, height - 166f, 620f, 94f);
            Event guiEvent = Event.current;
            if (guiEvent.type == EventType.MouseDown && controls.Contains(guiEvent.mousePosition))
                selectionPanelPointerCaptured = true;
            GUI.Box(new Rect(controls.x, controls.y, controls.width, 72f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(width * 0.5f - 105f, height - 157f, 210f, 28f),
                "DESTINO  X:" + relocationCoordinate.x + "  Y:" + relocationCoordinate.y, coordinateStyle);
            GUIStyle cancelStyle = new GUIStyle(buttonStyle) { normal = { background = orangeButton } };
            if (GUI.Button(new Rect(width * 0.5f - 292f, height - 126f, 220f, 54f), "CANCELAR", cancelStyle))
                CancelRelocation();
            GUI.enabled = valid && !relocationRequestPending;
            if (GUI.Button(new Rect(width * 0.5f + 72f, height - 126f, 220f, 54f), valid ? "CONFIRMAR DESTINO" : "TILE OCUPADA", buttonStyle))
                relocationConfirmationOpen = true;
            GUI.enabled = true;

            if (!relocationConfirmationOpen) return;
            Rect modal = new Rect(width * 0.5f - 235f, height * 0.5f - 105f, 470f, 210f);
            if (guiEvent.type == EventType.MouseDown && modal.Contains(guiEvent.mousePosition))
                selectionPanelPointerCaptured = true;
            GUI.DrawTexture(modal, cardPanel);
            GUI.Label(new Rect(modal.x + 32f, modal.y + 24f, modal.width - 64f, 42f), "¿CONFIRMAR REUBICACIÓN?", cardTitleStyle);
            GUI.Label(new Rect(modal.x + 34f, modal.y + 72f, modal.width - 68f, 42f),
                "Tu base se moverá a X:" + relocationCoordinate.x + "  Y:" + relocationCoordinate.y, cardBodyStyle);
            if (GUI.Button(new Rect(modal.x + 28f, modal.y + 138f, 190f, 48f), "VOLVER", cancelStyle))
                relocationConfirmationOpen = false;
            if (GUI.Button(new Rect(modal.x + 252f, modal.y + 138f, 190f, 48f), relocationRequestPending ? "MOVIENDO..." : "MOVER BASE", buttonStyle))
                ConfirmRelocation();
        }

        private void DrawAlliancePlacementControls(float width, float height)
        {
            bool valid = IsAlliancePlacementValid();
            Rect controls = new Rect(width * .5f - 310f, height - 166f, 620f, 94f);
            Event guiEvent = Event.current;
            if (guiEvent.type == EventType.MouseDown && controls.Contains(guiEvent.mousePosition)) selectionPanelPointerCaptured = true;
            GUI.Box(new Rect(controls.x, controls.y, controls.width, 72f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(width * .5f - 150f, height - 157f, 300f, 28f),
                alliancePlacementType.ToUpperInvariant() + "  X:" + alliancePlacementCoordinate.x + "  Y:" + alliancePlacementCoordinate.y, coordinateStyle);
            GUIStyle cancelStyle = new GUIStyle(buttonStyle) { normal = { background = orangeButton } };
            if (GUI.Button(new Rect(width * .5f - 292f, height - 126f, 220f, 54f), "CANCELAR", cancelStyle)) CancelAlliancePlacement();
            GUI.enabled = valid && !alliancePlacementPending;
            if (GUI.Button(new Rect(width * .5f + 72f, height - 126f, 220f, 54f), valid ? "CONSTRUIR " + alliancePlacementType.ToUpperInvariant() : "TILE OCUPADA", buttonStyle))
                alliancePlacementConfirmationOpen = true;
            GUI.enabled = true;
            if (!alliancePlacementConfirmationOpen) return;
            Rect modal = new Rect(width * .5f - 235f, height * .5f - 105f, 470f, 210f);
            if (guiEvent.type == EventType.MouseDown && modal.Contains(guiEvent.mousePosition)) selectionPanelPointerCaptured = true;
            GUI.DrawTexture(modal, cardPanel);
            GUI.Label(new Rect(modal.x + 32f, modal.y + 24f, modal.width - 64f, 42f), "¿DESPLEGAR " + alliancePlacementType.ToUpperInvariant() + " DE ALIANZA?", cardTitleStyle);
            GUI.Label(new Rect(modal.x + 34f, modal.y + 72f, modal.width - 68f, 48f),
                (alliancePlacementType == "Flag" ? "Debe conectarse al territorio actual. Expandirá radio 3" : "Se reclamará un territorio cuadrado de radio 5") + " alrededor de X:" + alliancePlacementCoordinate.x + " Y:" + alliancePlacementCoordinate.y, cardBodyStyle);
            if (GUI.Button(new Rect(modal.x + 28f, modal.y + 138f, 190f, 48f), "VOLVER", cancelStyle)) alliancePlacementConfirmationOpen = false;
            if (GUI.Button(new Rect(modal.x + 252f, modal.y + 138f, 190f, 48f), alliancePlacementPending ? "CONSTRUYENDO..." : "CONFIRMAR", buttonStyle)) ConfirmAlliancePlacement();
        }

        private void DrawSelectionPanel(float width, float height)
        {
            visibleTiles.TryGetValue(selectedCoordinate, out TileVisual visual);
            string type = visual?.data?.tile_type ?? "Empty";
            int level = visual?.data?.level ?? 1;
            string occupant = string.IsNullOrWhiteSpace(visual?.data?.occupant_id) ? "Ninguno" :
                visual.data.occupant_id == SupabaseSyncClient.Instance?.CurrentUserId ? "Tu colonia" : "Otro superviviente";

            bool rallyTarget = type == "Fortress" || (type == "Beast" && level >= 2);
            float panelHeight = type == "ResourceNode" || type == "Beast" || type == "PlayerCity" ? 452f : 344f;
            Rect panel = new Rect(width * 0.5f - 245f, 98f, 490f, panelHeight);
            Event guiEvent = Event.current;
            bool pointerInsidePanel = panel.Contains(guiEvent.mousePosition);
            if (guiEvent.type == EventType.MouseDown && pointerInsidePanel)
                selectionPanelPointerCaptured = true;

            GUI.DrawTexture(panel, cardPanel);
            GUI.DrawTexture(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 116f), cardPreview);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 88f, 210f, 36f), "X:" + selectedCoordinate.x + "  Y:" + selectedCoordinate.y, cardCoordinateStyle);
            string previewTitle = type == "Beast" ? BeastName(visual.data.beast_kind, level) :
                type == "Fortress" ? FacilityName(visual.data.facility_key) : PreviewSymbol(type, visual?.data?.res_type);
            GUI.Label(new Rect(panel.x + 82f, panel.y + 36f, 326f, 44f), previewTitle, cardTitleStyle);
            if (GUI.Button(new Rect(panel.x + panel.width - 54f, panel.y + 10f, 38f, 38f), "×", buttonStyle)) ClearSelection();

            GUI.Label(new Rect(panel.x + 30f, panel.y + 140f, panel.width - 60f, 40f), TileLabel(type), cardTitleStyle);
            GUI.DrawTexture(new Rect(panel.x + 24f, panel.y + 184f, panel.width - 48f, 62f), cardInfo);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 190f, 410f, 52f), type == "ResourceNode" ? "CAPACIDAD  " + Mathf.Max(0, visual.data.res_remaining) + " / " + Mathf.Max(0, visual.data.res_capacity) + "  ·  NIVEL " + level
                : type == "Beast" ? "NIVEL " + level + "  ·  PODER RECOMENDADO " + visual.data.beast_power + "\nRECOMPENSA  " + visual.data.reward_amount + " " + RewardLabel(visual.data.reward_type)
                : type == "Fortress" ? "GUARNICIÓN  " + visual.data.facility_power + " PODER  ·  NIVEL " + level + "\n" +
                    (string.IsNullOrEmpty(visual.data.owner_alliance_id) ? "NEUTRAL" : "CONQUISTADA") + "  ·  BUFF +" + visual.data.buff_percent + "% " +
                    (visual.data.buff_type == "AllianceAttack" ? "ATAQUE" : "PRODUCCIÓN")
                : "OCUPADO POR  " + occupant + "  ·  NIVEL " + level + "\nSALUD " + visual.data.city_health + "%  ·  " + (IsFuture(visual.data.peace_shield_until) ? "ESCUDO ACTIVO" : IsFuture(visual.data.burning_until) ? "EN LLAMAS" : "SIN PROTECCIÓN"), cardBodyStyle);

            float actionY = panel.y + 266f;
            if (type == "ResourceNode" || type == "Beast" || type == "PlayerCity")
            {
                int available = Mathf.Max(0, prototype.SnowInfantry);
                troopsToSend = Mathf.Clamp(troopsToSend, 1, Mathf.Max(1, available));
                int combatPower = Mathf.CeilToInt(troopsToSend * 20f * (assignElena && prototype.ElenaUnlocked ? 1.15f : 1f) * ResearchManager.CombatPowerMultiplier * AllianceManager.AttackMultiplier);
                string stat = type == "ResourceNode" ? "CARGA " + (troopsToSend * LoadPerSnowInfantry) : "PODER " + combatPower;
                GUI.Label(new Rect(panel.x + 28f, panel.y + 252f, 295f, 42f), "TROPAS " + troopsToSend + "  ·  " + stat, cardBodyStyle);
                if (GUI.Button(new Rect(panel.x + 340f, panel.y + 250f, 48f, 42f), "−", buttonStyle)) troopsToSend = Mathf.Max(1, troopsToSend - 1);
                if (GUI.Button(new Rect(panel.x + 398f, panel.y + 250f, 48f, 42f), "+", buttonStyle)) troopsToSend = Mathf.Min(Mathf.Max(1, available), troopsToSend + 1);
                string heroLabel = assignElena ? "ELENA · +15% PODER · +20% VELOCIDAD" : "SIN HÉROE";
                if (GUI.Button(new Rect(panel.x + 28f, panel.y + 298f, 418f, 38f), heroLabel, buttonStyle))
                    assignElena = !assignElena && prototype.ElenaUnlocked;
                actionY = panel.y + 374f;
            }
            string action = type == "Empty" ? "OCUPAR" : type == "ResourceNode" ? "RECOLECTAR" : type == "Fortress" ? "SOLO CON RALLY" : "ATACAR";
            string leftAction = rallyTarget && AllianceManager.HasAlliance ? "RALLY · 5 MIN" : "REUBICAR";
            if (GUI.Button(new Rect(panel.x + 24f, actionY, 210f, 56f), leftAction, buttonStyle))
            {
                if (rallyTarget && AllianceManager.HasAlliance)
                {
                    AllianceManager.Instance.CreateRallyAt(selectedCoordinate.x, selectedCoordinate.y,
                        type == "Beast" ? "EliteBeast" : "Facility", troopsToSend);
                    ClearSelection();
                }
                else
                    BeginRelocation();
            }
            GUIStyle actionStyle = action == "ATACAR" ? new GUIStyle(buttonStyle) { normal = { background = orangeButton } } : buttonStyle;
            GUI.enabled = type != "Fortress";
            if (GUI.Button(new Rect(panel.x + 256f, actionY, 210f, 56f), action, actionStyle))
            {
                if (type == "ResourceNode") StartGatheringMarch(visual);
                else if (type == "Beast") StartBeastMarch(visual);
                else if (type == "PlayerCity") StartCityMarch(visual);
                else
                {
                    actionMessage = action + " · sector " + selectedCoordinate.x + "," + selectedCoordinate.y + " preparado";
                    actionMessageUntil = Time.unscaledTime + 3f;
                }
            }
            GUI.enabled = true;
        }

        private void DrawBattleReport(float width, float height)
        {
            Rect panel = new Rect(width * 0.5f - 245f, height * 0.5f - 155f, 490f, 310f);
            GUI.DrawTexture(panel, cardPanel);
            GUI.DrawTexture(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 78f), battleReportTitle == "VICTORIA" ? blueButton : orangeButton);
            GUI.Label(new Rect(panel.x + 40f, panel.y + 34f, panel.width - 80f, 46f), "INFORME DE BATALLA · " + battleReportTitle, cardTitleStyle);
            GUI.Label(new Rect(panel.x + 45f, panel.y + 118f, panel.width - 90f, 110f), battleReportBody, cardBodyStyle);
            if (GUI.Button(new Rect(panel.x + 120f, panel.y + 244f, 250f, 48f), "CERRAR INFORME", buttonStyle)) battleReportOpen = false;
        }

        private static string PreviewSymbol(string type, string resourceType)
        {
            if (type == "PlayerCity") return "CIUDAD TERMAL";
            if (type == "ResourceNode") return "NODO DE " + ResourceLabel(resourceType);
            if (type == "Beast") return "BESTIA DE NIEVE";
            if (type == "Fortress") return "FORTALEZA";
            return "CAMPOS DE HIELO";
        }

        private static string FacilityName(string key)
        {
            if (key == "RegionalThermalStation") return "ESTACIÓN TÉRMICA REGIONAL";
            if (key == "GlacialHuntingPost") return "PUESTO DE CAZA GLACIAL";
            return "INSTALACIÓN NEUTRAL";
        }

        private static string TileLabel(string type)
        {
            if (type == "PlayerCity") return "CIUDAD";
            if (type == "ResourceNode") return "NODO DE RECOLECCIÓN";
            if (type == "Beast") return "BESTIA";
            if (type == "Fortress") return "INSTALACIÓN / BASTIÓN";
            return "TERRENO VACÍO";
        }

        private static string ResourceLabel(string resourceType)
        {
            if (resourceType == "Wood") return "MADERA";
            if (resourceType == "Food") return "COMIDA";
            if (resourceType == "Coal") return "CARBÓN";
            return "RECURSOS";
        }

        private static string RewardLabel(string rewardType)
        {
            if (rewardType == "Coal") return "CARBÓN";
            if (rewardType == "Crystals") return "CRISTALES";
            if (rewardType == "Speedups") return "ACELERADORES";
            return "SIN BOTÍN";
        }

        private static string BeastName(string kind, int level)
        {
            return (kind == "GlacialBear" ? "OSO POLAR GLACIAL" : "LOBO DE NIEBLA") + " NV. " + level;
        }

        private void CreateMaterials()
        {
            snowA = NewMaterial(new Color(0.68f, 0.79f, 0.84f));
            snowB = NewMaterial(new Color(0.58f, 0.72f, 0.79f));
            gridMaterial = NewMaterial(new Color(0.12f, 0.25f, 0.31f));
            cityMaterial = NewMaterial(new Color(0.2f, 0.72f, 1f));
            resourceMaterial = NewMaterial(new Color(0.18f, 0.82f, 0.48f));
            beastMaterial = NewMaterial(new Color(0.92f, 0.27f, 0.19f));
            fortressMaterial = NewMaterial(new Color(1f, 0.58f, 0.12f));
            selectionMaterial = NewMaterial(new Color(0.05f, 0.95f, 1f));
            selectionMaterial.EnableKeyword("_EMISSION");
            selectionMaterial.SetColor("_EmissionColor", new Color(0.05f, 1.5f, 2f));
            validRelocationMaterial = NewMaterial(new Color(0.16f, 0.92f, 0.42f));
            invalidRelocationMaterial = NewMaterial(new Color(1f, 0.2f, 0.12f));
            allianceTerritoryMaterial = NewMaterial(new Color(0.08f, 0.88f, 1f));
        }

        private void CreateWorldBackdrop()
        {
            worldBackdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldBackdrop.name = "World Map Square Backdrop 1200x1200";
            worldBackdrop.transform.SetParent(mapRoot.transform, false);
            worldBackdrop.transform.position = new Vector3(-TileSize * 0.5f, -0.12f, -TileSize * 0.5f);
            worldBackdrop.transform.localScale = new Vector3(MapSize * TileSize, 0.08f, MapSize * TileSize);
            Renderer renderer = worldBackdrop.GetComponent<Renderer>();
            renderer.sharedMaterial = gridMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(worldBackdrop.GetComponent<Collider>());
        }

        private static Material NewMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        private void EnsureStyles()
        {
            if (darkPanel != null && cityPinStyle != null && searchPinStyle != null) return;
            darkPanel = SolidTexture(new Color(0.025f, 0.075f, 0.105f, 0.96f));
            blueButton = SolidTexture(new Color(0.05f, 0.36f, 0.54f, 1f));
            orangeButton = SolidTexture(new Color(0.68f, 0.2f, 0.08f, 1f));
            cardPanel = SolidTexture(new Color(0.86f, 0.94f, 1f, 0.99f));
            cardPreview = SolidTexture(new Color(0.03f, 0.48f, 0.7f, 1f));
            cardInfo = SolidTexture(new Color(0.67f, 0.79f, 0.91f, 1f));
            greenPin = CreatePinTexture(new Color(0.08f, 0.9f, 0.28f, 1f), false);
            orangePin = CreatePinTexture(new Color(1f, 0.48f, 0.06f, 1f), true);
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 8, 8), normal = { background = darkPanel, textColor = new Color(0.9f, 0.96f, 1f) } };
            coordinateStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.48f, 0.86f, 1f) } };
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.04f, 0.2f, 0.34f) } };
            cardBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.08f, 0.28f, 0.46f) } };
            cardCoordinateStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { background = blueButton, textColor = Color.white }, hover = { background = blueButton, textColor = Color.white }, active = { background = orangeButton, textColor = Color.white } };
            FrostboundVisualTheme.ApplyPanel(bodyStyle);
            FrostboundVisualTheme.ApplyButton(buttonStyle);
            cityPinStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            Texture2D transparent = SolidTexture(Color.clear);
            searchPinStyle = new GUIStyle(GUI.skin.button);
            searchPinStyle.normal.background = transparent;
            searchPinStyle.hover.background = transparent;
            searchPinStyle.active.background = transparent;
        }

        private static Texture2D CreatePinTexture(Color fill, bool magnifier)
        {
            const int width = 64;
            const int height = 80;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            Color outline = new Color(0.025f, 0.12f, 0.16f, 1f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = x - 32f;
                float dy = y - 51f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool inHead = distance <= 27f;
                float triangleHalf = Mathf.Lerp(2f, 18f, Mathf.Clamp01(y / 30f));
                bool inTail = y <= 32 && Mathf.Abs(dx) <= triangleHalf;
                Color pixel = Color.clear;
                if (inHead || inTail)
                {
                    bool edge = (inHead && distance >= 23.5f) ||
                        (inTail && (Mathf.Abs(dx) >= triangleHalf - 2.5f || y <= 3));
                    pixel = edge ? outline : fill;
                }

                if (magnifier)
                {
                    float lensDx = x - 28f;
                    float lensDy = y - 54f;
                    float lensDistance = Mathf.Sqrt(lensDx * lensDx + lensDy * lensDy);
                    if (lensDistance >= 7f && lensDistance <= 11f) pixel = Color.white;
                    float handleDistance = Mathf.Abs((y - 38f) - (x - 43f));
                    if (x >= 36 && x <= 48 && y >= 31 && y <= 45 && handleDistance <= 2.2f) pixel = Color.white;
                }
                else if ((x - 32f) * (x - 32f) + (y - 53f) * (y - 53f) <= 45f)
                {
                    pixel = Color.white;
                }
                pixels[y * width + x] = pixel;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
