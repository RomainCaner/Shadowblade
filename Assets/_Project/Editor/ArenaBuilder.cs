using ShadowBlade.Combat;
using ShadowBlade.Enemies;
using ShadowBlade.Level;
using ShadowBlade.Player;
using ShadowBlade.UI;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Construit la scène de test "Arena" : sol, murs, piliers, héros provisoire (capsule),
    /// ennemis en vagues, caméra Cinemachine, HUD et contrôles tactiles.
    /// Menu : ShadowBlade > Construire l'arène de test.
    /// Relancer l'outil écrase la scène Arena et le prefab Enemy existants.
    /// </summary>
    public static class ArenaBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string MaterialsFolder = "Assets/_Project/Materials";
        const string PrefabsFolder = "Assets/_Project/Prefabs";

        [MenuItem("ShadowBlade/Construire l'arène de test")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Point d'entrée pour Unity en ligne de commande (-executeMethod).</summary>
        public static void BuildFromCommandLine() => Build();

        static void Build()
        {
            ConfigurePlayerSettings();
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material ground = CreateMaterial("Ground", new Color(0.22f, 0.22f, 0.25f));
            Material stone = CreateMaterial("Stone", new Color(0.45f, 0.42f, 0.4f));
            Material hero = CreateMaterial("Hero", new Color(0.2f, 0.45f, 0.85f));
            Material steel = CreateMaterial("Steel", new Color(0.85f, 0.85f, 0.9f), metallic: 0.8f);
            Material enemy = CreateMaterial("Enemy", new Color(0.55f, 0.12f, 0.12f));
            Material glow = CreateMaterial("EnemyEyes", new Color(1f, 0.75f, 0.2f));
            Material telegraph = CreateMaterial("Telegraph", new Color(1f, 0.45f, 0.1f));

            BuildLevel(ground, stone);
            GameObject player = BuildPlayer(hero, steel);
            BuildCamera(player.transform);
            EnemyAI enemyPrefab = BuildEnemyPrefab(enemy, glow, telegraph);
            Canvas ui = BuildUI(player.GetComponent<PlayerMovement>(), out Text waveText, out Text messageText);
            BuildGameController(enemyPrefab, player.GetComponent<Health>(), waveText, messageText);
            BuildPlayerHealthBar(ui.transform, player.GetComponent<Health>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("ARENA_BUILT " + ScenePath);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "RomainCaner";
            PlayerSettings.productName = "ShadowBlade";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.romaincaner.shadowblade");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        }

        // ---------------------------------------------------------------- Niveau

        static void BuildLevel(Material ground, Material stone)
        {
            // Tout le décor est sous "Level" : le NavMesh y est calculé au lancement.
            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            level.AddComponent<RuntimeNavMeshBaker>();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(level.transform);
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = ground;
            floor.isStatic = true;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(level.transform);
            CreateBlock("Wall_N", new Vector3(0f, 1.5f, 25f), new Vector3(50f, 3f, 1f), stone, walls);
            CreateBlock("Wall_S", new Vector3(0f, 1.5f, -25f), new Vector3(50f, 3f, 1f), stone, walls);
            CreateBlock("Wall_E", new Vector3(25f, 1.5f, 0f), new Vector3(1f, 3f, 50f), stone, walls);
            CreateBlock("Wall_W", new Vector3(-25f, 1.5f, 0f), new Vector3(1f, 3f, 50f), stone, walls);

            var props = new GameObject("Props").transform;
            props.SetParent(level.transform);
            Vector3[] pillars =
            {
                new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, 8f),
                new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f),
            };
            for (int i = 0; i < pillars.Length; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar_" + (i + 1);
                pillar.transform.SetParent(props);
                pillar.transform.position = pillars[i] + Vector3.up * 2f;
                pillar.transform.localScale = new Vector3(1.5f, 2f, 1.5f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
                pillar.isStatic = true;
            }
            CreateBlock("Crate_1", new Vector3(14f, 0.75f, -3f), new Vector3(1.5f, 1.5f, 1.5f), stone, props);
            CreateBlock("Crate_2", new Vector3(-15f, 0.75f, 2f), new Vector3(1.5f, 1.5f, 1.5f), stone, props);
            CreateBlock("LowWall", new Vector3(0f, 0.5f, 15f), new Vector3(8f, 1f, 1f), stone, props);

            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                sun.shadows = LightShadows.Soft;
            }
        }

        // ---------------------------------------------------------------- Héros

        static GameObject BuildPlayer(Material hero, Material steel)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 0.1f, 0f);

            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;

            player.AddComponent<Health>();
            player.AddComponent<PlayerInputReader>();
            var movement = player.AddComponent<PlayerMovement>();
            var combat = player.AddComponent<PlayerCombat>();
            var flash = player.AddComponent<HitFlash>();
            SetSerialized(flash, "flashColor", new Color(1f, 0.2f, 0.2f));

            // Le pivot du visuel est au centre du corps pour que la roulade tourne sur elle-même.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);

            GameObject body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body", visual, hero);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            // Petit "visage" pour voir dans quelle direction regarde le héros.
            GameObject face = CreateVisualPrimitive(PrimitiveType.Cube, "Face", visual, steel);
            face.transform.localPosition = new Vector3(0f, 0.5f, 0.35f);
            face.transform.localScale = new Vector3(0.4f, 0.15f, 0.15f);

            // L'épée tourne autour de ce pivot pendant les coups.
            var swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(visual, false);
            swordPivot.localPosition = new Vector3(0f, 0f, 0.1f);

            GameObject sword = CreateVisualPrimitive(PrimitiveType.Cube, "Sword", swordPivot, steel);
            sword.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            sword.transform.localScale = new Vector3(0.08f, 0.08f, 1.4f);

            SetSerialized(movement, "visual", visual);
            SetSerialized(combat, "swordPivot", swordPivot);

            return player;
        }

        static void BuildCamera(Transform target)
        {
            Camera mainCamera = Camera.main;
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

            // Vue plongeante de 3/4, à la Darkness Rises.
            var offset = new Vector3(0f, 9f, -7f);
            var cmObject = new GameObject("Player Camera");
            cmObject.transform.SetPositionAndRotation(
                target.position + offset,
                Quaternion.LookRotation(-offset + Vector3.up * 1f));

            var cmCamera = cmObject.AddComponent<CinemachineCamera>();
            cmCamera.Follow = target;
            LensSettings lens = cmCamera.Lens;
            lens.FieldOfView = 45f;
            cmCamera.Lens = lens;

            var follow = cmObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.4f, 0.4f, 0.4f);
        }

        // ---------------------------------------------------------------- Ennemis

        static EnemyAI BuildEnemyPrefab(Material body, Material eyes, Material telegraphMaterial)
        {
            const float attackRange = 1.8f;

            var root = new GameObject("Enemy");

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.45f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 1.3f;
            agent.radius = 0.45f;
            agent.height = 2f;

            var health = root.AddComponent<Health>();
            SetSerialized(health, "maxHealth", 40f);
            root.AddComponent<HitFlash>();
            var ai = root.AddComponent<EnemyAI>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);

            GameObject bodyObject = CreateVisualPrimitive(PrimitiveType.Capsule, "Body", visual, body);
            bodyObject.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

            GameObject eyesObject = CreateVisualPrimitive(PrimitiveType.Cube, "Eyes", visual, eyes);
            eyesObject.transform.localPosition = new Vector3(0f, 0.5f, 0.38f);
            eyesObject.transform.localScale = new Vector3(0.5f, 0.12f, 0.12f);

            // Cercle d'avertissement au sol, affiché juste avant que l'ennemi frappe.
            GameObject telegraph = CreateVisualPrimitive(PrimitiveType.Cylinder, "Telegraph", root.transform, telegraphMaterial);
            telegraph.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            telegraph.transform.localScale = new Vector3(attackRange * 2f, 0.01f, attackRange * 2f);
            SetSerialized(ai, "telegraph", telegraph.transform);
            SetSerialized(ai, "attackRange", attackRange);

            BuildEnemyHealthBar(root.transform);

            string path = PrefabsFolder + "/Enemy.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<EnemyAI>();
        }

        static void BuildEnemyHealthBar(Transform parent)
        {
            var barObject = new GameObject("HealthBar", typeof(RectTransform));
            barObject.transform.SetParent(parent, false);
            var rect = (RectTransform)barObject.transform;
            rect.localPosition = new Vector3(0f, 2.5f, 0f);
            rect.sizeDelta = new Vector2(100f, 12f);
            rect.localScale = Vector3.one * 0.012f;

            var canvas = barObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            barObject.AddComponent<Billboard>();

            RectTransform fill = CreateBar(barObject.transform, new Color(0.9f, 0.2f, 0.15f));
            var bar = barObject.AddComponent<HealthBar>();
            SetSerialized(bar, "fill", fill);
            SetSerialized(bar, "hideWhenFull", true);
        }

        static void BuildGameController(EnemyAI enemyPrefab, Health player, Text waveText, Text messageText)
        {
            var spawnRoot = new GameObject("SpawnPoints").transform;
            Vector3[] positions =
            {
                new Vector3(-18f, 0f, 18f), new Vector3(18f, 0f, 18f),
                new Vector3(18f, 0f, -18f), new Vector3(-18f, 0f, -18f),
            };
            var points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                points[i] = new GameObject("Spawn_" + (i + 1)).transform;
                points[i].SetParent(spawnRoot);
                points[i].position = positions[i];
            }

            var controllerObject = new GameObject("ArenaGame");
            var controller = controllerObject.AddComponent<ArenaGameController>();
            var so = new SerializedObject(controller);
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("waveText").objectReferenceValue = waveText;
            so.FindProperty("messageText").objectReferenceValue = messageText;
            SerializedProperty spawnPoints = so.FindProperty("spawnPoints");
            spawnPoints.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
                spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Interface

        static Canvas BuildUI(PlayerMovement player, out Text waveText, out Text messageText)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var canvasObject = new GameObject("UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Joystick : en bas à gauche.
            RectTransform stickBase = CreateImage("Joystick", canvasObject.transform, knob,
                new Color(1f, 1f, 1f, 0.2f), new Vector2(0f, 0f), new Vector2(280f, 260f), Vector2.one * 320f);
            RectTransform handle = CreateImage("Handle", stickBase, knob,
                new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 140f);
            var stick = handle.gameObject.AddComponent<OnScreenStick>();
            SetSerialized(stick, "m_ControlPath", "<Gamepad>/leftStick");
            SetSerialized(stick, "m_MovementRange", 110f);

            // Bouton d'attaque (gros) et d'esquive : en bas à droite.
            RectTransform attack = CreateButton("AttackButton", "Attaque", canvasObject.transform, knob, font,
                new Color(0.85f, 0.25f, 0.2f, 0.55f), new Vector2(-260f, 230f), 240f, "<Gamepad>/buttonWest");
            RectTransform dodge = CreateButton("DodgeButton", "Esquive", canvasObject.transform, knob, font,
                new Color(0.25f, 0.6f, 0.95f, 0.55f), new Vector2(-520f, 150f), 170f, "<Gamepad>/buttonEast");

            RectTransform cooldown = CreateImage("Cooldown", dodge, knob,
                new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 170f);
            Image cooldownImage = cooldown.GetComponent<Image>();
            cooldownImage.type = Image.Type.Filled;
            cooldownImage.fillMethod = Image.FillMethod.Radial360;
            cooldownImage.fillOrigin = (int)Image.Origin360.Top;
            cooldownImage.fillAmount = 0f;
            cooldownImage.raycastTarget = false;
            var overlay = cooldown.gameObject.AddComponent<DodgeCooldownOverlay>();
            SetSerialized(overlay, "player", player);

            attack.SetAsLastSibling();

            waveText = CreateText("WaveText", canvasObject.transform, font, 38,
                new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(900f, 70f));
            messageText = CreateText("Message", canvasObject.transform, font, 90,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 300f));

            return canvas;
        }

        static void BuildPlayerHealthBar(Transform canvas, Health player)
        {
            var root = new GameObject("PlayerHealth", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -40f);
            rect.sizeDelta = new Vector2(460f, 34f);

            RectTransform fill = CreateBar(root.transform, new Color(0.3f, 0.85f, 0.35f));
            var bar = root.AddComponent<HealthBar>();
            SetSerialized(bar, "health", player);
            SetSerialized(bar, "fill", fill);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text label = CreateText("Label", root.transform, font, 24,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 34f));
            label.text = "PV";
        }

        /// <summary>Fond sombre + remplissage qui s'étire de gauche à droite. Renvoie le remplissage.</summary>
        static RectTransform CreateBar(Transform parent, Color fillColor)
        {
            RectTransform background = CreateStretched("Background", parent);
            background.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform fill = CreateStretched("Fill", parent);
            fill.gameObject.AddComponent<Image>().color = fillColor;
            return fill;
        }

        static RectTransform CreateStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Text CreateText(string name, Transform parent, Font font, int fontSize,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        static RectTransform CreateButton(string name, string label, Transform parent, Sprite sprite, Font font,
            Color color, Vector2 position, float size, string controlPath)
        {
            RectTransform button = CreateImage(name, parent, sprite, color, new Vector2(1f, 0f), position, Vector2.one * size);
            var onScreen = button.gameObject.AddComponent<OnScreenButton>();
            SetSerialized(onScreen, "m_ControlPath", controlPath);

            Text text = CreateText("Label", button, font, Mathf.RoundToInt(size * 0.17f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
            text.text = label;
            return button;
        }

        static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return rect;
        }

        // ---------------------------------------------------------------- Utilitaires

        static void SetSerialized(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        static GameObject CreateVisualPrimitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // seul le collider racine gère les collisions
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Material CreateMaterial(string name, Color color, float metallic = 0f)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", metallic > 0f ? 0.7f : 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
