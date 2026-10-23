using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using ShadowBlade.Combat;
using ShadowBlade.Core;
using ShadowBlade.Enemies;
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
            yield return new WaitForSeconds(0.8f);
            yield return Capture("course");
            Stick(new Vector2(1f, 0f));
            yield return new WaitForSeconds(0.6f);
            Stick(Vector2.zero);

            // Attendre que les squelettes sortent de terre.
            yield return new WaitForSeconds(1.2f);
            yield return Capture("apparition_ennemis");
            yield return new WaitForSeconds(1.5f);

            // Aller au contact et enchaîner le combo.
            yield return Approach(2f, 5f);
            yield return Capture("au_contact");
            for (int i = 0; i < 3; i++)
            {
                yield return Press(GamepadButton.West);
                yield return new WaitForSeconds(0.2f);
                yield return Capture($"combo_{i + 1}");
            }
            yield return new WaitForSeconds(0.6f);

            // Compétences.
            string[] skillNames = { "charge", "tourbillon", "frappe_sismique" };
            GamepadButton[] skillButtons = { GamepadButton.North, GamepadButton.LeftShoulder, GamepadButton.RightShoulder };
            for (int i = 0; i < skillButtons.Length; i++)
            {
                yield return Approach(2.5f, 4f);
                yield return Press(skillButtons[i]);
                yield return new WaitForSeconds(i == 1 ? 0.6f : 0.25f);
                yield return Capture("competence_" + skillNames[i]);
                yield return new WaitForSeconds(1.2f);
            }

            // Roulade.
            Stick(new Vector2(-1f, 0f));
            yield return Press(GamepadButton.East);
            yield return new WaitForSeconds(0.15f);
            yield return Capture("roulade");
            Stick(Vector2.zero);

            // Laisser les ennemis attaquer (cercle d'avertissement).
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.9f);
                yield return Capture("ennemis_attaquent_" + (i + 1));
            }

            // Combat libre pendant 20 secondes : le robot frappe et utilise ses compétences.
            float end = Time.time + 20f;
            int round = 0;
            while (Time.time < end && !PlayerDead())
            {
                yield return Approach(2f, 3f);
                yield return Press(GamepadButton.West);
                yield return new WaitForSeconds(0.25f);
                if (++round % 6 == 0) yield return Press(skillButtons[(round / 6) % 3]);
                if (round % 15 == 0) yield return Capture("combat_libre_" + round);
            }
            yield return Capture("fin_mode_combat");

            // Mission 1-5 (boss) : valide l'intro, élimine la vague puis affronte le boss.
            Log("--- Mission 1-5 ---");
            GameSession.StartStage(StoryCatalog.Chapters[0].Stages[4]);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("mission_intro");
            ClickButton("PrimaryButton");
            yield return new WaitForSecondsRealtime(0.5f);

            end = Time.time + 60f;
            bool bossSeen = false;
            int bossShots = 0;
            while (Time.time < end && !PlayerDead())
            {
                EnemyAI boss = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).FirstOrDefault(e => e.name.StartsWith("Boss"));
                if (boss != null && !bossSeen)
                {
                    bossSeen = true;
                    yield return new WaitForSeconds(1f);
                    yield return Capture("boss_apparition");
                }
                if (boss != null && boss.IsWindingUp && bossShots < 3)
                {
                    bossShots++;
                    yield return Capture("boss_attaque_" + bossShots);
                }

                yield return Approach(2.2f, 2f);
                yield return Press(GamepadButton.West);
                yield return new WaitForSeconds(0.3f);
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
            float end = Time.time + timeout;
            while (Time.time < end)
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
            sb.Append($"t={Time.time:0.0}s");

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
