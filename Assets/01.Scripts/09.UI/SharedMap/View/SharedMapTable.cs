using UnityEngine;

namespace Lighthouse.UI.SharedMap.View
{
    // Optional authored anchor. Without one the runtime places its own table near the lighthouse centre.
    public sealed class SharedMapTable : MonoBehaviour
    {
        public static GameObject CreateVisual(Vector3 position)
        {
            var root = new GameObject("SharedMapTableVisual");
            root.transform.position = position;
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "MapTable";
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = Vector3.up * 0.85f;
            top.transform.localScale = new Vector3(1.8f, 0.12f, 1.3f);
            // The table is a local visual; collision and eligibility must not differ on a headless host.
            Object.Destroy(top.GetComponent<Collider>());
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "MapPaper";
            paper.transform.SetParent(root.transform, false);
            paper.transform.localPosition = Vector3.up * 0.92f;
            paper.transform.localScale = new Vector3(1.5f, 0.02f, 1f);
            Object.Destroy(paper.GetComponent<Collider>());
            return root;
        }
    }
}
