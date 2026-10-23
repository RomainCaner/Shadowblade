using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Prépare les personnages KayKit (CC0, Kay Lousberg) : réglages d'import, matériaux,
    /// contrôleurs d'animation, et pose des modèles à la bonne taille dans les scènes.
    /// Les FBX contiennent déjà toutes les animations (rig "Generic").
    /// </summary>
    public static class KayKitCharacters
    {
        public const string Root = "Assets/_Project/Art/KayKit";
        public const string KnightPath = Root + "/Adventurers/Knight.fbx";
        public const string MinionPath = Root + "/Skeletons/Skeleton_Minion.fbx";
        public const string WarriorPath = Root + "/Skeletons/Skeleton_Warrior.fbx";
        const string KnightTexture = Root + "/Adventurers/knight_texture.png";
        const string SkeletonTexture = Root + "/Skeletons/skeleton_texture.png";
        const string ControllersFolder = "Assets/_Project/Animation";
        const string ImportVersion = "ShadowBlade.v1";

        /// <summary>Armes et boucliers du chevalier qu'on cache (on garde l'épée à une main et le bouclier rond).</summary>
        public static readonly string[] KnightHiddenParts =
            { "1H_Sword_Offhand", "2H_Sword", "Badge_Shield", "Rectangle_Shield", "Spike_Shield" };

        static readonly string[] LoopingClips =
        {
            "Idle", "Idle_B", "Idle_Combat", "2H_Melee_Idle", "Running_A", "Running_B", "Running_C",
            "Walking_A", "Walking_B", "Walking_C", "Walking_D_Skeletons", "2H_Melee_Attack_Spinning",
        };

        public class CharacterAssets
        {
            public RuntimeAnimatorController Hero;
            public RuntimeAnimatorController Minion;
            public RuntimeAnimatorController Boss;
            public Material KnightMaterial;
            public Material SkeletonMaterial;
        }

        public static bool FilesPresent =>
            File.Exists(KnightPath) && File.Exists(MinionPath) && File.Exists(WarriorPath);

        /// <summary>
        /// Configure l'import des modèles et génère matériaux et contrôleurs.
        /// Renvoie null si les fichiers KayKit sont absents (les scènes gardent alors les capsules).
        /// </summary>
        public static CharacterAssets Prepare()
        {
            if (!FilesPresent) return null;
            foreach (string path in new[] { KnightPath, MinionPath, WarriorPath })
                if (!ConfigureImport(path)) return null;

            BuilderUtility.EnsureFolder(ControllersFolder);
            return new CharacterAssets
            {
                KnightMaterial = CreateTexturedMaterial("KayKit_Knight", KnightTexture),
                SkeletonMaterial = CreateTexturedMaterial("KayKit_Skeleton", SkeletonTexture),
                Hero = CreateHeroController(),
                Minion = CreateEnemyController("Skeleton_Minion", MinionPath, idle: "Idle_Combat", run: "Running_A", runScale: 1.1f,
                    attack: "Unarmed_Melee_Attack_Punch_A", heavy: "Unarmed_Melee_Attack_Punch_B", spawn: "Spawn_Ground_Skeletons"),
                Boss = CreateEnemyController("Skeleton_Warrior", WarriorPath, idle: "Idle_Combat", run: "Walking_D_Skeletons", runScale: 1.4f,
                    attack: "Unarmed_Melee_Attack_Punch_B", heavy: "1H_Melee_Attack_Jump_Chop", spawn: "Skeletons_Awaken_Standing"),
            };
        }

        // ---------------------------------------------------------------- Import

        static bool ConfigureImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"KayKit : {path} n'est pas encore importé par Unity. Clique dans Unity, attends la fin de l'import, puis relance.");
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
            Debug.Log($"KayKit : import configuré pour {path} ({clips.Length} animations)");
            return true;
        }

        /// <summary>Les prises FBX peuvent s'appeler "Idle" ou "Rig|Idle" selon l'export.</summary>
        static bool Matches(string clipName, string wanted) =>
            clipName == wanted || clipName.EndsWith("|" + wanted, StringComparison.Ordinal);

        static AnimationClip Clip(string path, string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && Matches(c.name, name));
            if (clip == null) Debug.LogError($"KayKit : animation \"{name}\" introuvable dans {path}");
            return clip;
        }

        static Material CreateTexturedMaterial(string name, string texturePath)
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

        static AnimatorController CreateHeroController()
        {
            string p = KnightPath;
            AnimatorController controller = NewController("Hero");
            AddParameters(controller, ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger), ("ComboIndex", AnimatorControllerParameterType.Int),
                ("Dodge", AnimatorControllerParameterType.Trigger), ("Skill", AnimatorControllerParameterType.Trigger),
                ("SkillIndex", AnimatorControllerParameterType.Int), ("Casting", AnimatorControllerParameterType.Bool),
                ("Hit", AnimatorControllerParameterType.Trigger), ("Dead", AnimatorControllerParameterType.Bool));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = AddLocomotion(controller, Clip(p, "Idle"), Clip(p, "Running_A"), 1.15f);

            // Combo : durées proches de celles de PlayerCombat pour que l'animation colle au coup.
            string[] attacks = { "1H_Melee_Attack_Slice_Diagonal", "1H_Melee_Attack_Slice_Horizontal", "2H_Melee_Attack_Spin" };
            float[] attackDurations = { 0.42f, 0.42f, 0.6f };
            for (int i = 0; i < attacks.Length; i++)
            {
                AnimatorState state = AddTimedState(sm, "Attack" + (i + 1), Clip(p, attacks[i]), attackDurations[i]);
                AnimatorStateTransition enter = AnyState(sm, state, 0.05f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "ComboIndex");
                ExitWithTime(state, locomotion);
            }

            AnimatorState dodge = AddTimedState(sm, "Dodge", Clip(p, "Dodge_Forward"), 0.4f);
            AnyState(sm, dodge, 0.03f).AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            ExitWithTime(dodge, locomotion);

            // Compétences : l'animation reste tant que la compétence est en cours (Casting).
            string[] skills = { "1H_Melee_Attack_Stab", "2H_Melee_Attack_Spinning", "2H_Melee_Attack_Chop" };
            float[] skillDurations = { 0.45f, 0f, 0.75f }; // 0 = vitesse normale, en boucle
            for (int i = 0; i < skills.Length; i++)
            {
                AnimatorState state = skillDurations[i] > 0f
                    ? AddTimedState(sm, "Skill" + (i + 1), Clip(p, skills[i]), skillDurations[i])
                    : AddState(sm, "Skill" + (i + 1), Clip(p, skills[i]), 1.3f);
                AnimatorStateTransition enter = AnyState(sm, state, 0.05f);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Skill");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "SkillIndex");
                AnimatorStateTransition exit = state.AddTransition(locomotion);
                exit.hasExitTime = false;
                exit.duration = 0.15f;
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Casting");
            }

            AnimatorState hit = AddTimedState(sm, "Hit", Clip(p, "Hit_A"), 0.4f);
            AnyState(sm, hit, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            ExitWithTime(hit, locomotion);

            AddDeath(sm, Clip(p, "Death_A"));
            return controller;
        }

        static AnimatorController CreateEnemyController(string name, string p, string idle, string run, float runScale,
            string attack, string heavy, string spawn)
        {
            AnimatorController controller = NewController(name);
            AddParameters(controller, ("Speed", AnimatorControllerParameterType.Float),
                ("Attack", AnimatorControllerParameterType.Trigger), ("Heavy", AnimatorControllerParameterType.Bool),
                ("Hit", AnimatorControllerParameterType.Trigger), ("Dead", AnimatorControllerParameterType.Bool));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = AddLocomotion(controller, Clip(p, idle), Clip(p, run), runScale);

            AnimatorState spawnState = AddState(sm, "Spawn", Clip(p, spawn), 1f);
            ExitWithTime(spawnState, locomotion, 0.95f);
            sm.defaultState = spawnState;

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

            AnimatorState hit = AddTimedState(sm, "Hit", Clip(p, "Hit_A"), 0.35f);
            AnyState(sm, hit, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            ExitWithTime(hit, locomotion);

            AddDeath(sm, Clip(p, "Death_C_Skeletons"));
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
        /// Place un modèle KayKit sous "parent", mis à l'échelle pour mesurer "targetHeight" mètres,
        /// pieds posés à la hauteur de "ground".
        /// </summary>
        public static Animator InstantiateModel(string fbxPath, Transform parent, Transform ground, float targetHeight,
            Material material, RuntimeAnimatorController controller, params string[] hiddenParts)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                if (hiddenParts.Contains(part.name)) part.gameObject.SetActive(false);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            Bounds bounds = CombinedBounds(model);
            if (bounds.size.y > 0.001f)
            {
                model.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
                bounds = CombinedBounds(model);
                model.transform.position += Vector3.up * (ground.position.y - bounds.min.y);
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
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
        /// Point au bout de la lame d'une arme (pour la traînée lumineuse) :
        /// l'extrémité de l'axe le plus long du mesh, du côté opposé à la poignée.
        /// </summary>
        public static Transform CreateWeaponTip(Transform model, string weaponName)
        {
            Transform weapon = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == weaponName);
            Mesh mesh = weapon == null ? null
                : weapon.GetComponent<MeshFilter>() != null ? weapon.GetComponent<MeshFilter>().sharedMesh
                : weapon.GetComponent<SkinnedMeshRenderer>() != null ? weapon.GetComponent<SkinnedMeshRenderer>().sharedMesh
                : null;
            if (mesh == null)
            {
                Debug.LogWarning($"KayKit : arme \"{weaponName}\" introuvable, pas de traînée d'épée.");
                return null;
            }

            Bounds b = mesh.bounds;
            int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
            Vector3 tip = b.center;
            float positiveEnd = b.center[axis] + b.extents[axis];
            float negativeEnd = b.center[axis] - b.extents[axis];
            tip[axis] = Mathf.Abs(positiveEnd) >= Mathf.Abs(negativeEnd) ? positiveEnd : negativeEnd;

            var tipTransform = new GameObject("SwordTip").transform;
            tipTransform.SetParent(weapon, false);
            tipTransform.localPosition = tip;
            return tipTransform;
        }
    }
}
