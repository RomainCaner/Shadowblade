using System.Collections;
using ShadowBlade.Core;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Déroulement d'une mission en donjon : on traverse les salles une à une.
    /// En entrant dans une salle, les grilles se referment et les vagues arrivent ;
    /// une fois la salle nettoyée, les grilles s'ouvrent, un coffre peut apparaître
    /// et une flèche indique la suite. La dernière salle est celle du boss (s'il y en a un).
    /// </summary>
    public class DungeonRunController : GameControllerBase
    {
        [SerializeField] DungeonGenerator generator;
        [SerializeField] GuideArrow guideArrow;
        [Tooltip("Distance aux murs à partir de laquelle on considère que le héros est entré dans la salle.")]
        [SerializeField] float entryMargin = 2.5f;
        [SerializeField] int treasureGold = 30;

        public static DungeonRunController Instance { get; private set; }

        /// <summary>Endroit où le héros doit aller (salle suivante), ou null pendant un combat.</summary>
        public Vector3? Objective => guideArrow != null ? guideArrow.Target : null;

        StageDefinition stage;
        int roomIndex = 1;      // prochaine salle à nettoyer (0 = entrée)
        bool inCombat;
        int waveNumber;
        int waveCount;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        protected override void Start()
        {
            base.Start();
            stage = generator.Stage;
            player.transform.position = generator.PlayerStart;
            Physics.SyncTransforms();
            UpdateObjective();
            UpdateHud();

            SetPaused(true);
            modal.Show(stage.Code + "  " + stage.Title, stage.Intro, "Combattre", () =>
            {
                modal.Hide();
                SetPaused(false);
            }, "Retour", GameSession.ReturnToMenu);
        }

        void Update()
        {
            if (finished || inCombat || roomIndex >= generator.Rooms.Count) return;
            DungeonRoom room = generator.Rooms[roomIndex];
            if (room.Contains(player.transform.position, entryMargin)) StartCoroutine(Encounter(room));
        }

        IEnumerator Encounter(DungeonRoom room)
        {
            inCombat = true;
            UpdateObjective();
            room.Lock();

            if (room.IsBoss)
            {
                waveNumber = 1;
                waveCount = 1;
                UpdateHud();
                SetMessage(stage.BossName);
                yield return new WaitForSeconds(delayBeforeWave);
                SetMessage(null);
                if (finished) yield break;

                SpawnBoss(room.SpawnPoints[4]);
                for (int i = 0; i < stage.BossMinions; i++) Spawn(enemyPrefab, room.SpawnPoints[i]);
                UpdateHud();
                yield return WaitUntilCleared();
            }
            else
            {
                int[] waves = room.Definition.Waves;
                waveCount = waves.Length;
                for (int w = 0; w < waves.Length && !finished; w++)
                {
                    waveNumber = w + 1;
                    UpdateHud();
                    SetMessage(waves.Length > 1 ? $"Vague {w + 1} / {waves.Length}" : "Combat !");
                    yield return new WaitForSeconds(w == 0 ? 1f : delayBeforeWave);
                    SetMessage(null);
                    if (finished) yield break;

                    for (int i = 0; i < waves[w]; i++) Spawn(enemyPrefab, room.SpawnPoints[i % room.SpawnPoints.Length]);
                    UpdateHud();
                    yield return WaitUntilCleared();
                }
            }
            if (finished) yield break;

            room.Unlock();
            inCombat = false;
            roomIndex++;

            if (room.Definition != null && room.Definition.Treasure)
            {
                TreasureChest chest = generator.SpawnChest(room, player.transform, treasureGold);
                if (chest != null)
                {
                    chest.Opened += c =>
                    {
                        AddGold(c.Gold);
                        FlashMessage($"Trésor !  +{c.Gold} or", 1.5f);
                    };
                }
            }

            if (roomIndex >= generator.Rooms.Count)
            {
                StartCoroutine(Victory());
                yield break;
            }

            FlashMessage("Salle nettoyée !", 1.5f);
            UpdateObjective();
            UpdateHud();
        }

        void UpdateObjective()
        {
            if (guideArrow == null) return;
            guideArrow.Target = !inCombat && roomIndex < generator.Rooms.Count
                ? generator.Rooms[roomIndex].Center
                : (Vector3?)null;
        }

        IEnumerator Victory()
        {
            finished = true;
            SetMessage("Victoire !");
            yield return new WaitForSeconds(resultDelay);
            SetMessage(null);

            PlayerProgress.CompleteStage(stage);
            PlayerProgress.AddGold(goldEarned);
            SetPaused(true);
            modal.Show("Victoire", $"{stage.Code}  {stage.Title} : mission accomplie !\n\nOr gagné : {goldEarned}",
                "Continuer", GameSession.ReturnToMenu, "Rejouer", GameSession.Restart);
        }

        protected override void ShowDefeat()
        {
            modal.Show("Défaite", $"{stage.Code}  {stage.Title}\nLe combat est perdu... mais l'Ordre ne renonce jamais.\n\nOr gagné : {goldEarned}",
                "Réessayer", GameSession.Restart, "Menu", GameSession.ReturnToMenu);
        }

        protected override string PauseDescription => $"{stage.Code}  {stage.Title}";

        protected override void UpdateHud()
        {
            if (stage == null) return;
            int totalRooms = generator.Rooms.Count - 1;
            int current = Mathf.Clamp(roomIndex, 1, totalRooms);
            string detail = inCombat
                ? (waveCount > 1 ? $"Vague {waveNumber}/{waveCount}   •   Ennemis : {alive}" : $"Ennemis : {alive}")
                : "Avancez !";
            string roomLabel = generator.Rooms[current].IsBoss ? "Salle du boss" : $"Salle {current}/{totalRooms}";
            if (statusText != null) statusText.text = $"Mission {stage.Code}   •   {roomLabel}   •   {detail}";
            if (goldText != null) goldText.text = $"Or  +{goldEarned}";
        }
    }
}
