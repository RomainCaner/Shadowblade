using UnityEngine;

namespace ShadowBlade.Level
{
    /// <summary>
    /// Pose des modèles de décor en se basant sur leur volume visible :
    /// peu importe leur point d'origine, ils sont centrés et posés au sol (ou au plafond voulu).
    /// </summary>
    public static class EnvironmentPlacer
    {
        public enum Anchor { Bottom, Top }

        public static GameObject Place(GameObject prefab, Vector3 target, float yaw, Vector3 scale, Transform parent,
            Material material, Anchor anchor = Anchor.Bottom)
        {
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale = scale;
            if (material != null)
            {
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    r.sharedMaterials = materials;
                }
            }

            Bounds bounds = CombinedBounds(go);
            float y = anchor == Anchor.Bottom ? target.y - bounds.min.y : target.y - bounds.max.y;
            go.transform.position += new Vector3(target.x - bounds.center.x, y, target.z - bounds.center.z);
            return go;
        }

        public static GameObject Place(GameObject prefab, Vector3 target, float yaw, float scale, Transform parent,
            Material material, Anchor anchor = Anchor.Bottom) =>
            Place(prefab, target, yaw, Vector3.one * scale, parent, material, anchor);

        public static Bounds CombinedBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Boîte de collision invisible (le NavMesh et le héros s'y arrêtent).</summary>
        public static BoxCollider AddBox(Transform parent, Vector3 center, Vector3 size, string name = "Collider")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }
    }
}
