using System.Collections.Generic;
using ShadowBlade.Core;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>Une salle générée : sa position, ses grilles, ses points d'apparition et ses vagues.</summary>
    public class DungeonRoom
    {
        public int Index;
        public Vector3 Center;
        /// <summary>Demi-largeur (X) et demi-profondeur (Z) en mètres.</summary>
        public Vector2 HalfSize;
        public RoomDefinition Definition;
        public bool IsEntry;
        public bool IsBoss;
        public Vector3 EntryDirection;   // vers l'extérieur, côté par lequel on arrive (zéro pour l'entrée)
        public Vector3 ExitDirection;    // vers l'extérieur, côté par lequel on repart (zéro pour la dernière)
        public Vector3[] SpawnPoints;
        public Vector3 TreasurePoint;
        public readonly List<DungeonGate> Gates = new List<DungeonGate>();

        public bool HasCombat => IsBoss || (Definition != null && Definition.Waves.Length > 0);

        /// <summary>Le point est dans la salle, à au moins "margin" mètres des murs.</summary>
        public bool Contains(Vector3 point, float margin) =>
            Mathf.Abs(point.x - Center.x) < HalfSize.x - margin && Mathf.Abs(point.z - Center.z) < HalfSize.y - margin;

        public Vector3 EdgePoint(Vector3 direction) =>
            Center + new Vector3(direction.x * HalfSize.x, 0f, direction.z * HalfSize.y);

        public void Lock()
        {
            foreach (DungeonGate gate in Gates) gate.Close();
        }

        public void Unlock()
        {
            foreach (DungeonGate gate in Gates) gate.Open();
        }
    }
}
