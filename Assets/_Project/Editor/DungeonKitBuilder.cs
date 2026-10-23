using System.Linq;
using ShadowBlade.Level;
using UnityEditor;
using UnityEngine;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Crée l'asset "DungeonKit" utilisé par le générateur de donjons : choix des modèles KayKit Dungeon
    /// et mesures (échelle, longueur d'un mur, taille d'une dalle).
    /// </summary>
    public static class DungeonKitBuilder
    {
        const string Dungeon = "Assets/_Project/Art/KayKit/Dungeon/";
        const string KitPath = BuilderUtility.PrefabsFolder + "/DungeonKit.asset";
        const float HeroHeight = 1.9f; // même taille que le chevalier

        public static DungeonKit CreateKit()
        {
            BuilderUtility.EnsureFolder(BuilderUtility.PrefabsFolder);
            var kit = AssetDatabase.LoadAssetAtPath<DungeonKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<DungeonKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            // Même échelle que les personnages : les packs KayKit sont conçus pour aller ensemble.
            kit.scale = HeroHeight / Mathf.Max(0.01f, Measure(KayKitCharacters.KnightPath).size.y);
            Bounds wall = Measure(Dungeon + "wall.fbx");
            kit.segment = Mathf.Max(wall.size.x, wall.size.z) * kit.scale;
            kit.wallHeight = wall.size.y * kit.scale;
            kit.wallAlongX = wall.size.x >= wall.size.z;
            Bounds tile = Measure(Dungeon + "floor_tile_large.fbx");
            kit.floorTileSize = Mathf.Max(tile.size.x, tile.size.z) * kit.scale;
            kit.material = KayKitCharacters.CreateTexturedMaterial("KayKit_Dungeon", Dungeon + "dungeon_texture.png");

            kit.floorTile = Model("floor_tile_large");
            kit.walls = Models("wall", "wall_arched", "wall_window_open", "wall_cracked", "wall_window_closed");
            kit.wallHalf = Model("wall_half");
            kit.pillar = Model("pillar");
            kit.bigPillar = Model("pillar_decorated");
            kit.gate = Model("barrier");

            kit.torch = Model("torch_mounted");
            kit.banners = Models("banner_patternA_red", "banner_patternB_blue", "banner_shield_red", "banner_thin_yellow");
            kit.bossBanner = Model("banner_triple_red");
            kit.props = Models("barrel_large", "barrel_small_stack", "crates_stacked", "box_stacked", "keg_decorated",
                "rubble_half", "table_medium_broken", "barrel_large_decorated");
            kit.wallDecor = Models("sword_shield", "shelf_small_candles");
            kit.chest = Model("chest");
            kit.bossTreasure = Model("chest_gold");
            kit.candles = Model("candle_triple");

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"Kit de donjon : échelle {kit.scale:0.###}, segment {kit.segment:0.00} m, mur {kit.wallHeight:0.00} m, dalle {kit.floorTileSize:0.00} m");
            return kit;
        }

        static GameObject Model(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon + name + ".fbx");
            if (model == null) Debug.LogWarning("Kit de donjon : modèle introuvable " + name);
            return model;
        }

        static GameObject[] Models(params string[] names) => names.Select(Model).Where(m => m != null).ToArray();

        static Bounds Measure(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return new Bounds(Vector3.zero, Vector3.one);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds bounds = EnvironmentPlacer.CombinedBounds(go);
            Object.DestroyImmediate(go);
            return bounds;
        }
    }
}
