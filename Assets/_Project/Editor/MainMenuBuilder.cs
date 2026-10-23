using System.Collections.Generic;
using ShadowBlade.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ShadowBlade.EditorTools.BuilderUtility;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Construit l'écran d'accueil "MainMenu", inspiré du lobby de Darkness Rises :
    /// héros sur un piédestal, barre du haut (niveau, or, options), cartes des modes de jeu à droite,
    /// barre de menus en bas, sélection des missions et paramètres.
    /// </summary>
    public static class MainMenuBuilder
    {
        [MenuItem("ShadowBlade/Tout construire (menu + arène)", priority = 0)]
        public static void BuildAllFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        [MenuItem("ShadowBlade/Construire le menu principal", priority = 21)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>Point d'entrée pour Unity en ligne de commande (-executeMethod).</summary>
        public static void BuildFromCommandLine() => BuildAll();

        static void BuildAll()
        {
            ArenaBuilder.BuildScene();
            BuildScene(); // le menu en dernier : c'est la scène qui reste ouverte
        }

        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            BuildShowcase();
            BuildUI();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("MENU_BUILT " + MenuScenePath);
        }

        // ---------------------------------------------------------------- Décor 3D

        static void BuildShowcase()
        {
            Material hero = CreateMaterial("Hero", new Color(0.2f, 0.45f, 0.85f));
            Material steel = CreateMaterial("Steel", new Color(0.85f, 0.85f, 0.9f), metallic: 0.8f);
            Material stone = CreateMaterial("Stone", new Color(0.45f, 0.42f, 0.4f));
            Material ground = CreateMaterial("Ground", new Color(0.22f, 0.22f, 0.25f));

            var backdrop = new Color(0.04f, 0.03f, 0.06f);
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 1.6f, -5.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.1f, 0f) - camera.transform.position);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backdrop;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = backdrop;
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 22f;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.GetComponent<Renderer>().sharedMaterial = ground;

            // Le héros est décalé à gauche : les menus occupent la droite de l'écran.
            var heroPosition = new Vector3(-1.8f, 0f, 0f);
            GameObject pedestal = CreateVisualPrimitive(PrimitiveType.Cylinder, "Pedestal", null, stone);
            pedestal.transform.position = heroPosition + Vector3.up * 0.1f;
            pedestal.transform.localScale = new Vector3(2f, 0.1f, 2f);

            var showcase = new GameObject("HeroShowcase");
            showcase.transform.position = heroPosition + Vector3.up * 0.2f;
            showcase.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            showcase.AddComponent<Rotator>();
            BuildHeroVisual(showcase.transform, hero, steel, out Transform swordPivot);
            swordPivot.localRotation = Quaternion.Euler(-60f, 30f, 0f); // épée levée

            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
                sun.color = new Color(1f, 0.85f, 0.7f);
                sun.intensity = 0.9f;
            }

            // Contre-jour rougeoyant derrière le héros.
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(1f, 0.3f, 0.15f);
            rim.intensity = 4f;
            rim.range = 8f;
            rim.transform.position = heroPosition + new Vector3(-1.2f, 2.2f, 1.6f);
        }

        // ---------------------------------------------------------------- Interface

        static void BuildUI()
        {
            CreateEventSystem();
            Transform ui = CreateCanvas("UI").transform;
            var comingSoon = new List<Object>();

            // Barre du haut : héros et niveau, or, options.
            RectTransform topBar = CreateBand("TopBar", ui, 1f, 90f, new Color(0f, 0f, 0f, 0.6f));
            CreateImage("Avatar", topBar, Knob, new Color(0.2f, 0.45f, 0.85f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), Vector2.one * 64f);
            Text heroText = CreateText("HeroText", topBar, "GUERRIER   Niv. 1", 32, new Vector2(0f, 0.5f),
                new Vector2(340f, 0f), new Vector2(480f, 60f), TextAnchor.MiddleLeft);
            Text goldText = CreateText("GoldText", topBar, "Or  0", 32, new Vector2(1f, 0.5f),
                new Vector2(-380f, 0f), new Vector2(300f, 60f), TextAnchor.MiddleRight, Gold);
            Button settingsButton = CreateButton("SettingsButton", topBar, "Options", new Vector2(1f, 0.5f),
                new Vector2(-110f, 0f), new Vector2(170f, 64f), Neutral, 28);

            // Titre.
            CreateText("Title", ui, "SHADOWBLADE", 88, new Vector2(0f, 1f), new Vector2(420f, -175f),
                new Vector2(760f, 110f), color: Gold);
            CreateText("Subtitle", ui, "L'Ordre de la Lame", 32, new Vector2(0f, 1f), new Vector2(420f, -245f),
                new Vector2(760f, 50f), color: new Color(0.8f, 0.8f, 0.85f));

            // Cartes des modes de jeu (à droite).
            Button storyButton = CreateModeCard("StoryCard", ui, "HISTOIRE", "Chapitre 1", Crimson,
                new Vector2(-340f, 265f), out Text storySubtitle);
            Button survivalButton = CreateModeCard("CombatCard", ui, "MODE COMBAT", "Vagues infinies",
                new Color(0.13f, 0.22f, 0.5f, 0.95f), new Vector2(-340f, 95f), out Text survivalSubtitle);
            comingSoon.Add(CreateModeCard("DungeonCard", ui, "DONJONS", "Bientôt disponible", Neutral,
                new Vector2(-340f, -75f), out _));
            comingSoon.Add(CreateModeCard("PvpCard", ui, "JCJ", "Bientôt disponible", Neutral,
                new Vector2(-340f, -245f), out _));

            // Barre du bas : sections pas encore disponibles.
            RectTransform bottomBar = CreateBand("BottomBar", ui, 0f, 110f, new Color(0f, 0f, 0f, 0.6f));
            string[] sections = { "Héros", "Compétences", "Inventaire", "Boutique", "Classement" };
            for (int i = 0; i < sections.Length; i++)
            {
                comingSoon.Add(CreateButton(sections[i] + "Button", bottomBar, sections[i], new Vector2(0f, 0.5f),
                    new Vector2(150f + i * 240f, 0f), new Vector2(220f, 76f), new Color(0.18f, 0.17f, 0.2f, 0.95f), 28));
            }

            Text toast = CreateText("Toast", ui, "", 36, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(900f, 70f),
                color: Gold);

            BuildStoryPanel(ui, out GameObject storyPanel, out Text chapterTitle, out Button[] stageButtons, out Button storyBack);
            BuildSettingsPanel(ui, out GameObject settingsPanel, out Slider volume, out Button reset, out Button close);

            var controller = new GameObject("MainMenu").AddComponent<MainMenuController>();
            Set(controller, "heroText", heroText);
            Set(controller, "goldText", goldText);
            Set(controller, "settingsButton", settingsButton);
            Set(controller, "storyButton", storyButton);
            Set(controller, "storySubtitle", storySubtitle);
            Set(controller, "survivalButton", survivalButton);
            Set(controller, "survivalSubtitle", survivalSubtitle);
            SetArray(controller, "comingSoonButtons", comingSoon);
            Set(controller, "storyPanel", storyPanel);
            Set(controller, "chapterTitle", chapterTitle);
            SetArray(controller, "stageButtons", stageButtons);
            Set(controller, "storyBackButton", storyBack);
            Set(controller, "settingsPanel", settingsPanel);
            Set(controller, "volumeSlider", volume);
            Set(controller, "resetProgressButton", reset);
            Set(controller, "settingsCloseButton", close);
            Set(controller, "toastText", toast);
        }

        static Button CreateModeCard(string name, Transform parent, string title, string subtitle, Color color,
            Vector2 position, out Text subtitleText)
        {
            Button card = CreateButton(name, parent, "", new Vector2(1f, 0.5f), position, new Vector2(600f, 150f), color);
            var outline = card.gameObject.GetComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText("Title", card.transform, title, 50, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(540f, 64f), TextAnchor.MiddleLeft);
            subtitleText = CreateText("Subtitle", card.transform, subtitle, 24, new Vector2(0.5f, 0f), new Vector2(0f, 38f),
                new Vector2(540f, 44f), TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            return card;
        }

        static void BuildStoryPanel(Transform ui, out GameObject panel, out Text chapterTitle, out Button[] stageButtons,
            out Button back)
        {
            RectTransform window = CreateOverlayWindow("StoryPanel", ui, new Vector2(1200f, 880f), out panel);
            chapterTitle = CreateText("ChapterTitle", window, "Chapitre 1", 50, new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(1100f, 80f), color: Gold);

            stageButtons = new Button[5];
            for (int i = 0; i < stageButtons.Length; i++)
            {
                Button stage = CreateButton("Stage" + (i + 1), window, "Mission", new Vector2(0.5f, 1f),
                    new Vector2(0f, -185f - i * 118f), new Vector2(1040f, 104f), new Color(0.3f, 0.13f, 0.1f, 0.95f), 34);
                var label = stage.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(40f, 0f);
                stageButtons[i] = stage;
            }

            back = CreateButton("BackButton", window, "Retour", new Vector2(0.5f, 0f), new Vector2(0f, 70f),
                new Vector2(320f, 84f), Neutral, 34);
        }

        static void BuildSettingsPanel(Transform ui, out GameObject panel, out Slider volume, out Button reset, out Button close)
        {
            RectTransform window = CreateOverlayWindow("SettingsPanel", ui, new Vector2(900f, 580f), out panel);
            CreateText("Title", window, "Paramètres", 50, new Vector2(0.5f, 1f), new Vector2(0f, -65f),
                new Vector2(800f, 80f), color: Gold);

            CreateText("VolumeLabel", window, "Volume", 34, new Vector2(0.5f, 0.5f), new Vector2(-270f, 90f),
                new Vector2(260f, 60f), TextAnchor.MiddleLeft);
            volume = CreateSlider(window, new Vector2(0.5f, 0.5f), new Vector2(110f, 90f), new Vector2(480f, 40f));

            reset = CreateButton("ResetButton", window, "Réinitialiser la progression", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -50f), new Vector2(600f, 84f), Crimson, 30);
            close = CreateButton("CloseButton", window, "Fermer", new Vector2(0.5f, 0f), new Vector2(0f, 70f),
                new Vector2(320f, 84f), Neutral, 34);
        }
    }
}
