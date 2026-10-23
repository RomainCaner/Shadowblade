using System.Collections.Generic;
using System.Linq;
using ShadowBlade.Core;
using UnityEngine;
using static ShadowBlade.Level.EnvironmentPlacer;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Génère le donjon d'une mission au lancement de la scène, à partir de StoryCatalog :
    /// salle d'entrée, salles de combat reliées par des couloirs (vers le nord, parfois à l'est ou à l'ouest),
    /// grilles aux portes, décor (torches, bannières, piliers, tonneaux) et salle du boss.
    /// Le même plan est produit à chaque partie d'une mission (graine fixe).
    /// S'exécute avant le calcul du NavMesh (RuntimeNavMeshBaker).
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class DungeonGenerator : MonoBehaviour
    {
        [SerializeField] DungeonKit kit;
        [SerializeField] Material outerGroundMaterial;
        [SerializeField] int corridorLength = 3;
        [SerializeField] int entrySize = 5;
        [SerializeField] int bossSize = 9;

        public StageDefinition Stage { get; private set; }
        public List<DungeonRoom> Rooms { get; } = new List<DungeonRoom>();
        public Bounds Bounds { get; private set; }
        public Vector3 PlayerStart => Rooms[0].Center + Vector3.up * 0.1f;

        System.Random random;
        float S;   // longueur d'un segment de mur
        float k;   // échelle des modèles
        readonly List<Rect> occupied = new List<Rect>();
        bool gateAlongX;

        static readonly Vector3[] Sides = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        void Awake()
        {
            // Scène lancée directement depuis l'éditeur : on prend la mission du boss pour tout voir.
            Stage = GameSession.Stage ?? StoryCatalog.Chapters[0].Stages.Last();
            Generate(Stage);
        }

        // ---------------------------------------------------------------- Plan

        void Generate(StageDefinition stage)
        {
            random = new System.Random(stage.Seed);
            S = kit.segment;
            k = kit.scale;
            gateAlongX = MeasureAlongX(kit.gate);

            var layout = new List<(RoomDefinition def, int size, bool boss)> { (null, entrySize, false) };
            layout.AddRange(stage.Rooms.Select(r => (r, r.Size, false)));
            if (stage.HasBoss) layout.Add((null, bossSize, true));

            Vector3 previousDirection = Vector3.forward;
            for (int i = 0; i < layout.Count; i++)
            {
                var (def, size, boss) = layout[i];
                var room = new DungeonRoom
                {
                    Index = i,
                    Definition = def,
                    IsEntry = i == 0,
                    IsBoss = boss,
                    HalfSize = Vector2.one * (size * S / 2f),
                };

                if (i == 0)
                {
                    room.Center = Vector3.zero;
                }
                else
                {
                    DungeonRoom previous = Rooms[i - 1];
                    Vector3 direction = ChooseDirection(previous, room.HalfSize, previousDirection, boss);
                    room.Center = previous.Center + direction * (HalfAlong(previous, direction) + corridorLength * S + room.HalfSize.x);
                    previous.ExitDirection = direction;
                    room.EntryDirection = -direction;
                    previousDirection = direction;
                }

                occupied.Add(RectOf(room, 0f));
                Rooms.Add(room);
            }

            foreach (DungeonRoom room in Rooms) BuildRoom(room);
            for (int i = 1; i < Rooms.Count; i++)
            {
                DungeonRoom previous = Rooms[i - 1];
                BuildCorridor(previous.EdgePoint(previous.ExitDirection), previous.ExitDirection);
            }

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (DungeonRoom room in Rooms)
                bounds.Encapsulate(new Bounds(room.Center, new Vector3(room.HalfSize.x * 2f, 4f, room.HalfSize.y * 2f)));
            Bounds = bounds;
            BuildOuterGround();
        }

        Vector3 ChooseDirection(DungeonRoom previous, Vector2 newHalf, Vector3 lastDirection, bool boss)
        {
            var candidates = new List<Vector3>();
            if (!boss)
            {
                // Le nord est deux fois plus probable ; jamais de demi-tour direct est/ouest.
                if (lastDirection != Vector3.left) candidates.Add(Vector3.right);
                if (lastDirection != Vector3.right) candidates.Add(Vector3.left);
                candidates = candidates.OrderBy(_ => random.Next()).ToList();
                candidates.Insert(random.Next(2) == 0 ? 0 : candidates.Count, Vector3.forward);
                if (random.Next(3) > 0) { candidates.Remove(Vector3.forward); candidates.Insert(0, Vector3.forward); }
            }
            candidates.Add(Vector3.forward);

            foreach (Vector3 direction in candidates)
            {
                Vector3 center = previous.Center + direction * (HalfAlong(previous, direction) + corridorLength * S + newHalf.x);
                var rect = new Rect(center.x - newHalf.x - S, center.z - newHalf.y - S, (newHalf.x + S) * 2f, (newHalf.y + S) * 2f);
                if (!occupied.Any(o => o.Overlaps(rect))) return direction;
            }
            return Vector3.forward;
        }

        static float HalfAlong(DungeonRoom room, Vector3 direction) =>
            Mathf.Abs(direction.x) > 0.5f ? room.HalfSize.x : room.HalfSize.y;

        static Rect RectOf(DungeonRoom room, float margin) =>
            new Rect(room.Center.x - room.HalfSize.x - margin, room.Center.z - room.HalfSize.y - margin,
                (room.HalfSize.x + margin) * 2f, (room.HalfSize.y + margin) * 2f);

        // ---------------------------------------------------------------- Salles

        void BuildRoom(DungeonRoom room)
        {
            var parent = new GameObject(room.IsBoss ? "BossRoom" : room.IsEntry ? "EntryRoom" : "Room_" + room.Index).transform;
            parent.SetParent(transform, false);
            Vector3 c = room.Center;
            float hx = room.HalfSize.x, hz = room.HalfSize.y;

            // Sol.
            AddBox(parent, c - Vector3.up * 0.5f, new Vector3(hx * 2f, 1f, hz * 2f), "Floor");
            TileFloor(parent, c, hx, hz);

            // Murs, avec une ouverture au milieu des côtés reliés à un couloir.
            foreach (Vector3 normal in Sides)
            {
                bool opening = normal == room.EntryDirection || normal == room.ExitDirection;
                BuildWallLine(parent, room.EdgePoint(normal), normal, Mathf.RoundToInt(2f * HalfAlongPerp(room, normal) / S), opening, room.IsBoss);
                if (opening) AddGate(room, parent, room.EdgePoint(normal), normal);
            }

            // Gros piliers dans les deux coins du fond (ceux du côté caméra cacheraient l'action).
            foreach (var corner in new[] { new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                Place(kit.bigPillar, c + new Vector3(corner.x * hx, 0f, corner.z * hz), 0f, k * 1.2f, parent, kit.material);

            DecorateWalls(parent, room);

            if (!room.IsEntry)
            {
                float p = room.IsBoss ? 0.5f : 0.45f;
                GameObject pillarPrefab = room.IsBoss ? kit.bigPillar : kit.pillar;
                foreach (var q in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
                {
                    GameObject pillar = Place(pillarPrefab, c + new Vector3(q.x * hx * p, 0f, q.z * hz * p), 0f, k * (room.IsBoss ? 1.3f : 1f), parent, kit.material);
                    AddCollider(parent, pillar);
                }
                ScatterProps(parent, room);
            }

            float inset = 2.5f;
            room.SpawnPoints = new[]
            {
                c + new Vector3(-hx + inset, 0f, hz - inset), c + new Vector3(hx - inset, 0f, hz - inset),
                c + new Vector3(hx - inset, 0f, -hz + inset), c + new Vector3(-hx + inset, 0f, -hz + inset),
                c + (room.ExitDirection != Vector3.zero ? room.ExitDirection : Vector3.forward) * (hz - inset),
            };
            room.TreasurePoint = c + Vector3.Cross(Vector3.up, room.EntryDirection == Vector3.zero ? Vector3.forward : room.EntryDirection) * (hx * 0.6f);

            if (room.IsBoss) DecorateBossRoom(parent, room);
        }

        static float HalfAlongPerp(DungeonRoom room, Vector3 normal) =>
            Mathf.Abs(normal.z) > 0.5f ? room.HalfSize.x : room.HalfSize.y;

        void TileFloor(Transform parent, Vector3 center, float hx, float hz)
        {
            float t = kit.floorTileSize;
            int nx = Mathf.CeilToInt(hx * 2f / t), nz = Mathf.CeilToInt(hz * 2f / t);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var position = center + new Vector3(-hx + t * (x + 0.5f), 0f, -hz + t * (z + 0.5f));
                Place(kit.floorTile, position, 90f * random.Next(4), k, parent, kit.material, Anchor.Top);
            }
        }

        /// <summary>Une rangée de murs le long d'un côté (centre "edge", tournée vers "normal").</summary>
        void BuildWallLine(Transform parent, Vector3 edge, Vector3 normal, int segments, bool opening, bool boss)
        {
            Vector3 along = Vector3.Cross(Vector3.up, normal);
            float yaw = WallYaw(normal);
            int openIndex = opening ? segments / 2 : -1;
            bool south = normal == Vector3.back;

            // Côté caméra (sud) : pas de mur visible, seulement la collision, pour ne jamais cacher le combat.
            for (int i = 0; i < segments && !south; i++)
            {
                if (i == openIndex) continue;
                GameObject prefab = boss ? kit.walls[0] : kit.walls[random.Next(kit.walls.Length)];
                Place(prefab, edge + along * (-segments * S / 2f + S * (i + 0.5f)), yaw, k, parent, kit.material);
            }

            // Collisions : une boîte de chaque côté de l'ouverture (ou une seule).
            float length = segments * S;
            Vector3 outward = normal * 0.4f + Vector3.up * 2.5f;
            if (openIndex < 0)
            {
                AddBox(parent, edge + outward, BoxSize(along, length, 5f, 1.2f), "WallCollider");
            }
            else
            {
                float side = (length - S) / 2f;
                foreach (int sign in new[] { -1, 1 })
                    AddBox(parent, edge + outward + along * sign * (S / 2f + side / 2f), BoxSize(along, side, 5f, 1.2f), "WallCollider");
            }
        }

        static Vector3 BoxSize(Vector3 along, float length, float height, float thickness) =>
            Mathf.Abs(along.x) > 0.5f ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length);

        float WallYaw(Vector3 normal)
        {
            Vector3 along = Vector3.Cross(Vector3.up, normal);
            bool sideAlongX = Mathf.Abs(along.x) > 0.5f;
            float yaw = sideAlongX == kit.wallAlongX ? 0f : 90f;
            if (normal == Vector3.back || normal == Vector3.left) yaw += 180f;
            return yaw;
        }

        void AddGate(DungeonRoom room, Transform parent, Vector3 edge, Vector3 normal)
        {
            var gateObject = new GameObject("Gate");
            gateObject.transform.SetParent(parent, false);
            gateObject.transform.position = edge;

            Vector3 along = Vector3.Cross(Vector3.up, normal);
            bool openingAlongX = Mathf.Abs(along.x) > 0.5f;
            float yaw = openingAlongX == gateAlongX ? 0f : 90f;

            GameObject visual = Place(kit.gate, edge, yaw, k, gateObject.transform, kit.material);
            if (visual != null)
            {
                // Étire la grille pour boucher exactement l'ouverture.
                Bounds b = CombinedBounds(visual);
                float width = openingAlongX ? b.size.x : b.size.z;
                if (width > 0.01f)
                {
                    Vector3 scale = visual.transform.localScale;
                    if (Mathf.Approximately(Mathf.Repeat(yaw, 180f), 0f)) scale.x *= S / width;
                    else scale.z *= S / width;
                    visual.transform.localScale = scale;
                    Bounds after = CombinedBounds(visual);
                    visual.transform.position += new Vector3(edge.x - after.center.x, 0f, edge.z - after.center.z);
                }
            }

            BoxCollider blocker = AddBox(gateObject.transform, edge + Vector3.up * 2f, BoxSize(along, S, 4f, 0.6f), "GateBlocker");
            var gate = gateObject.AddComponent<DungeonGate>();
            gate.Setup(visual != null ? visual.transform : gateObject.transform, blocker, kit.wallHeight + 0.5f);
            room.Gates.Add(gate);
        }

        // ---------------------------------------------------------------- Couloirs

        void BuildCorridor(Vector3 start, Vector3 direction)
        {
            var parent = new GameObject("Corridor").transform;
            parent.SetParent(transform, false);
            float length = corridorLength * S;
            Vector3 center = start + direction * (length / 2f);
            Vector3 perpendicular = Vector3.Cross(Vector3.up, direction);

            AddBox(parent, center - Vector3.up * 0.5f, BoxSize(direction, length, 1f, S + 0.2f), "Floor");
            for (int i = 0; i < corridorLength; i++)
            {
                Vector3 tile = start + direction * (S * (i + 0.5f));
                Place(kit.floorTile, tile, 90f * random.Next(4), k, parent, kit.material, Anchor.Top);
            }

            foreach (int sign in new[] { -1, 1 })
            {
                Vector3 normal = perpendicular * sign;
                Vector3 edge = center + normal * (S / 2f);
                bool south = normal == Vector3.back;
                for (int i = 0; i < corridorLength && !south; i++)
                    Place(kit.walls[0], edge + direction * (-length / 2f + S * (i + 0.5f)), WallYaw(normal), k, parent, kit.material);
                AddBox(parent, edge + normal * 0.4f + Vector3.up * 2.5f, BoxSize(direction, length, 5f, 1.2f), "WallCollider");
            }

            // Une torche dans chaque couloir (côté non-sud).
            Vector3 torchSide = perpendicular == Vector3.back ? -perpendicular : perpendicular;
            AddTorch(parent, center + torchSide * (S / 2f - 0.1f), torchSide);
        }

        // ---------------------------------------------------------------- Décor

        void DecorateWalls(Transform parent, DungeonRoom room)
        {
            foreach (Vector3 normal in Sides)
            {
                if (normal == Vector3.back) continue; // le côté caméra reste dégagé
                bool opening = normal == room.EntryDirection || normal == room.ExitDirection;
                float half = HalfAlongPerp(room, normal);
                Vector3 along = Vector3.Cross(Vector3.up, normal);
                Vector3 face = room.EdgePoint(normal) - normal * 0.15f;

                foreach (float t in new[] { -0.5f, 0.5f })
                    AddTorch(parent, face + along * (half * t), normal);

                if (!opening && !room.IsBoss && kit.banners.Length > 0)
                {
                    GameObject banner = kit.banners[random.Next(kit.banners.Length)];
                    Place(banner, face + along * (half * 0.2f) + Vector3.up * (kit.wallHeight * 0.3f),
                        Quaternion.LookRotation(-normal).eulerAngles.y, k, parent, kit.material);
                }
                if (!opening && kit.wallDecor.Length > 0 && random.Next(2) == 0)
                {
                    GameObject decor = kit.wallDecor[random.Next(kit.wallDecor.Length)];
                    Place(decor, face + along * (-half * 0.2f) + Vector3.up * (kit.wallHeight * 0.35f),
                        Quaternion.LookRotation(-normal).eulerAngles.y, k, parent, kit.material);
                }
            }
        }

        void AddTorch(Transform parent, Vector3 face, Vector3 wallNormal)
        {
            GameObject torch = Place(kit.torch, face + Vector3.up * (kit.wallHeight * 0.5f),
                Quaternion.LookRotation(-wallNormal).eulerAngles.y, k, parent, kit.material);
            if (torch == null) return;
            Bounds b = CombinedBounds(torch);
            var lightObject = new GameObject("TorchLight");
            lightObject.transform.SetParent(torch.transform, true);
            lightObject.transform.position = new Vector3(b.center.x, b.max.y + 0.2f, b.center.z) - wallNormal * 0.3f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.intensity = 2.5f;
            light.range = 7f;
            light.shadows = LightShadows.None;
        }

        void ScatterProps(Transform parent, DungeonRoom room)
        {
            if (kit.props.Length == 0) return;
            Vector3 c = room.Center;
            float hx = room.HalfSize.x - 1.6f, hz = room.HalfSize.y - 1.6f;
            var spots = new List<Vector3>
            {
                new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, -hz * 0.4f), new Vector3(hx, 0, -hz * 0.4f),
                new Vector3(-hx, 0, hz * 0.3f), new Vector3(hx, 0, hz * 0.3f),
            };
            int count = 2 + random.Next(3);
            foreach (Vector3 spot in spots.OrderBy(_ => random.Next()).Take(count))
            {
                GameObject prop = Place(kit.props[random.Next(kit.props.Length)], c + spot, random.Next(4) * 90f + random.Next(-20, 20), k, parent, kit.material);
                AddCollider(parent, prop);
            }
        }

        void DecorateBossRoom(Transform parent, DungeonRoom room)
        {
            Vector3 north = room.ExitDirection != Vector3.zero ? room.ExitDirection : -room.EntryDirection;
            if (north == Vector3.zero) north = Vector3.forward;
            Vector3 along = Vector3.Cross(Vector3.up, north);
            float half = HalfAlongPerp(room, north);
            Vector3 face = room.EdgePoint(north) - north * 0.15f;
            float yaw = Quaternion.LookRotation(-north).eulerAngles.y;

            // Bannières du boss sur les trois murs du fond.
            foreach (Vector3 normal in new[] { north, along, -along })
            {
                if (normal == Vector3.back) continue;
                float h = HalfAlongPerp(room, normal);
                Vector3 wallAlong = Vector3.Cross(Vector3.up, normal);
                Vector3 wallFace = room.EdgePoint(normal) - normal * 0.15f;
                foreach (float t in new[] { -0.75f, -0.25f, 0.25f, 0.75f })
                    Place(kit.bossBanner, wallFace + wallAlong * (h * t) + Vector3.up * (kit.wallHeight * 0.25f),
                        Quaternion.LookRotation(-normal).eulerAngles.y, k, parent, kit.material);
            }

            // "Trône" : trésor doré entouré de bougies, au fond de la salle.
            Vector3 throne = room.Center + north * (HalfAlong(room, north) - 2f);
            AddCollider(parent, Place(kit.bossTreasure, throne, yaw + 180f, k * 1.3f, parent, kit.material));
            Place(kit.candles, throne + along * 1.6f, 0f, k, parent, kit.material);
            Place(kit.candles, throne - along * 1.6f, 0f, k, parent, kit.material);

            // Lueurs rouges.
            foreach (int sign in new[] { -1, 1 })
            {
                var glow = new GameObject("BossGlow").AddComponent<Light>();
                glow.transform.SetParent(parent, false);
                glow.transform.position = room.Center + along * sign * half * 0.5f + Vector3.up * 3f;
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.15f, 0.1f);
                glow.intensity = 4f;
                glow.range = 12f;
            }
        }

        void BuildOuterGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "OuterGround";
            DestroyImmediate(ground.GetComponent<Collider>()); // immédiat : le NavMesh est calculé juste après
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(Bounds.center.x, -0.05f, Bounds.center.z);
            ground.transform.localScale = new Vector3(Bounds.size.x / 10f + 8f, 1f, Bounds.size.z / 10f + 8f);
            if (outerGroundMaterial != null) ground.GetComponent<Renderer>().sharedMaterial = outerGroundMaterial;
        }

        /// <summary>Fait apparaître le coffre au trésor d'une salle nettoyée.</summary>
        public TreasureChest SpawnChest(DungeonRoom room, Transform player, int gold)
        {
            GameObject model = Place(kit.chest, room.TreasurePoint, Quaternion.LookRotation(room.Center - room.TreasurePoint).eulerAngles.y,
                k * 1.2f, transform, kit.material);
            if (model == null) return null;
            var chest = model.AddComponent<TreasureChest>();
            chest.Player = player;
            chest.Gold = gold;
            return chest;
        }

        // ---------------------------------------------------------------- Utilitaires

        static void AddCollider(Transform parent, GameObject model)
        {
            if (model == null) return;
            Bounds b = CombinedBounds(model);
            AddBox(parent, b.center, b.size, model.name + "_Collider");
        }

        bool MeasureAlongX(GameObject prefab)
        {
            if (prefab == null) return true;
            GameObject probe = Place(prefab, Vector3.zero, 0f, 1f, null, null);
            Bounds b = CombinedBounds(probe);
            DestroyImmediate(probe);
            return b.size.x >= b.size.z;
        }
    }
}
