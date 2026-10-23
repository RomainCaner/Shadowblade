namespace ShadowBlade.Core
{
    /// <summary>Une salle de combat du donjon : ses vagues d'ennemis et un éventuel coffre.</summary>
    public class RoomDefinition
    {
        /// <summary>Nombre d'ennemis par vague (vide = salle sans combat).</summary>
        public int[] Waves = new int[0];
        /// <summary>Un coffre au trésor apparaît quand la salle est nettoyée.</summary>
        public bool Treasure;
        /// <summary>Taille de la salle en segments de mur (impair).</summary>
        public int Size = 7;
    }

    public class StageDefinition
    {
        /// <summary>Position de la mission dans toute l'histoire (sert à la sauvegarde).</summary>
        public int GlobalIndex;
        public string Code;
        public string Title;
        public string Intro;
        /// <summary>Salles de combat, dans l'ordre du parcours (après la salle d'entrée).</summary>
        public RoomDefinition[] Rooms;
        /// <summary>Si renseigné, la mission se termine dans la salle du boss.</summary>
        public string BossName;
        public int BossMinions = 2;
        public bool HasBoss => !string.IsNullOrEmpty(BossName);
        /// <summary>Graine du plan : le même donjon à chaque partie de cette mission.</summary>
        public int Seed => GlobalIndex * 97 + 13;
        public int RoomCount => Rooms.Length + (HasBoss ? 1 : 0);
    }

    public class ChapterDefinition
    {
        public string Title;
        public string Subtitle;
        public StageDefinition[] Stages;
    }

    /// <summary>
    /// Contenu du mode Histoire. Pour ajouter une mission ou un chapitre, il suffit de l'ajouter ici :
    /// le donjon (salles, couloirs, décor) est généré à partir de ces données.
    /// </summary>
    public static class StoryCatalog
    {
        public static readonly ChapterDefinition[] Chapters = Build();

        static ChapterDefinition[] Build()
        {
            var chapters = new[]
            {
                new ChapterDefinition
                {
                    Title = "Chapitre 1",
                    Subtitle = "Les Terres Brûlées",
                    Stages = new[]
                    {
                        Stage("1-1", "L'éveil",
                            "Les Ombres ont englouti le royaume d'Astrelle. Dernier survivant de l'Ordre de la Lame, " +
                            "tu te relèves dans les ruines de la forteresse.\nTraverse les salles et repousse les goules.",
                            Room(2), Room(3, 3, treasure: true)),
                        Stage("1-2", "La route des cendres",
                            "Les souterrains mènent vers la citadelle. Les créatures sont de plus en plus nombreuses " +
                            "à mesure que tu avances.",
                            Room(3), Room(3, 4), Room(4, 4, treasure: true)),
                        Stage("1-3", "Embuscade",
                            "Un piège ! Les grilles se referment derrière toi et les goules surgissent de partout.\n" +
                            "Utilise tes compétences pour t'en sortir.",
                            Room(2, 3), Room(4, 4, 3), Room(3, 5, treasure: true)),
                        Stage("1-4", "Les remparts",
                            "Les remparts de la citadelle sont en vue. Quelque chose de puissant attend derrière les portes...",
                            Room(3, 3), Room(4, 4), Room(3, 4, 5), Room(5, 5, treasure: true)),
                        Boss(Stage("1-5", "Le Seigneur des Cendres",
                            "Le Seigneur des Cendres attend dans la grande salle du trône.\n" +
                            "Esquive ses frappes dévastatrices quand le grand cercle apparaît, et abats-le !",
                            Room(3), Room(3, 4, treasure: true)), "Seigneur des Cendres"),
                    },
                },
            };

            int index = 0;
            foreach (ChapterDefinition chapter in chapters)
                foreach (StageDefinition stage in chapter.Stages)
                    stage.GlobalIndex = index++;
            return chapters;
        }

        static StageDefinition Stage(string code, string title, string intro, params RoomDefinition[] rooms) =>
            new StageDefinition { Code = code, Title = title, Intro = intro, Rooms = rooms };

        static RoomDefinition Room(params int[] waves) => new RoomDefinition { Waves = waves };

        static RoomDefinition Room(int wave1, int wave2, bool treasure) =>
            new RoomDefinition { Waves = new[] { wave1, wave2 }, Treasure = treasure };

        static RoomDefinition Room(int wave1, int wave2, int wave3, bool treasure) =>
            new RoomDefinition { Waves = new[] { wave1, wave2, wave3 }, Treasure = treasure };

        static StageDefinition Boss(StageDefinition stage, string bossName)
        {
            stage.BossName = bossName;
            return stage;
        }
    }
}
