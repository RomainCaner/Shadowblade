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
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ShadowBlade.EditorTools.BuilderUtility;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Construit la scène de combat "Arena" : décor, héros provisoire, ennemis et boss (prefabs),
    /// caméra Cinemachine, HUD et contrôles tactiles.
    /// Relancer l'outil écrase la scène Arena et les prefabs Enemy / Boss existants.
    /// </summary>
    public static class ArenaBuilder
    {
        class EnemySpec
        {
            public string PrefabName;
            public string DisplayName;
            public float Scale = 1f;
            public float Health = 40f;
            public float Speed = 3.5f;
            public float Damage = 8f;
            public float AttackRange = 1.8f;
            public float Windup = 0.7f;
            public int Gold = 5;
            public bool SuperArmor;
            public int HeavyEvery;
            public float HeavyRadius = 4f;
            public float HeavyDamage = 25f;
            public float HeavyWindup = 1.2f;
            public bool WorldHealthBar = true;
            public bool Horns;
            public Material Body;
            public Material Eyes;
            public Material Telegraph;
        }

        [MenuItem("ShadowBlade/Construire l'arène de test", priority = 20)]
        public static void BuildFromMenu()
        {
            if (CanBuildNow()) BuildScene();
        }

        /// <summary>Point d'entrée pour Unity en ligne de commande (-executeMethod).</summary>
        public static void BuildFromCommandLine() => BuildScene();

        public static void BuildScene()
        {
            ConfigurePlayerSettings();
            EnsureFolder(PrefabsFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material ground = CreateMaterial("Ground", new Color(0.22f, 0.22f, 0.25f));
            Material stone = CreateMaterial("Stone", new Color(0.45f, 0.42f, 0.4f));
            Material hero = CreateMaterial("Hero", new Color(0.2f, 0.45f, 0.85f));
            Material steel = CreateMaterial("Steel", new Color(0.85f, 0.85f, 0.9f), metallic: 0.8f);
            Material shockwave = CreateMaterial("Shockwave", new Color(1f, 0.85f, 0.45f));
            Material telegraph = CreateMaterial("Telegraph", new Color(1f, 0.45f, 0.1f));
            Material fx = CreateFxMaterial();

            ParticleSystem hitSpark = CreateBurstPrefab("FX_HitSpark", fx, 14, new Vector2(4f, 9f), new Vector2(0.12f, 0.3f),
                new Vector2(0.08f, 0.18f), new Color(1f, 0.9f, 0.5f), new Color(1f, 0.55f, 0.15f), 1.5f, stretched: true);
            ParticleSystem deathBurst = CreateBurstPrefab("FX_DeathBurst", fx, 30, new Vector2(2f, 6f), new Vector2(0.4f, 0.8f),
                new Vector2(0.25f, 0.5f), new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.05f, 0.1f), -0.3f, stretched: false);

            BuildLevel(ground, stone);
            GameObject player = BuildPlayer(hero, steel, shockwave, fx);
            CinemachineImpulseSource impulse = BuildCamera(player.transform);

            EnemyAI enemyPrefab = BuildEnemyPrefab(new EnemySpec
            {
                PrefabName = "Enemy",
                DisplayName = "Goule",
                Body = CreateMaterial("Enemy", new Color(0.55f, 0.12f, 0.12f)),
                Eyes = CreateMaterial("EnemyEyes", new Color(1f, 0.75f, 0.2f)),
                Telegraph = telegraph,
            });
            EnemyAI bossPrefab = BuildEnemyPrefab(new EnemySpec
            {
                PrefabName = "Boss",
                DisplayName = "Seigneur des Cendres",
                Scale = 1.8f,
                Health = 400f,
                Speed = 2.6f,
                Damage = 15f,
                AttackRange = 2.8f,
                Windup = 0.8f,
                Gold = 100,
                SuperArmor = true,
                HeavyEvery = 3,
                HeavyRadius = 5f,
                HeavyDamage = 30f,
                HeavyWindup = 1.3f,
                WorldHealthBar = false,
                Horns = true,
                Body = CreateMaterial("Boss", new Color(0.2f, 0.1f, 0.24f)),
                Eyes = CreateMaterial("BossEyes", new Color(1f, 0.2f, 0.1f)),
                Telegraph = telegraph,
            });

            BuildHud(player, enemyPrefab, bossPrefab, impulse, hitSpark, deathBurst);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("ARENA_BUILT " + ArenaScenePath);
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

        static GameObject BuildPlayer(Material hero, Material steel, Material shockwave, Material fx)
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
            var skills = player.AddComponent<PlayerSkills>();
            var flash = player.AddComponent<HitFlash>();
            Set(flash, "flashColor", new Color(1f, 0.2f, 0.2f));

            Transform visual = BuildHeroVisual(player.transform, hero, steel, out Transform swordPivot);

            Set(movement, "visual", visual);
            Set(combat, "swordPivot", swordPivot);
            Set(skills, "visual", visual);
            Set(skills, "swordPivot", swordPivot);
            Set(skills, "effectMaterial", shockwave);

            // Traînée lumineuse au bout de l'épée (la lame mesure 1,4 m à partir de 0,25 m du pivot).
            var tip = new GameObject("SwordTip").transform;
            tip.SetParent(swordPivot, false);
            tip.localPosition = new Vector3(0f, 0f, 1.6f);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = fx;
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.45f, 1f, 0f);
            var trailColors = new Gradient();
            trailColors.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailColors;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            var feedback = player.AddComponent<PlayerFeedback>();
            Set(feedback, "swordTrail", trail);
            return player;
        }

        static CinemachineImpulseSource BuildCamera(Transform target)
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

            // Tremblements de caméra : la caméra écoute les "impulsions" émises par CombatFeedback.
            cmObject.AddComponent<CinemachineImpulseListener>();
            return new GameObject("CameraShake").AddComponent<CinemachineImpulseSource>();
        }

        // ---------------------------------------------------------------- Ennemis

        static EnemyAI BuildEnemyPrefab(EnemySpec spec)
        {
            float s = spec.Scale;
            var root = new GameObject(spec.PrefabName);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, s, 0f);
            capsule.height = 2f * s;
            capsule.radius = 0.45f * s;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = spec.Speed;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.stoppingDistance = spec.AttackRange * 0.7f;
            agent.radius = 0.45f * s;
            agent.height = 2f * s;

            var health = root.AddComponent<Health>();
            Set(health, "maxHealth", spec.Health);
            root.AddComponent<HitFlash>();

            var ai = root.AddComponent<EnemyAI>();
            Set(ai, "displayName", spec.DisplayName);
            Set(ai, "goldReward", spec.Gold);
            Set(ai, "attackRange", spec.AttackRange);
            Set(ai, "attackDamage", spec.Damage);
            Set(ai, "windupDuration", spec.Windup);
            Set(ai, "superArmor", spec.SuperArmor);
            Set(ai, "heavyAttackEvery", spec.HeavyEvery);
            Set(ai, "heavyRadius", spec.HeavyRadius);
            Set(ai, "heavyDamage", spec.HeavyDamage);
            Set(ai, "heavyWindup", spec.HeavyWindup);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, s, 0f);
            visual.localScale = Vector3.one * s;

            GameObject body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body", visual, spec.Body);
            body.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

            GameObject eyes = CreateVisualPrimitive(PrimitiveType.Cube, "Eyes", visual, spec.Eyes);
            eyes.transform.localPosition = new Vector3(0f, 0.5f, 0.38f);
            eyes.transform.localScale = new Vector3(0.5f, 0.12f, 0.12f);

            if (spec.Horns)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject horn = CreateVisualPrimitive(PrimitiveType.Cube, "Horn", visual, spec.Eyes);
                    horn.transform.localPosition = new Vector3(0.25f * side, 0.95f, 0.05f);
                    horn.transform.localRotation = Quaternion.Euler(0f, 0f, -25f * side);
                    horn.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
                }
            }

            // Cercle d'avertissement au sol, affiché juste avant que l'ennemi frappe.
            GameObject telegraph = CreateVisualPrimitive(PrimitiveType.Cylinder, "Telegraph", root.transform, spec.Telegraph);
            telegraph.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            telegraph.transform.localScale = new Vector3(spec.AttackRange * 2f, 0.01f, spec.AttackRange * 2f);
            Set(ai, "telegraph", telegraph.transform);

            if (spec.WorldHealthBar) BuildEnemyHealthBar(root.transform, 2f * s + 0.5f);

            string path = $"{PrefabsFolder}/{spec.PrefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<EnemyAI>();
        }

        static void BuildEnemyHealthBar(Transform parent, float height)
        {
            var barObject = new GameObject("HealthBar", typeof(RectTransform));
            barObject.transform.SetParent(parent, false);
            var rect = (RectTransform)barObject.transform;
            rect.localPosition = new Vector3(0f, height, 0f);
            rect.sizeDelta = new Vector2(100f, 12f);
            rect.localScale = Vector3.one * 0.012f;

            var canvas = barObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            barObject.AddComponent<Billboard>();

            RectTransform fill = CreateBar(barObject.transform, new Color(0.9f, 0.2f, 0.15f));
            var bar = barObject.AddComponent<HealthBar>();
            Set(bar, "fill", fill);
            Set(bar, "hideWhenFull", true);
        }

        // ---------------------------------------------------------------- HUD et partie

        static void BuildHud(GameObject player, EnemyAI enemyPrefab, EnemyAI bossPrefab,
            CinemachineImpulseSource impulse, ParticleSystem hitSpark, ParticleSystem deathBurst)
        {
            CreateEventSystem();
            Canvas canvas = CreateCanvas("UI");
            Transform ui = canvas.transform;

            var movement = player.GetComponent<PlayerMovement>();
            var skills = player.GetComponent<PlayerSkills>();
            var health = player.GetComponent<Health>();

            BuildTouchControls(ui, movement, skills);

            // Barre de vie du héros (en haut à gauche).
            RectTransform hp = CreateRect("PlayerHealth", ui, new Vector2(0f, 1f), new Vector2(270f, -57f), new Vector2(460f, 34f));
            RectTransform hpFill = CreateBar(hp, new Color(0.3f, 0.85f, 0.35f));
            var hpBar = hp.gameObject.AddComponent<HealthBar>();
            Set(hpBar, "health", health);
            Set(hpBar, "fill", hpFill);
            CreateLabel(hp, "PV", 24);

            Text waveText = CreateText("WaveText", ui, "", 34, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1100f, 60f));
            Text goldText = CreateText("GoldText", ui, "Or  +0", 32, new Vector2(1f, 1f), new Vector2(-330f, -60f),
                new Vector2(300f, 60f), TextAnchor.MiddleRight, Gold);
            Button pause = CreateButton("PauseButton", ui, "II", new Vector2(1f, 1f), new Vector2(-90f, -60f),
                new Vector2(100f, 90f), new Color(0f, 0f, 0f, 0.5f), 40);

            // Barre du boss (en haut au centre, cachée tant qu'aucun boss n'est là).
            RectTransform bossBar = CreateRect("BossBar", ui, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(900f, 80f));
            Text bossName = CreateText("BossName", bossBar, "Boss", 32, new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(900f, 40f), color: new Color(1f, 0.6f, 0.4f));
            RectTransform bossFillRoot = CreateRect("Bar", bossBar, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(860f, 28f));
            RectTransform bossFill = CreateBar(bossFillRoot, new Color(0.85f, 0.15f, 0.25f));
            var bossHealthBar = bossFillRoot.gameObject.AddComponent<HealthBar>();
            Set(bossHealthBar, "fill", bossFill);

            Text message = CreateText("Message", ui, "", 90, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 300f));

            // Chiffres de dégâts (sous les fenêtres, au-dessus du reste du HUD).
            RectTransform numbersRoot = CreateStretched("DamageNumbers", ui);
            var damageNumbers = numbersRoot.gameObject.AddComponent<DamageNumbers>();
            Set(damageNumbers, "font", BuilderUtility.Font);

            var feedback = new GameObject("CombatFeedback").AddComponent<CombatFeedback>();
            Set(feedback, "damageNumbers", damageNumbers);
            Set(feedback, "impulse", impulse);
            Set(feedback, "hitSparkPrefab", hitSpark);
            Set(feedback, "deathBurstPrefab", deathBurst);

            ModalPanel modal = CreateModal(ui);
            CreateAudio(ui);

            // Points d'apparition dans les coins de l'arène.
            var spawnRoot = new GameObject("SpawnPoints").transform;
            Vector3[] positions =
            {
                new Vector3(-18f, 0f, 18f), new Vector3(18f, 0f, 18f),
                new Vector3(18f, 0f, -18f), new Vector3(-18f, 0f, -18f),
            };
            var points = new Object[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject("Spawn_" + (i + 1)).transform;
                point.SetParent(spawnRoot);
                point.position = positions[i];
                points[i] = point;
            }

            var game = new GameObject("ArenaGame").AddComponent<ArenaGameController>();
            Set(game, "enemyPrefab", enemyPrefab);
            Set(game, "bossPrefab", bossPrefab);
            Set(game, "player", health);
            SetArray(game, "spawnPoints", points);
            Set(game, "waveText", waveText);
            Set(game, "goldText", goldText);
            Set(game, "messageText", message);
            Set(game, "modal", modal);
            Set(game, "pauseButton", pause);
            Set(game, "bossBar", bossBar.gameObject);
            Set(game, "bossNameText", bossName);
            Set(game, "bossHealthBar", bossHealthBar);
        }

        static void BuildTouchControls(Transform ui, PlayerMovement movement, PlayerSkills skills)
        {
            // Joystick : en bas à gauche.
            RectTransform stickBase = CreateImage("Joystick", ui, Knob, new Color(1f, 1f, 1f, 0.2f),
                new Vector2(0f, 0f), new Vector2(280f, 260f), Vector2.one * 320f);
            RectTransform handle = CreateImage("Handle", stickBase, Knob, new Color(1f, 1f, 1f, 0.6f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 140f);
            var stick = handle.gameObject.AddComponent<OnScreenStick>();
            Set(stick, "m_ControlPath", "<Gamepad>/leftStick");
            Set(stick, "m_MovementRange", 110f);

            // Bas à droite : gros bouton d'attaque, compétences en arc autour, esquive au-dessus.
            RectTransform attack = CreateTouchButton("AttackButton", "Attaque", ui, new Color(0.85f, 0.25f, 0.2f, 0.6f),
                new Vector2(-230f, 220f), 250f, "<Gamepad>/buttonWest", 40);

            string[] names = { "Charge", "Tourbillon", "Frappe\nsismique" };
            string[] paths = { "<Gamepad>/buttonNorth", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder" };
            Vector2[] positions = { new Vector2(-495f, 140f), new Vector2(-480f, 375f), new Vector2(-270f, 495f) };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform button = CreateTouchButton("Skill" + (i + 1), names[i], ui, new Color(0.95f, 0.65f, 0.2f, 0.6f),
                    positions[i], 150f, paths[i], 24);
                Image overlay = CreateCooldownOverlay(button, 150f);
                Text timer = CreateText("Timer", button, "", 56, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 150f);

                var cooldown = button.gameObject.AddComponent<SkillButtonCooldown>();
                Set(cooldown, "skills", skills);
                Set(cooldown, "skillIndex", i);
                Set(cooldown, "overlay", overlay);
                Set(cooldown, "timerText", timer);
            }

            RectTransform dodge = CreateTouchButton("DodgeButton", "Esquive", ui, new Color(0.25f, 0.6f, 0.95f, 0.6f),
                new Vector2(-100f, 440f), 130f, "<Gamepad>/buttonEast", 24);
            Image dodgeOverlay = CreateCooldownOverlay(dodge, 130f);
            var dodgeCooldown = dodgeOverlay.gameObject.AddComponent<DodgeCooldownOverlay>();
            Set(dodgeCooldown, "player", movement);

            attack.SetAsLastSibling();
        }

        static RectTransform CreateTouchButton(string name, string label, Transform parent, Color color,
            Vector2 position, float size, string controlPath, int fontSize)
        {
            RectTransform button = CreateImage(name, parent, Knob, color, new Vector2(1f, 0f), position, Vector2.one * size);
            var onScreen = button.gameObject.AddComponent<OnScreenButton>();
            Set(onScreen, "m_ControlPath", controlPath);
            CreateLabel(button, label, fontSize);
            return button;
        }

        static Image CreateCooldownOverlay(Transform parent, float size)
        {
            RectTransform rect = CreateImage("Cooldown", parent, Knob, new Color(0f, 0f, 0f, 0.6f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
            Image image = rect.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillAmount = 0f;
            image.raycastTarget = false;
            return image;
        }
    }
}
