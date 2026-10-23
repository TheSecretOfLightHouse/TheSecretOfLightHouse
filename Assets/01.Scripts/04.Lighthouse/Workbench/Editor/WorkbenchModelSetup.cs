using Lighthouse.Dev.Tower;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lighthouse.Workbench.Editor
{
    // Swaps the plain bench block for the wooden workbench model placed in the open scene (2026-10-23).
    // Range checks are taken from the bench root, so the root moves under the model and the model becomes its child;
    // the rack follows the root. The model takes over the box's place in the trial panel and gets mesh colliders,
    // and the old box and the root's box collider go.
    // Only the open scene changes, and nothing is saved: look at it, then save the scene yourself.
    public static class WorkbenchModelSetup
    {
        private const string ModelName = "wooden_workbench_3d_model_low";
        private const string BoxName = "WorkbenchBox";

        [MenuItem("Lighthouse/Workbench/Use Wooden Model In Open Scene")]
        public static void SetUp()
        {
            WorkbenchNet bench = Object.FindFirstObjectByType<WorkbenchNet>(FindObjectsInactive.Include);

            if (!bench)
            {
                Debug.LogError($"[{nameof(WorkbenchModelSetup)}] No {nameof(WorkbenchNet)} in the open scene.");
                return;
            }

            GameObject model = FindModel(bench);

            if (!model)
            {
                Debug.LogError($"[{nameof(WorkbenchModelSetup)}] No {ModelName} in the open scene. Place the model first.");
                return;
            }

            MeshFilter[] meshes = model.GetComponentsInChildren<MeshFilter>(true);

            if (meshes.Length == 0)
            {
                Debug.LogError($"[{nameof(WorkbenchModelSetup)}] {ModelName} has no mesh.");
                return;
            }

            Undo.SetCurrentGroupName("Use Wooden Workbench Model");
            int group = Undo.GetCurrentGroup();
            Transform benchTransform = bench.transform;

            if (model.transform.parent != benchTransform)
            {
                Bounds bounds = GetBounds(model);
                // The rack stands 0.5 below the root (the old block's floor), so keep that offset above the model's floor.
                Vector3 root = new Vector3(bounds.center.x, bounds.min.y + 0.5f, bounds.center.z);

                Undo.RecordObject(benchTransform, "Move bench root to model");
                benchTransform.position = root;
                Undo.SetTransformParent(model.transform, benchTransform, "Parent model to bench");
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                if (!meshes[i].GetComponent<Collider>())
                {
                    MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(meshes[i].gameObject);
                    meshCollider.sharedMesh = meshes[i].sharedMesh;
                }
            }

            BoxCollider benchCollider = bench.GetComponent<BoxCollider>();

            if (benchCollider)
            {
                Undo.DestroyObjectImmediate(benchCollider);
            }

            Transform box = benchTransform.Find(BoxName);

            if (box)
            {
                Undo.DestroyObjectImmediate(box.gameObject);
            }

            // Screen mode shows the model where the box stood; rack mode still swaps it for the rack.
            foreach (TowerTrialPanel trialPanel in Object.FindObjectsByType<TowerTrialPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject trialSerialized = new SerializedObject(trialPanel);
                trialSerialized.FindProperty("_screenBox").objectReferenceValue = model;
                trialSerialized.ApplyModifiedProperties();
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(benchTransform);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            Selection.activeObject = model;
            LogRangeFit(bench, model);
        }

        private static GameObject FindModel(WorkbenchNet bench)
        {
            Transform child = bench.transform.Find(ModelName);

            if (child)
            {
                return child.gameObject;
            }

            foreach (GameObject root in bench.gameObject.scene.GetRootGameObjects())
            {
                if (root.name == ModelName)
                {
                    return root;
                }
            }

            return null;
        }

        private static Bounds GetBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return new Bounds(model.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        // Range is measured from the root at the model's center, so a long bench can leave its ends out of reach.
        private static void LogRangeFit(WorkbenchNet bench, GameObject model)
        {
            Bounds bounds = GetBounds(model);
            float halfLength = Mathf.Max(bounds.extents.x, bounds.extents.z);
            InteractionTarget target = bench.GetComponent<InteractionTarget>();
            string range = target ? target.Range.ToString("0.0") : "-";

            Debug.Log($"[{nameof(WorkbenchModelSetup)}] {ModelName} is now the bench on {bench.name}. "
                + $"Half length {halfLength:0.00} m, interaction range {range} m. Save the scene to keep it.");
        }
    }
}
