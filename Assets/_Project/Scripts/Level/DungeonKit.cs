using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Pièces de décor utilisées pour générer les donjons du mode Histoire (modèles KayKit Dungeon).
    /// Créé et rempli automatiquement par l'outil "Tout construire".
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowBlade/Kit de donjon")]
    public class DungeonKit : ScriptableObject
    {
        [Header("Échelle")]
        [Tooltip("Échelle appliquée à toutes les pièces (la même que les personnages).")]
        public float scale = 1f;
        [Tooltip("Longueur d'un segment de mur après mise à l'échelle, en mètres.")]
        public float segment = 3f;
        public float wallHeight = 3f;
        [Tooltip("Côté d'une dalle de sol après mise à l'échelle, en mètres.")]
        public float floorTileSize = 3f;
        [Tooltip("Vrai si le mur est modélisé dans la longueur selon X (sinon selon Z).")]
        public bool wallAlongX = true;
        public Material material;

        [Header("Structure")]
        public GameObject floorTile;
        public GameObject[] walls;
        public GameObject wallHalf;
        public GameObject pillar;
        public GameObject bigPillar;
        public GameObject gate;

        [Header("Décor")]
        public GameObject torch;
        public GameObject[] banners;
        public GameObject bossBanner;
        public GameObject[] props;
        public GameObject[] wallDecor;
        public GameObject chest;
        public GameObject bossTreasure;
        public GameObject candles;
    }
}
