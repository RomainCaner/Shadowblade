using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Prépare les personnages animés : réglages d'import, matériaux, contrôleurs d'animation,
    /// et pose des modèles à la bonne taille dans les scènes.
    /// Deux packs CC0 sont gérés : Quaternius "RPG Characters" (utilisé en priorité, proportions adultes)
    /// et KayKit (style plus "chibi", gardé en secours). Les FBX contiennent leurs animations (rig "Generic").
    /// </summary>
    public static class KayKitCharacters
    {
        public const string Root = "Assets/_Project/Art/KayKit";
        public const string KnightPath = Root + "/Adventurers/Knight.fbx";
        public const string MinionPath = Root + "/Skeletons/Skeleton_Minion.fbx";
        public const string WarriorPath = Root + "/Skeletons/Skeleton_Warrior.fbx";
        const string KnightTexture = Root + "/Adventurers/knight_texture.png";
        const string SkeletonTexture = Root + "/Skeletons/skeleton_texture.png";

        const string Quaternius = "Assets/_Project/Art/Quaternius/RPG";
        const string QWarrior = Quaternius + "/Warrior.fbx";
        const string QRogue = Quaternius + "/Rogue.fbx";
        const string QCleric = Quaternius + "/Cleric.fbx";

        const string ControllersFolder = "Assets/_Project/Animation";
        const string ImportVersion = "ShadowBlade.v1";

        /// <summary>Armes et boucliers du chevalier KayKit qu'on cache (on garde l'épée à une main et le bouclier rond).</summary>
        public static readonly string[] KnightHiddenParts =
            { "1H_Sword_Offhand", "2H_Sword", "Badge_Shield", "Rectangle_Shield", "Spike_Shield" };

        static readonly string[] LoopingClips =
        {
            "Idle", "Idle_B", "Idle_Combat", "2H_Melee_Idle", "Running_A", "Running_B", "Running_C",
            "Walking_A", "Walking_B", "Walking_C", "Walking_D_Skeletons", "2H_Melee_Attack_Spinning",
            "Idle_Weapon", "Run_Weapon", "Attacking_Idle", "Attack_Idle", "Idle_Attacking", "Run", "Walk",
        };

        /// <summary>Un modèle animé et la façon de le poser.</summary>
        public class CharacterModel
        {
            public string Path;
            public RuntimeAnimatorController Controller;
            /// <summary>Matériau unique pour tout le modèle ; si null, chaque matériau du FBX est remplacé
            /// par un matériau utilisant la texture du même nom dans TextureFolder.</summary>
            public Material Material;
            public string TextureFolder;
            public string[] Hidden = Array.Empty<string>();
            public float Height = 1.9f;
            /// <summary>Rotation du modèle si le FBX ne regarde pas vers +Z.</summary>
            public float Yaw;
        }

        public class CharacterAssets
        {
            public CharacterModel Hero;
            public CharacterModel Minion;
            public CharacterModel Boss;
            /// <summary>Nom de l'os ou de l'objet au bout de l'épée du héros (pour la traînée).</summary>
            public string HeroWeaponTip;
            /// <summary>Faut-il "dessiner" par le code la rotation du Tourbillon (pas d'animation dédiée) ?</summary>
            public bool HeroProceduralPoses;
            public string MinionName = "Squelette";
            public float MinionSpawn = 1.2f, MinionDeathDelay = 1.2f, MinionImpact = 0.35f;
            public float BossSpawn = 1.5f, BossDeathDelay = 1.6f, BossImpact = 0.4f, BossHeavyImpact = 0.75f;
        }

        /// <summary>Clips d'animation du héros, dans l'ordre attendu par le contrôleur.</summary>
        class HeroClips
        {
            public string Idle, Run, Dodge, Hit, Death;
            public float RunScale = 1f;
            public string[] Attacks;
            public float[] AttackDurations = { 0.42f, 0.42f, 0.6f };
            public string[] Skills;
            /// <summary>Durée de chaque compétence (0 = vitesse normale, en boucle).</summary>
            public float[] SkillDurations;
        }

        public static bool FilesPresent =>
            File.Exists(KnightPath) && File.Exists(MinionPath) && File.Exists(WarriorPath);

        static bool QuaterniusPresent =>
            File.Exists(QWarrior) && File.Exists(QRogue) && File.Exists(QCleric);

        /// <summary>
        /// Configure l'import des modèles et génère matériaux et contrôleurs.
        /// Renvoie null si aucun pack n'est présent (les scènes gardent alors les capsules).
        /// </summary>
        public static CharacterAssets Prepare()
        {
            BuilderUtility.EnsureFolder(ControllersFolder);
            if (QuaterniusPresent) return PrepareQuaternius();
            if (FilesPresent) return PrepareKayKit();
            return null;
        }

        static CharacterAssets PrepareQuaternius()
        {
            foreach (string path in new[] { QWarrior, QRogue, QCleric })
                if (!ConfigureImport(path)) return null;

            return new CharacterAssets
            {
                Hero = new CharacterModel
                {
                    Path = QWarrior, TextureFolder = Quaternius, Height = 1.85f,
                    Controller = CreateHeroController("Hero", QWarrior, new HeroClips
                    {
                        Idle = "Idle_Weapon", Run = "Run_Weapon", Dodge = "Roll", Hit = "RecieveHit", Death = "Death",
                        Attacks = new[] { "Sword_AttackFast", "Sword_AttackFast", "Sword_Attack" },
                        Skills = new[] { "Sword_AttackFast", "Sword_Attack", "Sword_Attack" },
                        SkillDurations = new[] { 0.45f, 0.55f, 0.75f },
                    }),
                },
                HeroWeaponTip = "Weapon.R_end",
                HeroProceduralPoses = true, // pas d'animation de rotation : le Tourbillon fait tourner le modèle
                Minion = new CharacterModel
                {
                    Path = QRogue, TextureFolder = Quaternius, Height = 1.75f,
                    Controller = CreateEnemyController("Assassin", QRogue, idle: "Attacking_Idle", run: "Run", runScale: 1f,
                        attack: "Dagger_Attack", heavy: "Dagger_Attack2", hit: "RecieveHit", death: "Death", spawn: null),
                },
                MinionName = "Assassin",
                MinionSpawn = 0.6f, MinionDeathDelay = 1.3f, MinionImpact = 0.35f,
                Boss = new CharacterModel
                {
                    Path = QCleric, TextureFolder = Quaternius, Height = 3.2f,
                    Controller = CreateEnemyController("Boss", QCleric, idle: "Attack_Idle", run: "Walk", runScale: 1.3f,
                        attack: "Staff_Attack", heavy: "Spell1", hit: "RecieveHit", death: "Death", spawn: null),
                },
                BossSpawn = 0.8f, BossDeathDelay = 1.8f, BossImpact = 0.45f, BossHeavyImpact = 0.8f,
            };
        }

        static CharacterAssets PrepareKayKit()
        {
            foreach (string path in new[] { KnightPath, MinionPath, WarriorPath })
                if (!ConfigureImport(path)) return null;

            Material skeleton = CreateTexturedMaterial("KayKit_Skeleton", SkeletonTexture);
            return new CharacterAssets
            {
                Hero = new CharacterModel
                {
                    Path = KnightPath, Material = CreateTexturedMaterial("KayKit_Knight", KnightTexture),
                    Hidden = KnightHiddenParts, Height = 1.9f,
                    Controller = CreateHeroController("Hero", KnightPath, new HeroClips
                    {
                        Idle = "Idle", Run = "Running_A", RunScale = 1.15f, Dodge = "Dodge_Forward", Hit = "Hit_A", Death = "Death_A",
                        Attacks = new[] { "1H_Melee_Attack_Slice_Diagonal", "1H_Melee_Attack_Slice_Horizontal", "2H_Melee_Attack_Spin" },
                        Skills = new[] { "1H_Melee_Attack_Stab", "2H_Melee_Attack_Spinning", "2H_Melee_Attack_Chop" },
                        SkillDurations = new[] { 0.45f, 0f, 0.75f },
                    }),
                },
                HeroWeaponTip = "1H_Sword",
                HeroProceduralPoses = false,
                Minion = new CharacterModel
                {
                    Path = MinionPath, Material = skeleton, Height = 1.8f,
                    Controller = CreateEnemyController("Skeleton_Minion", MinionPath, idle: "Idle_Combat", run: "Running_A", runScale: 1.1f,
                        attack: "Unarmed_Melee_Attack_Punch_A", heavy: "Unarmed_Melee_Attack_Punch_B", hit: "Hit_A",
                        death: "Death_C_Skeletons", spawn: "Spawn_Ground_Skeletons"),
                },
                Boss = new CharacterModel
                {
                    Path = WarriorPath, Material = skeleton, Height = 3.4f,
                    Controller = CreateEnemyController("Skeleton_Warrior", WarriorPath, idle: "Idle_Combat", run: "Walking_D_Skeletons", runScale: 1.4f,
                        attack: "Unarmed_Melee_Attack_Punch_B", heavy: "1H_Melee_Attack_Jump_Chop", hit: "Hit_A",
                        death: "Death_C_Skeletons", spawn: "Skeletons_Awaken_Standing"),
                },
            };
        }

        // ---------------------------------------------------------------- Import

        static bool ConfigureImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Personnages : {path} n'est pas encore importé par Unity. Clique dans Unity, attends la fin de l'import, puis relance.");
                return false;
            }
            if (importer.userData == ImportVersion) return true;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
                clip.loopTime = LoopingClips.Any(name => Matches(clip.name, name));
            importer.clipAnimations = clips;
            importer.userData = ImportVersion;
            importer.SaveAndReimport();
            Debug.Log($"Personnages : import configuré pour {path} ({clips.Length} animations)");
            return true;
        }

        /// <summary>Les prises FBX peuvent s'appeler "Idle" ou "Rig|Idle" selon l'export.</summary>
        static bool Matches(string clipName, string wanted) =>
            clipName == wanted || clipName.EndsWith("|" + wanted, StringComparison.Ordinal);

        static AnimationClip Clip(string path, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && Matches(c.name, name));
            if (clip == null) Debug.LogError($"Personnages : animation \"{name}\" introuvable dans {path}");
            return clip;
        }

        public static Material CreateTexturedMaterial(string name, string texturePath)
        {
            Material material = BuilderUtility.CreateMaterial(name, Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Smoothness", 0.15f);
            // Émission active (noire par défaut) : HitFlash la fait briller quand le personnage est touché.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- Contrôleurs

        static AnimatorController CreateHeroController(string name, string p, HeroClips clips)
        {
            AnimatorController controller = NewController(name);
            AddParameters(controller, ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger), ("ComboIndex", AnimatorControllerParameterType.Int),
                ("Dodge", AnimatorControllerParameterType.Trigger), ("Skill", AnimatorControllerParameterType.Trigger),
                ("SkillIndex", AnimatorControllerParameterType.Int), ("Casting", AnimatorControllerParameterType.Bool),
                ("Hit", AnimatorControllerParameterType.Trigger), ("Dead", AnimatorControllerParameterType.Bool));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = AddLocomotion(controller, Clip(p, clips.Idle), Clip(p, clips.Run), clips.RunScale);

            // Combo : durées proches de celles de PlayerCombat pour que l'animation colle au coup.
            for (int i = 0; i < clips.Attacks.Length; i++)
            {
                AnimatorState state = AddTimedState(sm, "Attack" + (i + 1), Clip(p, clips.Attacks[i]), clips.AttackDurations[i]);
                AnimatorStateTransition enter = AnyState(sm, state, 0.05f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "ComboIndex");
                ExitWithTime(state, locomotion);
            }

            AnimatorState dodge = AddTimedState(sm, "Dodge", Clip(p, clips.Dodge), 0.4f);
            AnyState(sm, dodge, 0.03f).AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            ExitWithTime(dodge, locomotion);

            // Compétences : l'animation reste tant que la compétence est en cours (Casting).
            for (int i = 0; i < clips.Skills.Length; i++)
            {
                AnimatorState state = clips.SkillDurations[i] > 0f
                    ? AddTimedState(sm, "Skill" + (i + 1), Clip(p, clips.Skills[i]), clips.SkillDurations[i])
                    : AddState(sm, "Skill" + (i + 1), Clip(p, clips.Skills[i]), 1.3f);
                AnimatorStateTransition enter = AnyState(sm, state, 0.05f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Skill");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "SkillIndex");
                AnimatorStateTransition exit = state.AddTransition(locomotion);
                exit.hasExitTime = false;
                exit.duration = 0.15f;
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Casting");
            }

            AnimatorState hit = AddTimedState(sm, "Hit", Clip(p, clips.Hit), 0.4f);
            AnyState(sm, hit, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            ExitWithTime(hit, locomotion);

            AddDeath(sm, Clip(p, clips.Death));
            return controller;
        }

        static AnimatorController CreateEnemyController(string name, string p, string idle, string run, float runScale,
            string attack, string heavy, string hit, string death, string spawn)
        {
            AnimatorController controller = NewController(name);
            AddParameters(controller, ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger), ("Heavy", AnimatorControllerParameterType.Bool),
                ("Hit", AnimatorControllerParameterType.Trigger), ("Dead", AnimatorControllerParameterType.Bool));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = AddLocomotion(controller, Clip(p, idle), Clip(p, run), runScale);

            if (!string.IsNullOrEmpty(spawn))
            {
                AnimatorState spawnState = AddState(sm, "Spawn", Clip(p, spawn), 1f);
                ExitWithTime(spawnState, locomotion, 0.95f);
                sm.defaultState = spawnState;
            }

            AnimatorState attackState = AddState(sm, "Attack", Clip(p, attack), 1f);
            AnimatorStateTransition toAttack = AnyState(sm, attackState, 0.05f);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Heavy");
            ExitWithTime(attackState, locomotion);

            AnimatorState heavyState = AddState(sm, "HeavyAttack", Clip(p, heavy), 1f);
            AnimatorStateTransition toHeavy = AnyState(sm, heavyState, 0.05f);
            toHeavy.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toHeavy.AddCondition(AnimatorConditionMode.If, 0f, "Heavy");
            ExitWithTime(heavyState, locomotion);

            AnimatorState hitState = AddTimedState(sm, "Hit", Clip(p, hit), 0.35f);
            AnyState(sm, hitState, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            ExitWithTime(hitState, locomotion);

            AddDeath(sm, Clip(p, death));
            return controller;
        }

        static AnimatorController NewController(string name)
        {
            string path = $"{ControllersFolder}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static void AddParameters(AnimatorController controller, params (string name, AnimatorControllerParameterType type)[] parameters)
        {
            foreach (var (name, type) in parameters) controller.AddParameter(name, type);
        }

        static AnimatorState AddLocomotion(AnimatorController controller, AnimationClip idle, AnimationClip run, float runScale)
        {
            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(run, 1f);
            ChildMotion[] children = tree.children;
            children[1].timeScale = runScale;
            tree.children = children;
            controller.layers[0].stateMachine.defaultState = state;
            return state;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, float speed)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            state.speed = speed;
            return state;
        }

        /// <summary>État dont la vitesse est ajustée pour que l'animation dure "duration" secondes.</summary>
        static AnimatorState AddTimedState(AnimatorStateMachine sm, string name, AnimationClip clip, float duration)
        {
            float speed = clip != null && duration > 0f ? Mathf.Max(0.2f, clip.length / duration) : 1f;
            return AddState(sm, name, clip, speed);
        }

        /// <summary>Transition depuis n'importe quel état, sauf si le personnage est mort.</summary>
        static AnimatorStateTransition AnyState(AnimatorStateMachine sm, AnimatorState target, float duration)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(target);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            return transition;
        }

        static void ExitWithTime(AnimatorState from, AnimatorState to, float exitTime = 0.88f)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.12f;
        }

        static void AddDeath(AnimatorStateMachine sm, AnimationClip clip)
        {
            AnimatorState death = AddState(sm, "Death", clip, 1f);
            AnimatorStateTransition transition = sm.AddAnyStateTransition(death);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        }

        // ---------------------------------------------------------------- Modèles dans la scène

        /// <summary>
        /// Place un modèle sous "parent", mis à l'échelle pour mesurer model.Height mètres,
        /// pieds posés à la hauteur de "ground".
        /// </summary>
        public static Animator InstantiateModel(CharacterModel model, Transform parent, Transform ground)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(model.Path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, model.Yaw, 0f);
            go.transform.localScale = Vector3.one;

            foreach (Transform part in go.GetComponentsInChildren<Transform>(true))
                if (model.Hidden.Contains(part.name)) part.gameObject.SetActive(false);

            var cache = new Dictionary<string, Material>();
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = ResolveMaterial(model, materials[i], cache);
                renderer.sharedMaterials = materials;
            }

            Bounds bounds = CombinedBounds(go);
            if (bounds.size.y > 0.001f)
            {
                go.transform.localScale = Vector3.one * (model.Height / bounds.size.y);
                bounds = CombinedBounds(go);
                go.transform.position += Vector3.up * (ground.position.y - bounds.min.y);
            }

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = model.Controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        /// <summary>
        /// Matériau final d'un emplacement : le matériau unique du modèle, ou sinon un matériau URP
        /// (avec émission pour le flash des coups) utilisant la texture qui porte le nom du matériau d'origine.
        /// </summary>
        static Material ResolveMaterial(CharacterModel model, Material original, Dictionary<string, Material> cache)
        {
            if (model.Material != null) return model.Material;
            if (original == null || string.IsNullOrEmpty(model.TextureFolder)) return original;
            if (cache.TryGetValue(original.name, out Material cached)) return cached;

            string texture = $"{model.TextureFolder}/{original.name}.png";
            Material result = File.Exists(texture) ? CreateTexturedMaterial("Char_" + original.name, texture) : original;
            cache[original.name] = result;
            return result;
        }

        static Bounds CombinedBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Point au bout de la lame (pour la traînée lumineuse). "tipName" peut être un os placé au bout
        /// de l'arme (ex : "Weapon.R_end") ou un mesh d'arme dont on prend l'extrémité la plus éloignée de la poignée.
        /// </summary>
        public static Transform CreateWeaponTip(Transform model, string tipName)
        {
            Transform target = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == tipName);
            if (target == null)
            {
                Debug.LogWarning($"Personnages : \"{tipName}\" introuvable, pas de traînée d'épée.");
                return null;
            }

            Mesh mesh = target.GetComponent<MeshFilter>() != null ? target.GetComponent<MeshFilter>().sharedMesh
                : target.GetComponent<SkinnedMeshRenderer>() != null ? target.GetComponent<SkinnedMeshRenderer>().sharedMesh
                : null;

            var tip = new GameObject("SwordTip").transform;
            tip.SetParent(target, false);
            if (mesh == null) return tip; // un os : on se place dessus

            Bounds b = mesh.bounds;
            int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
            Vector3 position = b.center;
            float positiveEnd = b.center[axis] + b.extents[axis];
            float negativeEnd = b.center[axis] - b.extents[axis];
            position[axis] = Mathf.Abs(positiveEnd) >= Mathf.Abs(negativeEnd) ? positiveEnd : negativeEnd;
            tip.localPosition = position;
            return tip;
        }
    }
}
