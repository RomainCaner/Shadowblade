using Unity.AI.Navigation;
using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Calcule le NavMesh (zones où les ennemis peuvent marcher) au lancement de la scène.
    /// Rapide pour une petite arène, et toujours à jour si on déplace les obstacles.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        void Awake() => GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
