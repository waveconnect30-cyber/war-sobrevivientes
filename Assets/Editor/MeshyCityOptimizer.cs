#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrostboundFrontier.EditorTools
{
    public sealed class MeshyCityPostprocessor : AssetPostprocessor
    {
        private bool IsCityAsset => assetPath.StartsWith("Assets/Art/MeshyCity/");

        private void OnPreprocessModel()
        {
            if (!IsCityAsset) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private void OnPreprocessTexture()
        {
            if (!IsCityAsset) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 65;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 65;
            importer.SetPlatformTextureSettings(android);
        }
    }

    public static class MeshyCityOptimizer
    {
        private const string SourcePath = "Assets/Art/MeshyCity/MeshyCity.fbx";
        private const string AlbedoPath = "Assets/Art/MeshyCity/MeshyCity_Albedo.png";
        private const string OutputFolder = "Assets/Resources/World";
        private const string PrefabPath = OutputFolder + "/MeshyCity_World.prefab";
        private const string MaterialPath = OutputFolder + "/MeshyCity_URP_Lit.mat";
        private const string ReportPath = OutputFolder + "/MeshyCity_Optimization_Report.txt";

        [MenuItem("Frostbound Frontier/Assets/Generar ciudad Meshy optimizada")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new FileNotFoundException("No se pudo importar la ciudad", SourcePath);
            Material cityMaterial = CreateLightweightMaterial();

            GameObject instance = Object.Instantiate(source);
            instance.name = "Meshy City World LOD";
            Bounds sourceBounds = CalculateBounds(instance);
            float maxXZ = Mathf.Max(.001f, Mathf.Max(sourceBounds.size.x, sourceBounds.size.z));
            float normalizedScale = .88f / maxXZ;

            GameObject root = new GameObject("Meshy City World");
            GameObject high = new GameObject("LOD0 Optimized");
            GameObject medium = new GameObject("LOD1 Low Poly");
            GameObject far = new GameObject("LOD2 Strategic Icon");
            high.transform.SetParent(root.transform, false);
            medium.transform.SetParent(root.transform, false);
            far.transform.SetParent(root.transform, false);
            high.transform.localScale = medium.transform.localScale = Vector3.one * normalizedScale;
            high.transform.localPosition = medium.transform.localPosition = -sourceBounds.center * normalizedScale;

            List<Renderer> lod0 = BuildLevel(instance, high.transform, .006f, "LOD0", cityMaterial);
            List<Renderer> lod1 = BuildLevel(instance, medium.transform, .035f, "LOD1", cityMaterial);
            Object.DestroyImmediate(instance);

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/FrostboundSkin/icon_wood.svg");
            SpriteRenderer sprite = far.AddComponent<SpriteRenderer>();
            sprite.sprite = icon;
            sprite.color = new Color(.25f, .9f, 1f, 1f);
            sprite.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (icon != null)
            {
                float iconSize = Mathf.Max(.001f, Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));
                sprite.transform.localScale = Vector3.one * (.72f / iconSize);
            }

            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] {
                new LOD(.48f, lod0.ToArray()),
                new LOD(.16f, lod1.ToArray()),
                new LOD(.025f, new Renderer[] { sprite })
            });
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.RecalculateBounds();
            int lod0Vertices = 0, lod0Triangles = 0, lod1Vertices = 0, lod1Triangles = 0;
            CountGeometry(lod0, ref lod0Vertices, ref lod0Triangles);
            CountGeometry(lod1, ref lod1Vertices, ref lod1Triangles);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            WriteReport(lod0Vertices, lod0Triangles, lod1Vertices, lod1Triangles);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log($"Ciudad Meshy optimizada: {PrefabPath} | LOD0 renderers {lod0.Count} | LOD1 renderers {lod1.Count}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static List<Renderer> BuildLevel(GameObject source, Transform parent, float cellRatio, string suffix, Material material)
        {
            List<Renderer> renderers = new List<Renderer>();
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                GameObject child = new GameObject(filter.name + "_" + suffix);
                child.transform.SetParent(parent, false);
                child.transform.localPosition = filter.transform.position;
                child.transform.localRotation = filter.transform.rotation;
                child.transform.localScale = filter.transform.lossyScale;
                Mesh mesh = ClusterMesh(filter.sharedMesh, cellRatio);
                mesh.name = filter.sharedMesh.name + "_" + suffix;
                string meshPath = OutputFolder + "/" + Sanitize(mesh.name) + ".asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderers.Add(renderer);
            }
            return renderers;
        }

        private static Material CreateLightweightMaterial()
        {
            AssetDatabase.DeleteAsset(MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "Meshy City URP Lit" };
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            material.mainTexture = albedo;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void WriteReport(int lod0Vertices, int lod0Triangles, int lod1Vertices, int lod1Triangles)
        {
            string absolutePrefab = Path.GetFullPath(PrefabPath);
            string report =
                $"Frostbound Frontier - Meshy City Optimization\n" +
                $"Tile footprint: 0.88 x 0.88 units (1 tile)\n" +
                $"Texture Android: 1024px ASTC 6x6\n" +
                $"LOD0: {lod0Vertices:N0} vertices / {lod0Triangles:N0} triangles\n" +
                $"LOD1: {lod1Vertices:N0} vertices / {lod1Triangles:N0} triangles\n" +
                $"LOD2: 1 SpriteRenderer quad\n" +
                $"Prefab YAML: {(File.Exists(absolutePrefab) ? new FileInfo(absolutePrefab).Length : 0):N0} bytes\n";
            File.WriteAllText(Path.GetFullPath(ReportPath), report);
            AssetDatabase.ImportAsset(ReportPath);
        }

        private static void CountGeometry(List<Renderer> renderers, ref int vertices, ref int triangles)
        {
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                vertices += mesh.vertexCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += (int)mesh.GetIndexCount(sub) / 3;
            }
        }

        private static Mesh ClusterMesh(Mesh source, float ratio)
        {
            Vector3[] vertices = source.vertices;
            Vector2[] uv = source.uv;
            Bounds bounds = source.bounds;
            float cell = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * ratio;
            Dictionary<Vector3Int, int> clusters = new Dictionary<Vector3Int, int>();
            List<Vector3> newVertices = new List<Vector3>();
            List<Vector2> newUv = new List<Vector2>();
            int[] remap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell), Mathf.RoundToInt(p.z / cell));
                if (!clusters.TryGetValue(key, out int index))
                {
                    index = newVertices.Count;
                    clusters.Add(key, index);
                    newVertices.Add(p);
                    newUv.Add(uv != null && uv.Length == vertices.Length ? uv[i] : Vector2.zero);
                }
                remap[i] = index;
            }
            Mesh result = new Mesh { indexFormat = newVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            result.SetVertices(newVertices);
            result.SetUVs(0, newUv);
            result.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] triangles = source.GetTriangles(sub);
                List<int> kept = new List<int>(triangles.Length);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = remap[triangles[i]], b = remap[triangles[i + 1]], c = remap[triangles[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    kept.Add(a); kept.Add(b); kept.Add(c);
                }
                result.SetTriangles(kept, sub, false);
            }
            result.RecalculateNormals();
            result.RecalculateTangents();
            result.RecalculateBounds();
            return result;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }
    }
}
#endif
