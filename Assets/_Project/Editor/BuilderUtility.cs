using System.Collections.Generic;
using System.IO;
using ShadowBlade.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Fonctions communes aux outils qui construisent les scènes (matériaux, modèles provisoires, interface).
    /// </summary>
    public static class BuilderUtility
    {
        public const string MaterialsFolder = "Assets/_Project/Materials";
        public const string PrefabsFolder = "Assets/_Project/Prefabs";
        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string ArenaScenePath = "Assets/Scenes/Arena.unity";
        public const string DungeonScenePath = "Assets/Scenes/Dungeon.unity";

        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);
        public static readonly Color Crimson = new Color(0.62f, 0.14f, 0.1f, 0.95f);
        public static readonly Color DarkPanel = new Color(0.08f, 0.07f, 0.1f, 0.96f);
        public static readonly Color Neutral = new Color(0.28f, 0.28f, 0.32f, 0.95f);

        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // ---------------------------------------------------------------- Projet

        /// <summary>
        /// À appeler au début de chaque outil du menu : refuse de construire pendant le mode Play
        /// (Unity l'interdit) et propose d'enregistrer la scène ouverte.
        /// </summary>
        public static bool CanBuildNow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ShadowBlade",
                    "Arrête d'abord le mode Play (bouton ▶ en haut), puis relance la construction.", "OK");
                return false;
            }
            return UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        /// <summary>Menu principal en premier, puis l'arène (seulement les scènes qui existent).</summary>
        public static void UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in new[] { MenuScenePath, ArenaScenePath, DungeonScenePath })
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void EnsureFolder(string path)
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

        public static Material CreateMaterial(string name, Color color, float metallic = 0f)
        {
            EnsureFolder(MaterialsFolder);
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

        /// <summary>
        /// Matériau additif (lumineux, transparent) pour les particules et la traînée de l'épée.
        /// La texture ronde "Knob" donne des particules douces au lieu de carrés.
        /// </summary>
        public static Material CreateFxMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/FX_Additive.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", Knob.texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);   // transparent
            material.SetFloat("_Blend", 2f);     // additif
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Explosion de particules qui se détruit toute seule (étincelles, mort d'un ennemi).</summary>
        public static ParticleSystem CreateBurstPrefab(string name, Material material, int count, Vector2 speed,
            Vector2 lifetime, Vector2 size, Color colorA, Color colorB, float gravity, bool stretched)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = count * 2;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.SizeOverLifetimeModule shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1f;
            }

            EnsureFolder(PrefabsFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        /// <summary>Gestionnaire de sons de la scène + sons de clic sur tous les boutons du canvas.</summary>
        public static void CreateAudio(Transform canvas)
        {
            new GameObject("Audio").AddComponent<ShadowBlade.Audio.AudioManager>();
            canvas.gameObject.AddComponent<ShadowBlade.Audio.ButtonClickSounds>();
        }

        // ---------------------------------------------------------------- Valeurs sérialisées

        public static void Set(Object target, string property, string value) =>
            Apply(target, property, p => p.stringValue = value);
        public static void Set(Object target, string property, float value) =>
            Apply(target, property, p => p.floatValue = value);
        public static void Set(Object target, string property, int value) =>
            Apply(target, property, p => p.intValue = value);
        public static void Set(Object target, string property, bool value) =>
            Apply(target, property, p => p.boolValue = value);
        public static void Set(Object target, string property, Color value) =>
            Apply(target, property, p => p.colorValue = value);
        public static void Set(Object target, string property, Object value) =>
            Apply(target, property, p => p.objectReferenceValue = value);

        public static void SetArray(Object target, string property, IReadOnlyList<Object> values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(property);
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Apply(Object target, string property, System.Action<SerializedProperty> setter)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Propriété introuvable : {target.GetType().Name}.{property}");
                return;
            }
            setter(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Modèles 3D provisoires

        public static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
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

        /// <summary>Primitive purement visuelle (sans collider).</summary>
        public static GameObject CreateVisualPrimitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Guerrier provisoire (capsule + épée). Le pivot du visuel est au centre du corps
        /// pour que la roulade et le tourbillon tournent sur eux-mêmes.
        /// </summary>
        public static Transform BuildHeroVisual(Transform parent, Material hero, Material steel, out Transform swordPivot)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);

            GameObject body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body", visual, hero);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            GameObject face = CreateVisualPrimitive(PrimitiveType.Cube, "Face", visual, steel);
            face.transform.localPosition = new Vector3(0f, 0.5f, 0.35f);
            face.transform.localScale = new Vector3(0.4f, 0.15f, 0.15f);

            swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(visual, false);
            swordPivot.localPosition = new Vector3(0f, 0f, 0.1f);

            GameObject sword = CreateVisualPrimitive(PrimitiveType.Cube, "Sword", swordPivot, steel);
            sword.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            sword.transform.localScale = new Vector3(0.08f, 0.08f, 1.4f);
            return visual;
        }

        // ---------------------------------------------------------------- Interface

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform CreateStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Bande pleine largeur collée en haut (y = 1) ou en bas (y = 0) de l'écran.</summary>
        public static RectTransform CreateBand(string name, Transform parent, float anchorY, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            go.AddComponent<Image>().color = color;
            return rect;
        }

        public static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return rect;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize,
            Vector2 anchor, Vector2 position, Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            RectTransform rect = CreateRect(name, parent, anchor, position, size);
            return SetupText(rect.gameObject, content, fontSize, alignment, color ?? Color.white);
        }

        /// <summary>Texte qui occupe tout son parent (étiquette de bouton).</summary>
        public static Text CreateLabel(Transform parent, string content, int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = CreateStretched("Label", parent);
            return SetupText(rect.gameObject, content, fontSize, alignment, Color.white);
        }

        static Text SetupText(GameObject go, string content, int fontSize, TextAnchor alignment, Color color)
        {
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 position,
            Vector2 size, Color color, int fontSize = 32)
        {
            RectTransform rect = CreateImage(name, parent, null, color, anchor, position, size);
            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            button.colors = colors;
            rect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            CreateLabel(rect, label, fontSize);
            return button;
        }

        /// <summary>Fond sombre + remplissage qui s'étire de gauche à droite. Renvoie le remplissage.</summary>
        public static RectTransform CreateBar(Transform parent, Color fillColor)
        {
            RectTransform background = CreateStretched("Background", parent);
            background.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform fill = CreateStretched("Fill", parent);
            fill.gameObject.AddComponent<Image>().color = fillColor;
            return fill;
        }

        /// <summary>Voile sombre plein écran + fenêtre centrée. Renvoie la fenêtre.</summary>
        public static RectTransform CreateOverlayWindow(string name, Transform parent, Vector2 windowSize, out GameObject root)
        {
            RectTransform overlay = CreateStretched(name, parent);
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            root = overlay.gameObject;

            RectTransform window = CreateImage("Window", overlay, null, DarkPanel, new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
            var outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);
            return window;
        }

        public static ModalPanel CreateModal(Transform canvas)
        {
            RectTransform window = CreateOverlayWindow("Modal", canvas, new Vector2(1050f, 600f), out GameObject root);

            Text title = CreateText("Title", window, "Titre", 56, new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(950f, 90f), color: Gold);
            Text body = CreateText("Body", window, "", 32, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(920f, 280f));
            Button primary = CreateButton("PrimaryButton", window, "OK", new Vector2(0.5f, 0f), new Vector2(190f, 85f),
                new Vector2(340f, 95f), Crimson, 36);
            Button secondary = CreateButton("SecondaryButton", window, "Retour", new Vector2(0.5f, 0f), new Vector2(-190f, 85f),
                new Vector2(340f, 95f), Neutral, 36);

            var modal = root.AddComponent<ModalPanel>();
            Set(modal, "title", title);
            Set(modal, "body", body);
            Set(modal, "primaryButton", primary);
            Set(modal, "secondaryButton", secondary);
            root.SetActive(false);
            return modal;
        }

        public static Slider CreateSlider(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = Knob,
            };
            GameObject go = DefaultControls.CreateSlider(resources);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go.GetComponent<Slider>();
        }
    }
}
