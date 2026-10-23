namespace ShadowBlade.Core
{
    public class StageDefinition
    {
        /// <summary>Position de la mission dans toute l'histoire (sert à la sauvegarde).</summary>
        public int GlobalIndex;
        public string Code;
        public string Title;
        public string Intro;
        /// <summary>Nombre d'ennemis par vague.</summary>
        public int[] Waves;
        /// <summary>Si vrai, un boss apparaît après la dernière vague.</summary>
        public bool HasBoss;
    }

    public class ChapterDefinition
    {
        public string Title;
        public string Subtitle;
        public StageDefinition[] Stages;
    }

    /// <summary>
    /// Contenu du mode Histoire. Pour ajouter une mission ou un chapitre, il suffit de l'ajouter ici.
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
                            "tu te relèves parmi les cendres.\nRepousse les goules qui rôdent autour du campement.",
                            2, 3),
                        Stage("1-2", "La route des cendres",
                            "La route vers la citadelle est infestée. Les créatures sont de plus en plus nombreuses " +
                            "à mesure que la nuit tombe.",
                            3, 3, 4),
                        Stage("1-3", "Embuscade",
                            "Un piège ! Les goules surgissent de toutes parts.\nUtilise tes compétences pour t'en sortir.",
                            4, 5),
                        Stage("1-4", "Les remparts",
                            "Les remparts de la citadelle sont en vue. Quelque chose de puissant attend derrière les portes...",
                            3, 4, 5),
                        Boss(Stage("1-5", "Le Seigneur des Cendres",
                            "Le Seigneur des Cendres en personne bloque le passage.\n" +
                            "Esquive ses frappes dévastatrices quand le grand cercle apparaît, et abats-le !",
                            3)),
                    },
                },
            };

            int index = 0;
            foreach (ChapterDefinition chapter in chapters)
                foreach (StageDefinition stage in chapter.Stages)
                    stage.GlobalIndex = index++;
            return chapters;
        }

        static StageDefinition Stage(string code, string title, string intro, params int[] waves) =>
            new StageDefinition { Code = code, Title = title, Intro = intro, Waves = waves };

        static StageDefinition Boss(StageDefinition stage)
        {
            stage.HasBoss = true;
            return stage;
        }
    }
}
