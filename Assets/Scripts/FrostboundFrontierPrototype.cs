using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrostboundFrontier
{
    public sealed class FrostboundFrontierPrototype : MonoBehaviour
    {
        [Serializable]
        private sealed class SaveData
        {
            public int heat = 120;
            public int wood = 180;
            public int food = 140;
            public int coal = 50;
            public int generatorLevel = 1;
            public int sawmillLevel = 1;
            public int kitchenLevel = 1;
            public int barracksLevel = 1;
            public int snowInfantry = 20;
            public int crystals;
            public int speedups;
            public int woundedInfantry;
            public int healingAmount;
            public long healingStartedUtcTicks;
            public long healingEndsUtcTicks;
            public string elenaHeroId = "";
            public bool elenaUnlocked = true;
            public int trainingAmount;
            public long trainingStartedUtcTicks;
            public long trainingEndsUtcTicks;
            public int population = 6;
            public int sawmillWorkers = 1;
            public int kitchenWorkers = 1;
            public float temperature = 12f;
            public float populationHealth = 100f;
            public float populationHappiness = 100f;
            public string upgradingBuilding = "";
            public long upgradeStartedUtcTicks;
            public long upgradeEndsUtcTicks;
            public bool tutorialComplete;
            public bool tutorialRewardClaimed;
            public long lastSavedUtcTicks;
        }

        private sealed class Building
        {
            public string Id;
            public string DisplayName;
            public GameObject Root;
            public Vector3 BaseScale;
        }

        [Serializable]
        public sealed class PlayerCloudState
        {
            public string displayName = "SUPERVIVIENTE";
            public float temperature;
            public int population;
            public long wood;
            public long food;
            public long coal;
            public int generatorLevel;
            public float health;
            public float happiness;
            public long power;
            public long clientSavedAt;
            public int snowInfantry;
            public long crystals;
            public int speedups;
        }

        [Serializable]
        public sealed class BuildingCloudState
        {
            public string slotId;
            public string buildingType;
            public int level;
            public int assignedWorkers;
            public long upgradeStartedUtcTicks;
            public long finishesUtcTicks;
            public float posX;
            public float posZ;
        }

        private readonly List<Building> buildings = new List<Building>();
        private readonly List<Transform> workers = new List<Transform>();
        private SaveData state;
        private Camera worldCamera;
        private GameObject colonyRoot;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle resourceStyle;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D accentTexture;
        private Texture2D progressBackTexture;
        private Texture2D progressFillTexture;
        private Texture2D heroPortraitTexture;
        private bool stylesReady;
        private string selectedBuilding = "barracks";
        private string toast = "Mantén vivo el generador";
        private float toastUntil;
        private float productionAccumulator;
        private float woodProductionCarry;
        private float foodProductionCarry;
        private float autosaveAccumulator;
        private float dayPhase;
        private Vector2 lastPointer;
        private bool dragging;
        private bool heroesPanelOpen;
        private bool healingRequestPending;

        public Camera WorldCamera => worldCamera;
        public GameObject ColonyRoot => colonyRoot;
        public int SnowInfantry => state != null ? state.snowInfantry : 0;
        public int WoundedInfantry => state != null ? state.woundedInfantry : 0;
        public bool ElenaUnlocked => state == null || state.elenaUnlocked;
        public string ElenaHeroId => state != null ? state.elenaHeroId : string.Empty;
        public int Crystals => state != null ? state.crystals : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePrototypeExists()
        {
            if (FindAnyObjectByType<FrostboundFrontierPrototype>() == null)
            {
                new GameObject("Frostbound Frontier Prototype").AddComponent<FrostboundFrontierPrototype>();
            }
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Load();
            BuildWorld();
            ApplyOfflineProgress();
            CompleteUpgradeIfReady();
            CompleteTrainingIfReady();
            CompleteHealingIfReady();
            toastUntil = Time.unscaledTime + 5f;
        }

        private void Update()
        {
            productionAccumulator += Time.deltaTime;
            if (productionAccumulator >= 1f)
            {
                productionAccumulator -= 1f;
                SimulateSecond();
            }

            CompleteUpgradeIfReady();
            CompleteTrainingIfReady();
            CompleteHealingIfReady();
            autosaveAccumulator += Time.deltaTime;
            if (autosaveAccumulator >= 10f)
            {
                autosaveAccumulator = 0f;
                Save();
            }

            dayPhase += Time.deltaTime * 0.08f;
            if (!WorldMapManager.IsWorldMapActive)
            {
                AnimateWorkers();
                HandleCamera();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit() => Save();

        private void BuildWorld()
        {
            colonyRoot = new GameObject("Colony Visuals");
            RenderSettings.ambientLight = new Color(0.32f, 0.39f, 0.48f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.45f, 0.58f, 0.68f);
            RenderSettings.fogDensity = 0.012f;

            GameObject cameraObject = new GameObject("World Camera");
            worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.transform.SetPositionAndRotation(new Vector3(0f, 19f, -22f), Quaternion.Euler(35f, 0f, 0f));
            worldCamera.fieldOfView = 50f;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.37f, 0.5f, 0.62f);

            GameObject lightObject = new GameObject("Cold Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.84f, 0.91f, 1f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            lightObject.transform.SetParent(colonyRoot.transform, true);

            CreatePrimitive("Snow Ground", PrimitiveType.Cylinder, new Vector3(0f, -0.5f, 2f), new Vector3(13f, 0.5f, 10f), new Color(0.82f, 0.9f, 0.94f));
            CreateRoads();
            CreateBuilding("generator", "Generador térmico", new Vector3(0f, 1f, 2f), new Vector3(3.2f, 2.2f, 3.2f), new Color(0.2f, 0.29f, 0.34f));
            CreateBuilding("sawmill", "Aserradero", new Vector3(-6f, 0.8f, 3f), new Vector3(3.6f, 1.6f, 2.8f), new Color(0.38f, 0.23f, 0.14f));
            CreateBuilding("kitchen", "Cocina comunal", new Vector3(6f, 0.8f, 3f), new Vector3(3.6f, 1.6f, 2.8f), new Color(0.43f, 0.26f, 0.18f));
            CreateBuilding("shelter", "Refugio", new Vector3(0f, 0.7f, 8f), new Vector3(4.2f, 1.4f, 2.8f), new Color(0.24f, 0.34f, 0.42f));
            CreateBuilding("barracks", "Cuartel", new Vector3(-6f, 0.8f, 8f), new Vector3(3.8f, 1.7f, 2.8f), new Color(0.18f, 0.32f, 0.4f));
            CreateBuilding("hospital", "Enfermería", new Vector3(6f, 0.8f, 8f), new Vector3(3.8f, 1.7f, 2.8f), new Color(0.72f, 0.82f, 0.88f));
            CreateBuilding("research", "Laboratorio", new Vector3(0f, 0.9f, 12f), new Vector3(4.2f, 1.9f, 3f), new Color(0.12f, 0.48f, 0.62f));
            CreateTrees();
            CreateWorkers();
        }

        private void CreateRoads()
        {
            Color path = new Color(0.56f, 0.64f, 0.67f);
            CreatePrimitive("Main Path", PrimitiveType.Cube, new Vector3(0f, 0.03f, 3f), new Vector3(2f, 0.08f, 13f), path);
            CreatePrimitive("Cross Path", PrimitiveType.Cube, new Vector3(0f, 0.04f, 3f), new Vector3(14f, 0.08f, 1.5f), path);
        }

        private void CreateBuilding(string id, string displayName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject root = CreatePrimitive(displayName, PrimitiveType.Cube, position, scale, color);
            GameObject roof = CreatePrimitive(displayName + " Roof", PrimitiveType.Cylinder, position + Vector3.up * (scale.y * 0.62f), new Vector3(scale.x * 0.62f, 0.3f, scale.z * 0.62f), color * 0.72f);
            roof.transform.SetParent(root.transform, true);
            if (id == "generator")
            {
                GameObject fire = CreatePrimitive("Thermal Core", PrimitiveType.Sphere, position + Vector3.up * 1.1f, Vector3.one * 1.2f, new Color(1f, 0.38f, 0.08f));
                fire.transform.SetParent(root.transform, true);
                Light glow = fire.AddComponent<Light>();
                glow.color = new Color(1f, 0.3f, 0.06f);
                glow.range = 11f;
                glow.intensity = 3f;
            }
            buildings.Add(new Building { Id = id, DisplayName = displayName, Root = root, BaseScale = scale });
            int level = GetLevel(id);
            if (level > 1) root.transform.localScale *= 1f + (level - 1) * 0.08f;
        }

        private void CreateTrees()
        {
            System.Random random = new System.Random(17);
            for (int i = 0; i < 28; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float radius = 9f + (float)random.NextDouble() * 4f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0.7f, 2f + Mathf.Sin(angle) * radius * 0.72f);
                GameObject trunk = CreatePrimitive("Frozen Pine", PrimitiveType.Cylinder, p, new Vector3(0.25f, 1.4f, 0.25f), new Color(0.24f, 0.18f, 0.13f));
                GameObject crown = CreatePrimitive("Snowy Crown", PrimitiveType.Capsule, p + Vector3.up * 1.2f, new Vector3(1.1f, 1.7f, 1.1f), new Color(0.18f, 0.35f, 0.33f));
                crown.transform.SetParent(trunk.transform, true);
            }
        }

        private void CreateWorkers()
        {
            Color[] coats = { new Color(0.85f, 0.25f, 0.17f), new Color(0.95f, 0.66f, 0.16f), new Color(0.18f, 0.48f, 0.7f) };
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 4f, 0.65f, 2f + Mathf.Sin(angle) * 3f);
                GameObject worker = CreatePrimitive("Worker " + (i + 1), PrimitiveType.Capsule, p, new Vector3(0.45f, 0.65f, 0.45f), coats[i % coats.Length]);
                workers.Add(worker.transform);
            }
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.position = position;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = color;
            if (colonyRoot != null) go.transform.SetParent(colonyRoot.transform, true);
            return go;
        }

        private void AnimateWorkers()
        {
            for (int i = 0; i < workers.Count; i++)
            {
                float angle = dayPhase + i * Mathf.PI * 2f / workers.Count;
                Vector3 target = new Vector3(Mathf.Cos(angle) * (4f + i % 2), 0.65f, 2f + Mathf.Sin(angle) * (3f + i % 3));
                workers[i].position = Vector3.Lerp(workers[i].position, target, Time.deltaTime * 0.8f);
                workers[i].LookAt(target + new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
            }
        }

        private void HandleCamera()
        {
            if (Input.touchCount == 2)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                float previous = (a.position - a.deltaPosition - (b.position - b.deltaPosition)).magnitude;
                Zoom((previous - (a.position - b.position).magnitude) * 0.02f);
                return;
            }

            Vector2 pointer = Input.touchCount == 1 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            bool pressed = Input.touchCount == 1 || Input.GetMouseButton(0);
            if (pressed && pointer.y > Screen.height * 0.2f)
            {
                if (dragging)
                {
                    Vector2 delta = pointer - lastPointer;
                    worldCamera.transform.position += new Vector3(-delta.x, 0f, -delta.y) * 0.012f;
                    ClampCamera();
                }
                lastPointer = pointer;
                dragging = true;
            }
            else dragging = false;

            Zoom(-Input.mouseScrollDelta.y * 1.5f);
        }

        private void Zoom(float amount)
        {
            if (Mathf.Abs(amount) < 0.001f) return;
            worldCamera.transform.position += worldCamera.transform.forward * amount;
            ClampCamera();
        }

        private void ClampCamera()
        {
            Vector3 p = worldCamera.transform.position;
            p.x = Mathf.Clamp(p.x, -8f, 8f);
            p.y = Mathf.Clamp(p.y, 11f, 27f);
            p.z = Mathf.Clamp(p.z, -28f, -10f);
            worldCamera.transform.position = p;
        }

        private void OnGUI()
        {
            if (WorldMapManager.IsWorldMapActive) return;
            GUI.depth = 100;
            EnsureStyles();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(Screen.width / 1280f, 0.75f, 1.35f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.Box(new Rect(18f, 16f, width - 36f, 62f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(36f, 25f, 270f, 42f), "FROSTBOUND FRONTIER", titleStyle);
            GUI.Label(new Rect(305f, 27f, 280f, 38f), SupabaseSyncClient.Status, resourceStyle);
            DrawResource(new Rect(width - 690f, 27f, 145f, 38f), "TEMP", Mathf.RoundToInt(state.temperature), TemperatureColor());
            DrawResource(new Rect(width - 530f, 27f, 145f, 38f), "MADERA", state.wood, new Color(0.82f, 0.63f, 0.36f));
            DrawResource(new Rect(width - 370f, 27f, 145f, 38f), "COMIDA", state.food, new Color(0.48f, 0.82f, 0.4f));
            DrawResource(new Rect(width - 210f, 27f, 150f, 38f), "LIBRES", AvailableWorkers(), new Color(0.45f, 0.78f, 1f));

            if (AllianceManager.IsPanelOpen || ResearchManager.IsPanelOpen)
            {
                GUI.matrix = oldMatrix;
                return;
            }

            DrawStatusPanel();
            DrawTutorial(width);
            if (GUI.Button(new Rect(width - 540f, 98f, 155f, 42f), "HÉROES", buttonStyle)) heroesPanelOpen = true;

            GUI.Box(new Rect(18f, height - 158f, width - 36f, 140f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(38f, height - 144f, 340f, 34f), BuildingName(selectedBuilding), titleStyle);
            GUI.Label(new Rect(38f, height - 108f, 460f, 50f), BuildingDescription(selectedBuilding), bodyStyle);
            int level = GetLevel(selectedBuilding);
            int cost = 75 * level;
            GUI.Label(new Rect(width - 455f, height - 143f, 190f, 32f), "NIVEL " + level, resourceStyle);
            DrawWorkerControls(width, height);
            DrawUpgradeControls(width, height, cost);
            DrawTrainingControls(width, height);
            DrawHospitalControls(width, height);
            DrawResearchControls(width, height);

            float cardX = 38f;
            foreach (Building building in buildings)
            {
                if (GUI.Button(new Rect(cardX, height - 58f, 155f, 32f), building.DisplayName, buttonStyle)) selectedBuilding = building.Id;
                cardX += 165f;
            }

            if (Time.unscaledTime < toastUntil)
            {
                GUI.Label(new Rect(width * 0.5f - 180f, 92f, 360f, 42f), toast, resourceStyle);
            }

            if (heroesPanelOpen) DrawHeroesPanel(width, height);

            GUI.matrix = oldMatrix;
        }

        private void DrawResource(Rect rect, string label, int value, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, 18f, 18f), accentTexture);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 25f, rect.y, rect.width - 25f, rect.height), label + "  " + value, resourceStyle);
        }

        private void DrawStatusPanel()
        {
            GUI.Box(new Rect(18f, 88f, 260f, 104f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(32f, 96f, 232f, 28f), "POBLACIÓN  " + state.population, resourceStyle);
            DrawMeter(new Rect(34f, 130f, 220f, 20f), state.populationHealth / 100f, "Salud " + Mathf.RoundToInt(state.populationHealth) + "%", new Color(0.22f, 0.78f, 0.48f));
            DrawMeter(new Rect(34f, 158f, 220f, 20f), state.populationHappiness / 100f, "Ánimo " + Mathf.RoundToInt(state.populationHappiness) + "%", new Color(0.95f, 0.68f, 0.18f));
        }

        private void DrawTutorial(float width)
        {
            GUI.Box(new Rect(width - 365f, 88f, 347f, 104f), GUIContent.none, bodyStyle);
            GUI.Label(new Rect(width - 350f, 96f, 315f, 28f), state.tutorialComplete ? "MISIÓN COMPLETADA" : "MISIÓN INICIAL", state.tutorialComplete ? resourceStyle : titleStyle);
            string objective = state.tutorialComplete
                ? "+150 madera  +100 comida recibidos"
                : "Mejora el Generador térmico a nivel 2";
            GUI.Label(new Rect(width - 350f, 128f, 315f, 50f), objective, bodyStyle);
        }

        private void DrawWorkerControls(float width, float height)
        {
            if (selectedBuilding != "sawmill" && selectedBuilding != "kitchen") return;
            int assigned = selectedBuilding == "sawmill" ? state.sawmillWorkers : state.kitchenWorkers;
            GUI.Label(new Rect(width - 650f, height - 143f, 185f, 32f), "TRABAJADORES " + assigned, resourceStyle);
            if (GUI.Button(new Rect(width - 650f, height - 105f, 85f, 34f), "− QUITAR", buttonStyle)) ChangeWorkers(selectedBuilding, -1);
            if (GUI.Button(new Rect(width - 557f, height - 105f, 92f, 34f), "+ ASIGNAR", buttonStyle)) ChangeWorkers(selectedBuilding, 1);
        }

        private void DrawUpgradeControls(float width, float height, int cost)
        {
            if (selectedBuilding == "barracks" || selectedBuilding == "hospital" || selectedBuilding == "research") return;
            Rect area = new Rect(width - 255f, height - 136f, 215f, 70f);
            if (IsUpgrading(selectedBuilding))
            {
                float progress = UpgradeProgress();
                DrawMeter(new Rect(area.x, area.y + 8f, area.width, 25f), progress, Mathf.RoundToInt(progress * 100f) + "%", new Color(1f, 0.55f, 0.12f));
                GUI.Label(new Rect(area.x, area.y + 37f, area.width, 28f), "LISTO EN " + UpgradeSecondsRemaining() + " s", resourceStyle);
                if (GUI.Button(new Rect(area.x - 160f, area.y + 35f, 150f, 30f), "PEDIR AYUDA", buttonStyle))
                    AllianceManager.Instance?.RequestHelp("BuildingUpgrade", selectedBuilding + "_01");
                return;
            }

            bool anotherUpgrade = !string.IsNullOrEmpty(state.upgradingBuilding);
            string label = anotherUpgrade ? "COLA OCUPADA\n" + BuildingName(state.upgradingBuilding) : "MEJORAR\n" + cost + " madera";
            bool previous = GUI.enabled;
            GUI.enabled = !anotherUpgrade;
            if (GUI.Button(area, label, buttonStyle)) StartUpgrade(cost);
            GUI.enabled = previous;
        }

        private void DrawTrainingControls(float width, float height)
        {
            if (selectedBuilding != "barracks") return;
            Rect area = new Rect(width - 455f, height - 136f, 415f, 70f);
            if (state.trainingAmount > 0)
            {
                long duration = state.trainingEndsUtcTicks - state.trainingStartedUtcTicks;
                float progress = duration <= 0 ? 1f : Mathf.Clamp01((float)(DateTime.UtcNow.Ticks - state.trainingStartedUtcTicks) / duration);
                DrawMeter(new Rect(area.x, area.y + 4f, area.width, 28f), progress, "ENTRENANDO " + state.trainingAmount + " INFANTERÍA", new Color(0.25f, 0.72f, 1f));
                int remaining = Mathf.Max(0, Mathf.CeilToInt((float)new TimeSpan(state.trainingEndsUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds));
                GUI.Label(new Rect(area.x, area.y + 38f, area.width, 28f), "LISTO EN " + remaining + " s", resourceStyle);
                return;
            }
            GUI.Label(new Rect(area.x, area.y, 190f, 30f), "INFANTERÍA DE NIEVE  " + state.snowInfantry, resourceStyle);
            if (GUI.Button(new Rect(area.x + 205f, area.y, 210f, 58f), "ENTRENAR 10\n50 comida · 10 s", buttonStyle))
            {
                if (state.food < 50) { ShowToast("Comida insuficiente"); return; }
                state.food -= 50;
                state.trainingAmount = 10;
                state.trainingStartedUtcTicks = DateTime.UtcNow.Ticks;
                state.trainingEndsUtcTicks = DateTime.UtcNow.AddSeconds(10).Ticks;
                Save();
                ShowToast("Entrenamiento iniciado");
            }
        }

        private void CompleteTrainingIfReady()
        {
            if (state == null || state.trainingAmount <= 0 || DateTime.UtcNow.Ticks < state.trainingEndsUtcTicks) return;
            int completed = state.trainingAmount;
            state.snowInfantry += completed;
            state.trainingAmount = 0;
            state.trainingStartedUtcTicks = 0;
            state.trainingEndsUtcTicks = 0;
            Save();
            ShowToast(completed + " Infantería de Nieve lista");
        }

        private void DrawHospitalControls(float width, float height)
        {
            if (selectedBuilding != "hospital") return;
            Rect area = new Rect(width - 500f, height - 142f, 460f, 82f);
            GUI.Label(new Rect(area.x, area.y, 225f, 30f), "HERIDOS  " + state.woundedInfantry, resourceStyle);
            if (state.healingAmount > 0)
            {
                long duration = state.healingEndsUtcTicks - state.healingStartedUtcTicks;
                float progress = duration <= 0 ? 1f : Mathf.Clamp01((float)(DateTime.UtcNow.Ticks - state.healingStartedUtcTicks) / duration);
                DrawMeter(new Rect(area.x, area.y + 34f, area.width, 27f), progress, "CURANDO " + state.healingAmount + " · " + HealingSecondsRemaining() + " s", new Color(0.24f, 0.86f, 0.68f));
                if (GUI.Button(new Rect(area.x - 160f, area.y + 31f, 150f, 34f), "PEDIR AYUDA", buttonStyle))
                    AllianceManager.Instance?.RequestHelp("HospitalHealing", "hospital_01");
                return;
            }
            int amount = Mathf.Min(10, state.woundedInfantry);
            bool previous = GUI.enabled;
            GUI.enabled = amount > 0 && !healingRequestPending;
            if (GUI.Button(new Rect(area.x + 230f, area.y, 230f, 58f), amount > 0 ? "CURAR " + amount + "\n" + (amount * 2) + " comida" : "SIN HERIDOS", buttonStyle)) StartHealing(amount);
            GUI.enabled = previous;
        }

        private void DrawResearchControls(float width, float height)
        {
            if (selectedBuilding != "research") return;
            GUI.Label(new Rect(width - 530f, height - 142f, 260f, 36f), "ÁRBOL DE TECNOLOGÍAS", resourceStyle);
            if (GUI.Button(new Rect(width - 255f, height - 142f, 215f, 62f), "ABRIR INVESTIGACIÓN", buttonStyle))
                ResearchManager.Instance?.OpenPanel();
        }

        private void StartHealing(int amount)
        {
            if (amount <= 0 || state.food < amount * 2) { ShowToast("Comida insuficiente para curar"); return; }
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud != null && cloud.CanQueryWorld)
            {
                healingRequestPending = true;
                StartCoroutine(cloud.StartHealing(amount, result =>
                {
                    healingRequestPending = false;
                    state.food = Mathf.Max(0, state.food - result.food_cost);
                    ApplyHospitalCloudState(result);
                    ShowToast("Curación iniciada");
                }, error => { healingRequestPending = false; ShowToast(error); }));
                return;
            }
            state.food -= amount * 2;
            state.woundedInfantry -= amount;
            state.healingAmount = amount;
            state.healingStartedUtcTicks = DateTime.UtcNow.Ticks;
            state.healingEndsUtcTicks = DateTime.UtcNow.AddSeconds(Mathf.Max(5, amount * 2)).Ticks;
            Save();
            ShowToast("Curación local iniciada");
        }

        private void CompleteHealingIfReady()
        {
            if (state == null || state.healingAmount <= 0 || DateTime.UtcNow.Ticks < state.healingEndsUtcTicks || healingRequestPending) return;
            int localAmount = state.healingAmount;
            SupabaseSyncClient cloud = SupabaseSyncClient.Instance;
            if (cloud != null && cloud.CanQueryWorld)
            {
                healingRequestPending = true;
                StartCoroutine(cloud.CompleteHealing(result =>
                {
                    healingRequestPending = false;
                    int completed = Mathf.Max(0, result.completed);
                    state.snowInfantry += completed;
                    state.woundedInfantry = Mathf.Max(0, result.wounded);
                    state.healingAmount = 0; state.healingStartedUtcTicks = 0; state.healingEndsUtcTicks = 0;
                    Save(); ShowToast(completed + " tropas recuperadas");
                }, error => { healingRequestPending = false; ShowToast(error); }));
                return;
            }
            state.snowInfantry += localAmount;
            state.healingAmount = 0; state.healingStartedUtcTicks = 0; state.healingEndsUtcTicks = 0;
            Save(); ShowToast(localAmount + " tropas recuperadas");
        }

        private int HealingSecondsRemaining() => Mathf.Max(0, Mathf.CeilToInt((float)new TimeSpan(state.healingEndsUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds));

        private void DrawHeroesPanel(float width, float height)
        {
            if (heroPortraitTexture == null) heroPortraitTexture = CreateHeroPortrait();
            Rect panel = new Rect(width * 0.5f - 300f, height * 0.5f - 205f, 600f, 410f);
            GUI.DrawTexture(panel, panelTexture);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 20f, 430f, 42f), "COLECCIÓN DE HÉROES", titleStyle);
            if (GUI.Button(new Rect(panel.x + 530f, panel.y + 16f, 48f, 42f), "×", buttonStyle)) heroesPanelOpen = false;
            GUI.DrawTexture(new Rect(panel.x + 32f, panel.y + 82f, 180f, 220f), heroPortraitTexture);
            GUI.Label(new Rect(panel.x + 238f, panel.y + 82f, 320f, 42f), "ELENA", titleStyle);
            GUI.Label(new Rect(panel.x + 238f, panel.y + 126f, 320f, 36f), "CAZADORA DEL HIELO", resourceStyle);
            GUI.Label(new Rect(panel.x + 238f, panel.y + 180f, 320f, 118f), "Nivel 1   ·   ★ 1\nLíder del ejército\n+15% poder contra bestias\n+20% velocidad de marcha", bodyStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 330f, panel.width - 68f, 48f), "Elena está disponible para las marchas de recolección y ataque.", resourceStyle);
        }

        private void DrawMeter(Rect rect, float value, string label, Color fill)
        {
            value = Mathf.Clamp01(value);
            GUI.DrawTexture(rect, progressBackTexture);
            Color previous = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * value, rect.height - 4f), progressFillTexture);
            GUI.color = previous;
            GUI.Label(rect, label, resourceStyle);
        }

        private void StartUpgrade(int cost)
        {
            if (selectedBuilding == "shelter")
            {
                ShowToast("El refugio se desbloqueará en el siguiente hito");
                return;
            }
            if (state.wood < cost)
            {
                ShowToast("Madera insuficiente");
                return;
            }
            state.wood -= cost;
            int duration = 8 + GetLevel(selectedBuilding) * 5;
            state.upgradingBuilding = selectedBuilding;
            state.upgradeStartedUtcTicks = DateTime.UtcNow.Ticks;
            state.upgradeEndsUtcTicks = DateTime.UtcNow.AddSeconds(duration).Ticks;
            Save();
            ShowToast("Construcción iniciada: " + duration + " s");
        }

        private void CompleteUpgradeIfReady()
        {
            if (string.IsNullOrEmpty(state.upgradingBuilding) || DateTime.UtcNow.Ticks < state.upgradeEndsUtcTicks) return;
            string completed = state.upgradingBuilding;
            if (completed == "generator") state.generatorLevel++;
            if (completed == "sawmill") state.sawmillLevel++;
            if (completed == "kitchen") state.kitchenLevel++;
            if (completed == "barracks") state.barracksLevel++;
            Building building = buildings.Find(item => item.Id == completed);
            if (building != null) building.Root.transform.localScale *= 1.08f;
            state.upgradingBuilding = "";
            state.upgradeStartedUtcTicks = 0;
            state.upgradeEndsUtcTicks = 0;
            ShowToast(BuildingName(completed) + " alcanzó nivel " + GetLevel(completed));
            CheckTutorial();
            Save();
        }

        private bool IsUpgrading(string id) => state.upgradingBuilding == id;

        private float UpgradeProgress()
        {
            long duration = state.upgradeEndsUtcTicks - state.upgradeStartedUtcTicks;
            if (duration <= 0) return 1f;
            return Mathf.Clamp01((float)(DateTime.UtcNow.Ticks - state.upgradeStartedUtcTicks) / duration);
        }

        private int UpgradeSecondsRemaining()
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)new TimeSpan(state.upgradeEndsUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds));
        }

        private void SimulateSecond()
        {
            int fuelCost = Mathf.Max(1, state.generatorLevel);
            bool generatorFueled = state.wood >= fuelCost;
            if (generatorFueled)
            {
                state.wood -= fuelCost;
                state.heat += state.generatorLevel;
                float targetTemperature = 16f + state.generatorLevel * 3f;
                state.temperature = Mathf.MoveTowards(state.temperature - 0.12f, targetTemperature, 0.55f + state.generatorLevel * 0.1f);
            }
            else
            {
                state.temperature = Mathf.Max(-35f, state.temperature - 0.85f);
                state.heat = Mathf.Max(0, state.heat - 2);
            }

            woodProductionCarry += state.sawmillWorkers * state.sawmillLevel * 2f * ResearchManager.WoodProductionMultiplier * AllianceManager.ResourceProductionMultiplier;
            foodProductionCarry += state.kitchenWorkers * state.kitchenLevel * 2f * ResearchManager.FoodProductionMultiplier * AllianceManager.ResourceProductionMultiplier;
            int producedWood = Mathf.FloorToInt(woodProductionCarry);
            int producedFood = Mathf.FloorToInt(foodProductionCarry);
            state.wood += producedWood; state.food += producedFood;
            woodProductionCarry -= producedWood; foodProductionCarry -= producedFood;
            state.food = Mathf.Max(0, state.food - Mathf.Max(1, state.population / 3));

            bool dangerousCold = state.temperature < 5f;
            bool hungry = state.food <= 0;
            float healthDelta = dangerousCold ? -0.7f : 0.12f;
            float happinessDelta = dangerousCold ? -0.8f : 0.08f;
            if (hungry)
            {
                healthDelta -= 0.6f;
                happinessDelta -= 0.8f;
            }
            state.populationHealth = Mathf.Clamp(state.populationHealth + healthDelta, 0f, 100f);
            state.populationHappiness = Mathf.Clamp(state.populationHappiness + happinessDelta, 0f, 100f);
        }

        private void ChangeWorkers(string buildingId, int delta)
        {
            int current = buildingId == "sawmill" ? state.sawmillWorkers : state.kitchenWorkers;
            if (delta > 0 && AvailableWorkers() <= 0)
            {
                ShowToast("No quedan supervivientes disponibles");
                return;
            }
            if (delta < 0 && current <= 0)
            {
                ShowToast("No hay trabajadores que retirar");
                return;
            }
            current = Mathf.Clamp(current + delta, 0, state.population);
            if (buildingId == "sawmill") state.sawmillWorkers = current;
            else state.kitchenWorkers = current;
            Save();
        }

        private int AvailableWorkers() => Mathf.Max(0, state.population - state.sawmillWorkers - state.kitchenWorkers);

        private void CheckTutorial()
        {
            if (state.tutorialComplete || state.generatorLevel < 2) return;
            state.tutorialComplete = true;
            state.tutorialRewardClaimed = true;
            state.wood += 150;
            state.food += 100;
            ShowToast("¡Misión completada! Recompensa recibida");
        }

        private Color TemperatureColor()
        {
            if (state.temperature < 5f) return new Color(0.4f, 0.75f, 1f);
            if (state.temperature > 18f) return new Color(1f, 0.45f, 0.12f);
            return new Color(0.86f, 0.94f, 1f);
        }

        private int GetLevel(string id)
        {
            if (id == "generator") return state.generatorLevel;
            if (id == "sawmill") return state.sawmillLevel;
            if (id == "kitchen") return state.kitchenLevel;
            if (id == "barracks") return state.barracksLevel;
            return 1;
        }

        private static string BuildingName(string id)
        {
            if (id == "generator") return "Generador térmico";
            if (id == "sawmill") return "Aserradero";
            if (id == "kitchen") return "Cocina comunal";
            if (id == "barracks") return "Cuartel de Infantería";
            if (id == "hospital") return "Enfermería";
            if (id == "research") return "Laboratorio de Investigación";
            return "Refugio";
        }

        private static string BuildingDescription(string id)
        {
            if (id == "generator") return "Produce calor y mantiene habitable el asentamiento.";
            if (id == "sawmill") return "Recupera madera congelada para nuevas construcciones.";
            if (id == "kitchen") return "Convierte suministros en raciones para los supervivientes.";
            if (id == "barracks") return "Entrena Infantería de Nieve para marchas y recolección.";
            if (id == "hospital") return "Recibe tropas heridas y las devuelve al servicio activo.";
            if (id == "research") return "Desbloquea mejoras económicas y militares permanentes.";
            return "Aloja a la población y protege a los trabajadores del frío.";
        }

        private void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            panelTexture = MakeTexture(new Color(0.035f, 0.07f, 0.1f, 0.93f));
            buttonTexture = MakeTexture(new Color(0.12f, 0.29f, 0.38f, 1f));
            accentTexture = MakeTexture(Color.white);
            progressBackTexture = MakeTexture(new Color(0.015f, 0.035f, 0.05f, 1f));
            progressFillTexture = MakeTexture(Color.white);
            heroPortraitTexture = CreateHeroPortrait();
            bodyStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(14, 14, 8, 8) };
            bodyStyle.normal.background = panelTexture;
            bodyStyle.normal.textColor = new Color(0.88f, 0.94f, 0.97f);
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            resourceStyle = new GUIStyle(bodyStyle) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = buttonTexture;
            buttonStyle.active.background = accentTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = new Color(0.03f, 0.08f, 0.1f);
            stylesReady = true;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateHeroPortrait()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color ice = new Color(0.14f, 0.48f, 0.68f, 1f);
            Color coat = new Color(0.05f, 0.18f, 0.28f, 1f);
            Color skin = new Color(0.96f, 0.78f, 0.66f, 1f);
            Color hair = new Color(0.87f, 0.94f, 1f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - 64f, dy = y - 72f;
                Color pixel = Color.Lerp(new Color(0.03f, 0.16f, 0.25f), ice, y / 128f);
                if (dx * dx + dy * dy < 27f * 27f) pixel = hair;
                if (dx * dx + (dy + 3f) * (dy + 3f) < 20f * 20f && y > 50) pixel = skin;
                if (y < 49 && Mathf.Abs(dx) < 39f - y * 0.25f) pixel = coat;
                if ((x - 57) * (x - 57) + (y - 72) * (y - 72) < 5) pixel = coat;
                if ((x - 71) * (x - 71) + (y - 72) * (y - 72) < 5) pixel = coat;
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply();
            return texture;
        }

        private void ApplyOfflineProgress()
        {
            if (state.lastSavedUtcTicks <= 0) return;
            double seconds = Math.Min((DateTime.UtcNow - new DateTime(state.lastSavedUtcTicks, DateTimeKind.Utc)).TotalSeconds, 7200d);
            if (seconds <= 2d) return;
            state.heat += Mathf.FloorToInt((float)seconds * state.generatorLevel);
            state.wood += Mathf.FloorToInt((float)seconds * state.sawmillLevel);
            state.food += Mathf.FloorToInt((float)seconds * state.kitchenLevel);
            toast = "Producción sin conexión: " + Mathf.FloorToInt((float)seconds) + " s";
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString("frostbound-frontier-save", string.Empty);
            state = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            if (state == null) state = new SaveData();
            if (state.population <= 0)
            {
                state.population = 6;
                state.sawmillWorkers = 1;
                state.kitchenWorkers = 1;
                state.temperature = 12f;
                state.populationHealth = 100f;
                state.populationHappiness = 100f;
            }
            state.sawmillWorkers = Mathf.Clamp(state.sawmillWorkers, 0, state.population);
            state.kitchenWorkers = Mathf.Clamp(state.kitchenWorkers, 0, state.population - state.sawmillWorkers);
        }

        public long LocalSavedAtUtcTicks => state != null ? state.lastSavedUtcTicks : 0;

        public void AddGatheredResources(string resourceType, int amount)
        {
            if (state == null || amount <= 0) return;
            if (resourceType == "Wood") state.wood += amount;
            else if (resourceType == "Food") state.food += amount;
            else if (resourceType == "Coal") state.coal += amount;
            else return;
            Save();
            ShowToast("Marcha regresó con " + amount + " de " + resourceType);
        }

        public void SpendCrystalsLocally(int amount)
        {
            if (state == null || amount <= 0) return;
            state.crystals = Mathf.Max(0, state.crystals - amount);
            Save();
        }

        public void SpendResearchResourcesLocally(int wood, int food, int crystals)
        {
            if (state == null) return;
            state.wood = Mathf.Max(0, state.wood - Mathf.Max(0, wood));
            state.food = Mathf.Max(0, state.food - Mathf.Max(0, food));
            state.crystals = Mathf.Max(0, state.crystals - Mathf.Max(0, crystals));
            Save();
        }

        public void ApplyBattleOutcome(int casualties, int wounded, string lootType, int lootAmount)
        {
            state.snowInfantry = Mathf.Max(0, state.snowInfantry - Mathf.Max(0, casualties) - Mathf.Max(0, wounded));
            state.woundedInfantry += Mathf.Max(0, wounded);
            if (lootType == "Coal") state.coal += Mathf.Max(0, lootAmount);
            else if (lootType == "Crystals") state.crystals += Mathf.Max(0, lootAmount);
            else if (lootType == "Speedups") state.speedups += Mathf.Max(0, lootAmount);
            Save();
        }

        public void ApplyHeroCloudState(SupabaseSyncClient.HeroCloudState hero)
        {
            if (hero == null) return;
            state.elenaHeroId = hero.hero_id ?? string.Empty;
            state.elenaUnlocked = hero.hero_key == "elena_ice_huntress";
            Save();
        }

        public void ApplyHospitalCloudState(SupabaseSyncClient.HospitalCloudState hospital)
        {
            if (hospital == null) return;
            state.woundedInfantry = Mathf.Max(0, hospital.wounded);
            state.healingAmount = Mathf.Max(0, hospital.healing_amount);
            state.healingStartedUtcTicks = ParseCloudTicks(hospital.healing_started_at);
            state.healingEndsUtcTicks = ParseCloudTicks(hospital.healing_finishes_at);
            Save();
        }

        private static long ParseCloudTicks(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out DateTime parsed)
                ? parsed.ToUniversalTime().Ticks : 0;
        }

        public PlayerCloudState GetPlayerCloudState()
        {
            Save();
            return new PlayerCloudState
            {
                temperature = state.temperature,
                population = state.population,
                wood = state.wood,
                food = state.food,
                coal = state.coal,
                generatorLevel = state.generatorLevel,
                health = state.populationHealth,
                happiness = state.populationHappiness,
                power = state.generatorLevel * 100L + state.sawmillLevel * 50L + state.kitchenLevel * 50L + state.population * 10L,
                clientSavedAt = state.lastSavedUtcTicks
                ,snowInfantry = state.snowInfantry
                ,crystals = state.crystals
                ,speedups = state.speedups
            };
        }

        public BuildingCloudState[] GetBuildingCloudStates()
        {
            BuildingCloudState[] result = new BuildingCloudState[buildings.Count];
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                bool upgrading = state.upgradingBuilding == building.Id;
                result[i] = new BuildingCloudState
                {
                    slotId = building.Id + "_01",
                    buildingType = building.Id,
                    level = GetLevel(building.Id),
                    assignedWorkers = building.Id == "sawmill" ? state.sawmillWorkers : (building.Id == "kitchen" ? state.kitchenWorkers : 0),
                    upgradeStartedUtcTicks = upgrading ? state.upgradeStartedUtcTicks : 0,
                    finishesUtcTicks = upgrading ? state.upgradeEndsUtcTicks : 0,
                    posX = building.Root.transform.position.x,
                    posZ = building.Root.transform.position.z
                };
            }
            return result;
        }

        public void ApplyRelationalCloudState(PlayerCloudState player, BuildingCloudState[] cloudBuildings)
        {
            if (player != null)
            {
                state.temperature = player.temperature;
                state.population = Mathf.Max(0, player.population);
                state.wood = (int)Math.Min(int.MaxValue, Math.Max(0L, player.wood));
                state.food = (int)Math.Min(int.MaxValue, Math.Max(0L, player.food));
                state.coal = (int)Math.Min(int.MaxValue, Math.Max(0L, player.coal));
                state.generatorLevel = Mathf.Max(1, player.generatorLevel);
                state.populationHealth = Mathf.Clamp(player.health, 0f, 100f);
                state.populationHappiness = Mathf.Clamp(player.happiness, 0f, 100f);
                state.lastSavedUtcTicks = player.clientSavedAt;
                state.snowInfantry = Mathf.Max(0, player.snowInfantry);
                state.crystals = (int)Math.Min(int.MaxValue, Math.Max(0L, player.crystals));
                state.speedups = Mathf.Max(0, player.speedups);
            }

            if (cloudBuildings != null)
            {
                foreach (BuildingCloudState row in cloudBuildings)
                {
                    if (row == null) continue;
                    if (row.buildingType == "generator") state.generatorLevel = Mathf.Max(1, row.level);
                    if (row.buildingType == "sawmill")
                    {
                        state.sawmillLevel = Mathf.Max(1, row.level);
                        state.sawmillWorkers = Mathf.Max(0, row.assignedWorkers);
                    }
                    if (row.buildingType == "kitchen")
                    {
                        state.kitchenLevel = Mathf.Max(1, row.level);
                        state.kitchenWorkers = Mathf.Max(0, row.assignedWorkers);
                    }
                    if (row.buildingType == "barracks") state.barracksLevel = Mathf.Max(1, row.level);
                    if (row.finishesUtcTicks > DateTime.UtcNow.Ticks)
                    {
                        state.upgradingBuilding = row.buildingType;
                        state.upgradeStartedUtcTicks = row.upgradeStartedUtcTicks;
                        state.upgradeEndsUtcTicks = row.finishesUtcTicks;
                    }
                }
            }

            state.sawmillWorkers = Mathf.Clamp(state.sawmillWorkers, 0, state.population);
            state.kitchenWorkers = Mathf.Clamp(state.kitchenWorkers, 0, state.population - state.sawmillWorkers);
            foreach (Building building in buildings)
                building.Root.transform.localScale = building.BaseScale * (1f + (GetLevel(building.Id) - 1) * 0.08f);
            CompleteUpgradeIfReady();
            Save();
            ShowToast("Progreso relacional recuperado");
        }

        public string ExportCloudSaveJson()
        {
            Save();
            return JsonUtility.ToJson(state);
        }

        public bool ImportCloudSaveJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            SaveData imported = JsonUtility.FromJson<SaveData>(json);
            if (imported == null) return false;
            state = imported;
            if (state.population <= 0) state.population = 6;
            state.sawmillWorkers = Mathf.Clamp(state.sawmillWorkers, 0, state.population);
            state.kitchenWorkers = Mathf.Clamp(state.kitchenWorkers, 0, state.population - state.sawmillWorkers);
            foreach (Building building in buildings)
            {
                int level = GetLevel(building.Id);
                building.Root.transform.localScale = building.BaseScale * (1f + (level - 1) * 0.08f);
            }
            CompleteUpgradeIfReady();
            Save();
            ShowToast("Partida recuperada desde Supabase");
            return true;
        }

        private void Save()
        {
            state.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString("frostbound-frontier-save", JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }
    }
}
