using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Construit une cour de château médiévale avec les packs KayKit (CC0) :
    /// Dungeon Remastered pour la cour (dalles, murs, piliers, torches, bannières, tonneaux)
    /// et Medieval Hexagon pour le paysage autour (château, tours, montagnes, arbres).
    /// Les pièces sont mesurées à la construction : peu importe leur échelle ou leur point d'origine.
    /// </summary>
    public static class MedievalArena
    {
        const string Dungeon = "Assets/_Project/Art/KayKit/Dungeon/";
        const string Hexagon = "Assets/_Project/Art/KayKit/Hexagon/";
        const float HeroHeight = 1.9f;   // doit correspondre à la taille du chevalier dans ArenaBuilder
        const int SegmentsPerSide = 11;  // impair : la porte tombe au milieu du mur nord

        public static bool FilesPresent =>
            File.Exists(Dungeon + "wall.fbx") && File.Exists(Hexagon + "buildings/red/building_castle_red.fbx");

        public class Result
        {
            public float HalfSize;
            public Vector3[] SpawnPoints;
            public Vector3 PlayerStart;
        }

        static Material dungeonMaterial;
        static Material hexagonMaterial;
        static float k;          // échelle appliquée à toutes les pièces du donjon
        static float wallHeight; // hauteur réelle des murs après mise à l'échelle

        /// <summary>
        /// Construit la cour sous "level" (obstacles pris en compte par le NavMesh)
        /// et le paysage sous "backdrop" (décor seulement).
        /// </summary>
        public static Result Build(Transform level, Transform backdrop)
        {
            dungeonMaterial = KayKitCharacters.CreateTexturedMaterial("KayKit_Dungeon", Dungeon + "dungeon_texture.png");
            hexagonMaterial = KayKitCharacters.CreateTexturedMaterial("KayKit_Hexagon", Hexagon + "hexagons_medieval.png");

            // Les packs KayKit sont conçus à la même échelle : on applique au décor
            // le même facteur qu'au chevalier, pour garder les proportions prévues par l'artiste.
            Bounds knight = Measure(KayKitCharacters.KnightPath);
            k = HeroHeight / Mathf.Max(0.01f, knight.size.y);

            Bounds wall = Measure(Dungeon + "wall.fbx");
            wallHeight = wall.size.y * k;
            bool wallAlongX = wall.size.x >= wall.size.z;
            float segment = Mathf.Max(wall.size.x, wall.size.z) * k;
            float thickness = Mathf.Min(wall.size.x, wall.size.z) * k;
            float half = segment * SegmentsPerSide / 2f;
            Debug.Log($"Arène : échelle {k:0.###}, murs {wallHeight:0.0} m, segment {segment:0.0} m, cour de {half * 2f:0} m");

            BuildFloor(level, half);
            BuildWalls(level, half, segment, thickness, wallAlongX);
            BuildPillarsAndProps(level, half);
            BuildBackdrop(backdrop, half);

            float s = half - 4f;
            return new Result
            {
                HalfSize = half,
                PlayerStart = new Vector3(0f, 0.1f, -half * 0.3f),
                SpawnPoints = new[]
                {
                    new Vector3(-s, 0f, s), new Vector3(s, 0f, s),
                    new Vector3(s, 0f, -s), new Vector3(-s, 0f, -s),
                    new Vector3(0f, 0f, half - 3f),
                },
            };
        }

        // ---------------------------------------------------------------- Sol

        static void BuildFloor(Transform level, float half)
        {
            // Sol physique : une seule boîte invisible (le NavMesh se base dessus).
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(level);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(half * 2f + 6f, 1f, half * 2f + 6f);

            var tiles = new GameObject("FloorTiles").transform;
            tiles.SetParent(level);
            Bounds tile = Measure(Dungeon + "floor_tile_large.fbx");
            float size = Mathf.Max(tile.size.x, tile.size.z) * k;
            int count = Mathf.CeilToInt(half * 2f / size);
            // Une seule sorte de dalle (même hauteur partout), tournée au hasard pour casser la répétition.
            var random = new System.Random(7);
            for (int x = 0; x < count; x++)
            for (int z = 0; z < count; z++)
            {
                var position = new Vector3(-half + size * (x + 0.5f), 0f, -half + size * (z + 0.5f));
                Place(Dungeon + "floor_tile_large.fbx", position, 90f * random.Next(4), k, tiles, dungeonMaterial, Anchor.Top);
            }
        }

        // ---------------------------------------------------------------- Murs

        static void BuildWalls(Transform level, float half, float segment, float thickness, bool wallAlongX)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(level);

            // Nord, est, ouest : murs hauts variés. Sud (côté caméra) : demi-murs pour ne pas cacher le héros.
            string[] northPattern = { "wall", "wall_arched", "wall", "wall_window_open", "wall_arched" };
            northPattern = Enumerable.Range(0, SegmentsPerSide)
                .Select(i => i == SegmentsPerSide / 2 ? "wall_gated" : northPattern[i % northPattern.Length]).ToArray();
            string[] sidePattern = { "wall", "wall_cracked", "wall_arched", "wall", "wall_window_closed", "wall_arched", "wall_broken" };
            string[] southPattern = { "wall_half" };

            BuildSide(walls, Vector3.forward, half, segment, wallAlongX, northPattern);
            BuildSide(walls, Vector3.right, half, segment, wallAlongX, sidePattern);
            BuildSide(walls, Vector3.left, half, segment, wallAlongX, sidePattern);
            BuildSide(walls, Vector3.back, half, segment, wallAlongX, southPattern);

            // Coins : gros piliers.
            foreach (Vector3 corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                Place(Dungeon + "pillar_decorated.fbx", corner * half, 0f, k * 1.3f, walls, dungeonMaterial, Anchor.Bottom);

            // Collisions : une boîte invisible par côté.
            foreach (Vector3 normal in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var blocker = new GameObject("WallCollider");
                blocker.transform.SetParent(level);
                blocker.transform.position = normal * (half + 0.2f) + Vector3.up * 3f;
                var box = blocker.AddComponent<BoxCollider>();
                bool alongX = Mathf.Abs(normal.z) > 0.5f;
                box.size = alongX ? new Vector3(half * 2f + 2f, 6f, thickness + 1f) : new Vector3(thickness + 1f, 6f, half * 2f + 2f);
            }

            // Torches allumées et bannières sur la face intérieure des murs.
            foreach (Vector3 normal in new[] { Vector3.forward, Vector3.left, Vector3.right })
            {
                Vector3 along = Vector3.Cross(Vector3.up, normal);
                Vector3 face = normal * (half - thickness * 0.5f - 0.05f);
                float yaw = Quaternion.LookRotation(-normal).eulerAngles.y; // tourné vers le centre

                foreach (float t in new[] { -0.55f, 0.55f })
                {
                    GameObject torch = Place(Dungeon + "torch_mounted.fbx", face + along * (half * t) + Vector3.up * (wallHeight * 0.5f),
                        yaw, k, walls, dungeonMaterial, Anchor.Bottom);
                    AddTorchLight(torch.transform);
                }
                foreach (float t in new[] { -0.25f, 0.25f })
                {
                    Place(Dungeon + "banner_patternA_red.fbx", face + along * (half * t) + Vector3.up * (wallHeight * 0.3f),
                        yaw, k, walls, dungeonMaterial, Anchor.Bottom);
                }
            }
        }

        static void BuildSide(Transform parent, Vector3 normal, float half, float segment, bool wallAlongX, string[] pattern)
        {
            Vector3 along = Vector3.Cross(Vector3.up, normal);
            bool sideAlongX = Mathf.Abs(along.x) > 0.5f;
            float yaw = sideAlongX == wallAlongX ? 0f : 90f;
            if (Vector3.Dot(normal, Vector3.back) > 0.5f || Vector3.Dot(normal, Vector3.left) > 0.5f) yaw += 180f;

            for (int i = 0; i < SegmentsPerSide; i++)
            {
                Vector3 position = normal * half + along * (-half + segment * (i + 0.5f));
                Place(Dungeon + pattern[i % pattern.Length] + ".fbx", position, yaw, k, parent, dungeonMaterial, Anchor.Bottom);
            }
        }

        static void AddTorchLight(Transform torch)
        {
            var go = new GameObject("TorchLight");
            go.transform.SetParent(torch, false);
            Bounds bounds = CombinedBounds(torch.gameObject);
            go.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.intensity = 3f;
            light.range = 8f;
            light.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- Obstacles et accessoires

        static void BuildPillarsAndProps(Transform level, float half)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(level);

            float p = half * 0.42f;
            foreach (Vector3 position in new[] { new Vector3(-p, 0, p), new Vector3(p, 0, p), new Vector3(-p, 0, -p), new Vector3(p, 0, -p) })
            {
                GameObject pillar = Place(Dungeon + "pillar_decorated.fbx", position, 0f, k, props, dungeonMaterial, Anchor.Bottom);
                AddCollider(pillar, level);
            }

            float edge = half - 2f;
            (string name, Vector3 position, float yaw)[] items =
            {
                ("barrel_large", new Vector3(-edge, 0f, edge - 3f), 0f),
                ("barrel_small_stack", new Vector3(-edge + 1.8f, 0f, edge - 0.5f), 30f),
                ("crates_stacked", new Vector3(edge - 0.5f, 0f, edge - 4f), 90f),
                ("box_stacked", new Vector3(edge - 3f, 0f, edge - 0.5f), 0f),
                ("rubble_large", new Vector3(edge - 1f, 0f, -2f), 45f),
                ("barrel_large_decorated", new Vector3(-edge, 0f, -3f), 0f),
                ("chest_gold", new Vector3(0f, 0f, edge - 0.5f), 180f),
                ("keg_decorated", new Vector3(-edge + 0.5f, 0f, 3.5f), 90f),
                ("table_medium_broken", new Vector3(edge - 1.5f, 0f, 5f), 20f),
                ("rubble_half", new Vector3(-4f, 0f, -edge + 1f), 0f),
                ("box_large", new Vector3(5f, 0f, -edge + 1f), 15f),
            };
            foreach (var (name, position, yaw) in items)
            {
                GameObject prop = Place(Dungeon + name + ".fbx", position, yaw, k, props, dungeonMaterial, Anchor.Bottom);
                if (prop != null && name != "chest_gold") AddCollider(prop, level);
            }

            // Râteliers d'armes et boucliers accrochés de chaque côté de la porte.
            Place(Dungeon + "sword_shield.fbx", new Vector3(-4f, wallHeight * 0.35f, half - 0.6f), 180f, k, props, dungeonMaterial, Anchor.Bottom);
            Place(Dungeon + "sword_shield_gold.fbx", new Vector3(4f, wallHeight * 0.35f, half - 0.6f), 180f, k, props, dungeonMaterial, Anchor.Bottom);
        }

        // ---------------------------------------------------------------- Paysage autour

        static void BuildBackdrop(Transform backdrop, float half)
        {
            GameObject outside = GameObject.CreatePrimitive(PrimitiveType.Plane);
            outside.name = "OuterGround";
            Object.DestroyImmediate(outside.GetComponent<Collider>());
            outside.transform.SetParent(backdrop);
            outside.transform.position = new Vector3(0f, -0.05f, 0f);
            outside.transform.localScale = new Vector3(30f, 1f, 30f);
            outside.GetComponent<Renderer>().sharedMaterial = BuilderUtility.CreateMaterial("OuterGround", new Color(0.2f, 0.26f, 0.15f));

            string b = Hexagon + "buildings/red/";
            string n = Hexagon + "decoration/nature/";
            PlaceHeight(b + "building_castle_red.fbx", new Vector3(0f, 0f, half + 16f), 180f, 18f, backdrop);
            PlaceHeight(b + "building_tower_A_red.fbx", new Vector3(-half - 6f, 0f, half + 6f), 135f, 11f, backdrop);
            PlaceHeight(b + "building_tower_B_red.fbx", new Vector3(half + 6f, 0f, half + 6f), 225f, 11f, backdrop);
            PlaceHeight(b + "building_church_red.fbx", new Vector3(-half - 14f, 0f, 4f), 90f, 10f, backdrop);
            PlaceHeight(b + "building_barracks_red.fbx", new Vector3(half + 12f, 0f, 2f), 270f, 7f, backdrop);
            PlaceHeight(b + "building_windmill_red.fbx", new Vector3(half + 20f, 0f, half + 18f), 200f, 12f, backdrop);

            PlaceHeight(n + "mountain_B_grass_trees.fbx", new Vector3(-half - 26f, 0f, half + 28f), 0f, 26f, backdrop);
            PlaceHeight(n + "mountain_C_grass_trees.fbx", new Vector3(half + 30f, 0f, half + 34f), 60f, 30f, backdrop);
            PlaceHeight(n + "mountain_A_grass_trees.fbx", new Vector3(14f, 0f, half + 46f), 120f, 34f, backdrop);
            PlaceHeight(n + "hills_A_trees.fbx", new Vector3(-half - 18f, 0f, -half - 12f), 0f, 7f, backdrop);
            PlaceHeight(n + "hills_C_trees.fbx", new Vector3(half + 18f, 0f, -half - 14f), 90f, 7f, backdrop);

            var random = new System.Random(11);
            string[] trees = { "trees_A_large", "trees_B_large", "tree_single_A", "trees_A_medium", "tree_single_B" };
            for (float z = -half; z <= half + 8f; z += 6f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var position = new Vector3(side * (half + 4f + (float)random.NextDouble() * 3f), 0f, z + (float)random.NextDouble() * 2f);
                    PlaceHeight(n + trees[random.Next(trees.Length)] + ".fbx", position, random.Next(360), 6f + (float)random.NextDouble() * 2f, backdrop);
                }
            }
        }

        // ---------------------------------------------------------------- Placement

        enum Anchor { Bottom, Top }

        /// <summary>
        /// Place un modèle pour que le centre de son volume soit en (x, z) de "target",
        /// et que son bas (ou son haut) soit à la hauteur target.y.
        /// </summary>
        static GameObject Place(string path, Vector3 target, float yaw, float scale, Transform parent, Material material, Anchor anchor)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning("Arène : modèle introuvable " + path);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                r.sharedMaterials = materials;
            }
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            go.isStatic = true;

            Bounds bounds = CombinedBounds(go);
            float y = anchor == Anchor.Bottom ? target.y - bounds.min.y : target.y - bounds.max.y;
            go.transform.position += new Vector3(target.x - bounds.center.x, y, target.z - bounds.center.z);
            return go;
        }

        /// <summary>Place un élément du paysage en le mettant à l'échelle pour mesurer "height" mètres.</summary>
        static void PlaceHeight(string path, Vector3 target, float yaw, float height, Transform parent)
        {
            Bounds bounds = Measure(path);
            float scale = bounds.size.y > 0.001f ? height / bounds.size.y : 1f;
            Place(path, target, yaw, scale, parent, hexagonMaterial, Anchor.Bottom);
        }

        /// <summary>Boîte de collision alignée sur le volume visible du modèle (placée sous "level").</summary>
        static void AddCollider(GameObject model, Transform level)
        {
            if (model == null) return;
            Bounds bounds = CombinedBounds(model);
            var go = new GameObject(model.name + "_Collider");
            go.transform.SetParent(level);
            go.transform.position = bounds.center;
            go.AddComponent<BoxCollider>().size = bounds.size;
        }

        static readonly Dictionary<string, Bounds> MeasureCache = new Dictionary<string, Bounds>();

        /// <summary>Taille du modèle à l'échelle 1, sans rotation.</summary>
        static Bounds Measure(string path)
        {
            if (MeasureCache.TryGetValue(path, out Bounds cached)) return cached;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return new Bounds(Vector3.zero, Vector3.one);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            Bounds bounds = CombinedBounds(go);
            Object.DestroyImmediate(go);
            MeasureCache[path] = bounds;
            return bounds;
        }

        static Bounds CombinedBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
