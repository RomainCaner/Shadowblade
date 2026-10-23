using ShadowBlade.Player;
using ShadowBlade.UI;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Construit la scène de test "Arena" : sol, murs, piliers, héros provisoire (capsule),
    /// caméra Cinemachine et contrôles tactiles. Menu : ShadowBlade > Construire l'arène de test.
    /// Relancer l'outil écrase la scène Arena existante.
    /// </summary>
    public static class ArenaBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string MaterialsFolder = "Assets/_Project/Materials";

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

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material ground = CreateMaterial("Ground", new Color(0.22f, 0.22f, 0.25f));
            Material stone = CreateMaterial("Stone", new Color(0.45f, 0.42f, 0.4f));
            Material hero = CreateMaterial("Hero", new Color(0.2f, 0.45f, 0.85f));
            Material steel = CreateMaterial("Steel", new Color(0.85f, 0.85f, 0.9f), metallic: 0.8f);

            BuildLevel(ground, stone);
            GameObject player = BuildPlayer(hero, steel);
            BuildCamera(player.transform);
            BuildTouchControls(player.GetComponent<PlayerMovement>());

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

        static void BuildLevel(Material ground, Material stone)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = ground;

            var walls = new GameObject("Walls").transform;
            CreateBlock("Wall_N", new Vector3(0f, 1.5f, 25f), new Vector3(50f, 3f, 1f), stone, walls);
            CreateBlock("Wall_S", new Vector3(0f, 1.5f, -25f), new Vector3(50f, 3f, 1f), stone, walls);
            CreateBlock("Wall_E", new Vector3(25f, 1.5f, 0f), new Vector3(1f, 3f, 50f), stone, walls);
            CreateBlock("Wall_W", new Vector3(-25f, 1.5f, 0f), new Vector3(1f, 3f, 50f), stone, walls);

            var props = new GameObject("Props").transform;
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

        static GameObject BuildPlayer(Material hero, Material steel)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 0.1f, 0f);

            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;

            player.AddComponent<PlayerInputReader>();
            var movement = player.AddComponent<PlayerMovement>();

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

            GameObject sword = CreateVisualPrimitive(PrimitiveType.Cube, "Sword", visual, steel);
            sword.transform.localPosition = new Vector3(0.55f, 0f, 0.6f);
            sword.transform.localScale = new Vector3(0.08f, 0.08f, 1.3f);

            var so = new SerializedObject(movement);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();

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

        static void BuildTouchControls(PlayerMovement player)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var canvasObject = new GameObject("TouchControls");
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
                new Color(1f, 1f, 1f, 0.2f), new Vector2(0f, 0f), new Vector2(280f, 260f), 320f);
            RectTransform handle = CreateImage("Handle", stickBase, knob,
                new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, 140f);
            var stick = handle.gameObject.AddComponent<OnScreenStick>();
            SetSerialized(stick, "m_ControlPath", "<Gamepad>/leftStick");
            SetSerialized(stick, "m_MovementRange", 110f);

            // Bouton d'attaque (gros) et d'esquive : en bas à droite.
            RectTransform attack = CreateButton("AttackButton", "Attaque", canvasObject.transform, knob, font,
                new Color(0.85f, 0.25f, 0.2f, 0.55f), new Vector2(-260f, 230f), 240f, "<Gamepad>/buttonWest");
            RectTransform dodge = CreateButton("DodgeButton", "Esquive", canvasObject.transform, knob, font,
                new Color(0.25f, 0.6f, 0.95f, 0.55f), new Vector2(-520f, 150f), 170f, "<Gamepad>/buttonEast");

            RectTransform cooldown = CreateImage("Cooldown", dodge, knob,
                new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, 170f);
            Image cooldownImage = cooldown.GetComponent<Image>();
            cooldownImage.type = Image.Type.Filled;
            cooldownImage.fillMethod = Image.FillMethod.Radial360;
            cooldownImage.fillOrigin = (int)Image.Origin360.Top;
            cooldownImage.fillAmount = 0f;
            cooldownImage.raycastTarget = false;
            var overlay = cooldown.gameObject.AddComponent<DodgeCooldownOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("player").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();

            attack.SetAsLastSibling();
        }

        static RectTransform CreateButton(string name, string label, Transform parent, Sprite sprite, Font font,
            Color color, Vector2 position, float size, string controlPath)
        {
            RectTransform button = CreateImage(name, parent, sprite, color, new Vector2(1f, 0f), position, size);
            var onScreen = button.gameObject.AddComponent<OnScreenButton>();
            SetSerialized(onScreen, "m_ControlPath", controlPath);

            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(button, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.AddComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size * 0.17f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }

        static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchor, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return rect;
        }

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
            Object.DestroyImmediate(go.GetComponent<Collider>()); // seul le CharacterController gère les collisions
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Material CreateMaterial(string name, Color color, float metallic = 0f)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Materials");

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
    }
}
