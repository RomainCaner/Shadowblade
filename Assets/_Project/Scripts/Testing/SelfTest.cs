using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using ShadowBlade.Combat;
using ShadowBlade.Core;
using ShadowBlade.Enemies;
using ShadowBlade.Level;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowBlade.Testing
{
    /// <summary>
    /// Robot de test automatique, actif seulement si le jeu est lancé avec "-selftest &lt;dossier&gt;".
    /// Il passe par le menu, lance le mode Combat, joue avec une manette virtuelle (déplacement, combo,
    /// compétences, roulade), prend des captures d'écran et écrit un rapport, puis quitte le jeu.
    /// </summary>
    public class SelfTest : MonoBehaviour
    {
        string outputFolder;
        Gamepad pad;
        readonly StringBuilder report = new StringBuilder();
        int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-selftest");
            if (index < 0 || index + 1 >= args.Length) return;

            var go = new GameObject("SelfTest");
            DontDestroyOnLoad(go);
            go.AddComponent<SelfTest>().outputFolder = args[index + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputFolder);
            Application.logMessageReceived += OnLog;
            Log($"Démarrage du test - {DateTime.Now:HH:mm:ss}");

            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("menu");

            GameSession.StartSurvival();
            yield return new WaitForSecondsRealtime(1.5f);
            pad = InputSystem.AddDevice<Gamepad>("SelfTestPad");
            yield return Capture("arene_debut");

            // Course vers l'avant puis vers la droite.
            Stick(new Vector2(0f, 1f));
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("course");
            Stick(new Vector2(1f, 0f));
            yield return new WaitForSecondsRealtime(0.6f);
            Stick(Vector2.zero);

            // Attendre que les squelettes sortent de terre.
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("apparition_ennemis");
            yield return new WaitForSecondsRealtime(1.5f);

            // Aller au contact et enchaîner le combo.
            yield return Approach(2f, 5f);
            yield return Capture("au_contact");
            for (int i = 0; i < 3; i++)
            {
                yield return Press(GamepadButton.West);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Capture($"combo_{i + 1}");
            }
            yield return new WaitForSecondsRealtime(0.6f);

            // Compétences.
            string[] skillNames = { "charge", "tourbillon", "frappe_sismique" };
            GamepadButton[] skillButtons = { GamepadButton.North, GamepadButton.LeftShoulder, GamepadButton.RightShoulder };
            for (int i = 0; i < skillButtons.Length; i++)
            {
                yield return Approach(2.5f, 4f);
                yield return Press(skillButtons[i]);
                yield return new WaitForSecondsRealtime(i == 1 ? 0.6f : 0.25f);
                yield return Capture("competence_" + skillNames[i]);
                yield return new WaitForSecondsRealtime(1.2f);
            }

            // Roulade.
            Stick(new Vector2(-1f, 0f));
            yield return Press(GamepadButton.East);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Capture("roulade");
            Stick(Vector2.zero);

            // Laisser les ennemis attaquer (cercle d'avertissement).
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Capture("ennemis_attaquent_" + (i + 1));
            }

            // Combat libre pendant 20 secondes : le robot frappe et utilise ses compétences.
            float end = Time.realtimeSinceStartup + 20f;
            int round = 0;
            while (Time.realtimeSinceStartup < end && !PlayerDead() && !ModalOpen())
            {
                yield return Approach(2f, 3f);
                yield return Press(GamepadButton.West);
                yield return new WaitForSecondsRealtime(0.25f);
                if (++round % 6 == 0) yield return Press(skillButtons[(round / 6) % 3]);
                if (round % 15 == 0) yield return Capture("combat_libre_" + round);
            }
            yield return Capture("fin_mode_combat");

            // Mission 1-5 en donjon : traverse les salles, ouvre les coffres, bat le boss.
            // Le robot est invincible ici pour pouvoir vérifier tout le parcours jusqu'à la victoire.
            Log("--- Mission 1-5 (donjon) ---");
            GameSession.StartStage(StoryCatalog.Chapters[0].Stages[4]);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("mission_intro");
            yield return CaptureOverview("plan_du_donjon");
            ClickButton("PrimaryButton");
            yield return new WaitForSecondsRealtime(0.5f);
            Transform hero = PlayerTransform();
            if (hero != null) hero.GetComponent<Health>().InvulnerabilityCheck = () => true;
            yield return Capture("salle_entree");

            end = Time.realtimeSinceStartup + 180f;
            bool bossSeen = false;
            int bossShots = 0;
            bool fighting = false;
            int roomsCleared = 0;
            while (Time.realtimeSinceStartup < end && !PlayerDead() && !VictoryShown())
            {
                EnemyAI boss = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).FirstOrDefault(e => e.name.StartsWith("Boss"));
                if (boss != null && !bossSeen)
                {
                    bossSeen = true;
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Capture("boss_apparition");
                }
                if (boss != null && boss.IsWindingUp && bossShots < 3)
                {
                    bossShots++;
                    yield return Capture("boss_attaque_" + bossShots);
                }

                if (NearestEnemy(PlayerTransform()) == null)
                {
                    if (fighting)
                    {
                        fighting = false;
                        roomsCleared++;
                        yield return new WaitForSecondsRealtime(0.6f);
                        yield return Capture("salle_nettoyee_" + roomsCleared);
                    }

                    // Coffre à ouvrir en priorité, sinon la salle suivante.
                    TreasureChest chest = FindObjectsByType<TreasureChest>(FindObjectsSortMode.None).FirstOrDefault();
                    Vector3? goal = chest != null ? chest.transform.position
                        : DungeonRunController.Instance != null ? DungeonRunController.Instance.Objective : null;
                    if (goal.HasValue) yield return MoveTo(goal.Value, 0.4f);
                    else yield return null;
                    continue;
                }

                if (!fighting)
                {
                    fighting = true;
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Capture("combat_salle_" + (roomsCleared + 1));
                }

                yield return Approach(2.2f, 2f);
                yield return Press(GamepadButton.West);
                yield return new WaitForSecondsRealtime(0.3f);
                if (++round % 7 == 0) yield return Press(skillButtons[(round / 7) % 3]);
                if (round % 7 == 3) yield return Press(GamepadButton.East);
            }
            yield return Capture("fin_mission");

            Log("Test terminé.");
            File.WriteAllText(Path.Combine(outputFolder, "rapport.txt"), report.ToString());
            Application.Quit();
        }

        // ------------------------------------------------------------ Actions

        void Stick(Vector2 value)
        {
            var state = new GamepadState { leftStick = value };
            InputSystem.QueueStateEvent(pad, state);
        }

        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        /// <summary>Marche vers l'ennemi le plus proche jusqu'à "distance" mètres (au plus "timeout" secondes).</summary>
        IEnumerator Approach(float distance, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                Transform player = PlayerTransform();
                EnemyAI enemy = NearestEnemy(player);
                if (player == null || enemy == null) break;

                Vector3 to = enemy.transform.position - player.position;
                to.y = 0f;
                if (to.magnitude <= distance) break;

                // La caméra regarde vers +Z : le stick correspond directement aux axes X et Z.
                Stick(new Vector2(to.x, to.z).normalized);
                yield return null;
            }
            Stick(Vector2.zero);
            yield return null;
        }

        IEnumerator MoveTo(Vector3 target, float duration)
        {
            float end = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < end)
            {
                Transform player = PlayerTransform();
                if (player == null) break;
                Vector3 to = target - player.position;
                to.y = 0f;
                if (to.magnitude < 1f) break;
                Stick(new Vector2(to.x, to.z).normalized);
                yield return null;
            }
            Stick(Vector2.zero);
        }

        static bool ModalOpen() =>
            FindObjectsByType<ShadowBlade.UI.ModalPanel>(FindObjectsSortMode.None).Any(m => m.IsVisible);

        static bool VictoryShown() =>
            FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(t => t.isActiveAndEnabled && t.text == "Victoire");

        /// <summary>Vue du dessus de tout le donjon (caméra temporaire par-dessus la caméra de jeu).</summary>
        IEnumerator CaptureOverview(string label)
        {
            DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();
            if (generator == null) yield break;
            Bounds b = generator.Bounds;
            var go = new GameObject("OverviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.size.z, b.size.x / cam.aspect) * 0.55f;
            cam.transform.SetPositionAndRotation(b.center + Vector3.up * 80f, Quaternion.Euler(90f, 0f, 0f));
            cam.farClipPlane = 200f;
            cam.depth = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToArray();
            foreach (Canvas c in canvases) c.enabled = false; // l'interface masquerait le plan
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false; // la brume cacherait tout vu de si haut
            yield return null;
            yield return Capture(label);
            RenderSettings.fog = fog;
            foreach (Canvas c in canvases) c.enabled = true;
            Destroy(go);
            yield return null;
        }

        static void ClickButton(string name)
        {
            UnityEngine.UI.Button button = FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name && b.isActiveAndEnabled);
            if (button != null) button.onClick.Invoke();
        }

        // ------------------------------------------------------------ Captures et rapport

        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            shot++;
            string file = $"{shot:00}_{label}.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, file));
            Log($"[{file}] {DescribeState()}");
            yield return null;
        }

        string DescribeState()
        {
            var sb = new StringBuilder();
            sb.Append($"t={Time.realtimeSinceStartup:0.0}s");

            Transform player = PlayerTransform();
            if (player != null)
            {
                Health health = player.GetComponent<Health>();
                sb.Append($" | héros pos={Round(player.position)} PV={health.Current:0}/{health.Max:0}");
                Animator animator = player.GetComponentInChildren<Animator>();
                if (animator != null) sb.Append($" anim={ClipName(animator)}");
                Vector3 forward = player.forward;
                sb.Append($" regard={Round(forward)}");
            }

            EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            sb.Append($" | ennemis={enemies.Length}");
            foreach (EnemyAI enemy in enemies.Take(4))
            {
                Animator animator = enemy.GetComponentInChildren<Animator>();
                sb.Append($" [{enemy.DisplayName} PV={enemy.Health.Current:0} anim={(animator != null ? ClipName(animator) : "-")}]");
            }
            return sb.ToString();
        }

        static string ClipName(Animator animator)
        {
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            return clips.Length > 0 ? clips.OrderByDescending(c => c.weight).First().clip.name : "(aucune)";
        }

        static string Round(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";

        static Transform PlayerTransform()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }

        static bool PlayerDead()
        {
            Transform player = PlayerTransform();
            return player != null && player.GetComponent<Health>().IsDead;
        }

        static EnemyAI NearestEnemy(Transform player)
        {
            if (player == null) return null;
            return FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)
                .Where(e => !e.Health.IsDead)
                .OrderBy(e => (e.transform.position - player.position).sqrMagnitude)
                .FirstOrDefault();
        }

        void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[SelfTest] " + line);
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
                report.AppendLine($"  !! {type}: {message}");
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
