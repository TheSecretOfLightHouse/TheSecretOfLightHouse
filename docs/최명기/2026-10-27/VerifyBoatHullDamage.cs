using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Lighthouse.Player.Boat.View;

public static class VerifyBoatHullDamage
{
    public static object Main()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string[] names = { "Boat", "captainBoat", "gardenBoat", "TankerBoat" };
        int cases = 0;
        foreach (string name in names)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/" + name + ".prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            BoatHullDamage damage = instance.GetComponent<BoatHullDamage>();
            GameObject wreck = null;
            try
            {
                if (!damage) { throw new Exception(name + ": no hull damage component."); }
                BoatDurability durability = instance.GetComponent<BoatDurability>();
                typeof(BoatHullDamage).GetMethod("Awake", Private).Invoke(damage, null);
                typeof(BoatHullDamage).GetMethod("OnEnable", Private).Invoke(damage, null);
                FieldInfo current = typeof(BoatDurability).GetField("_currentDurability", Private);
                MethodInfo changed = typeof(BoatDurability).GetMethod("OnDurabilityChanged", Private);
                MethodInfo refresh = typeof(BoatHullDamage).GetMethod("LateUpdate", Private);
                MeshFilter model = instance.transform.Find("Model").GetComponent<MeshFilter>();
                Mesh intact = model.sharedMesh;
                var data = new SerializedObject(damage);
                float[] health = { 100f, 75f, 50f, 25f, 80f, 50f, 0f };
                int[] stages = { 0, 1, 2, 3, 0, 2, 3 };
                for (int i = 0; i < health.Length; i++)
                {
                    current.SetValue(durability, health[i]);
                    changed.Invoke(durability, new object[] { 100f, health[i] });
                    refresh.Invoke(damage, null);
                    Mesh expected = stages[i] == 0 ? intact
                        : (Mesh)data.FindProperty("_damageMeshes").GetArrayElementAtIndex(stages[i] - 1).objectReferenceValue;
                    if (damage.DamageStage != stages[i] || model.sharedMesh != expected)
                    {
                        throw new Exception(name + ": damage/repair stage failed at " + health[i]);
                    }
                    if (model.GetComponent<MeshRenderer>().sharedMaterials.Length != 1)
                    {
                        throw new Exception(name + ": wrong materials at stage " + stages[i]);
                    }
                    if (model.sharedMesh.GetTriangles(0).Length != intact.GetTriangles(0).Length)
                    {
                        throw new Exception(name + ": damage removed hull triangles.");
                    }
                    if (model.sharedMesh.normals.Any(n => n.sqrMagnitude < 0.5f))
                    {
                        throw new Exception(name + ": invalid deformation normals.");
                    }
                    cases++;
                }

                // Polling also handles a late initial snapshot without a durability event.
                current.SetValue(durability, 75f);
                refresh.Invoke(damage, null);
                if (damage.DamageStage != 1 || model.sharedMesh != data.FindProperty("_damageMeshes").GetArrayElementAtIndex(0).objectReferenceValue)
                {
                    throw new Exception(name + ": late refresh failed.");
                }

                var controller = new SerializedObject(instance.GetComponent<BoatController>());
                BoatWreckEffect effectPrefab = (BoatWreckEffect)controller.FindProperty("_wreckEffectPrefab").objectReferenceValue;
                if (!effectPrefab) { throw new Exception(name + ": no wreck effect."); }
                wreck = UnityEngine.Object.Instantiate(effectPrefab.gameObject);
                BoatWreckEffect effect = wreck.GetComponent<BoatWreckEffect>();
                effect.SetHullDamageStage(3);
                effect.Initialize(0f);
                Mesh bow = wreck.transform.Find("Bow").GetComponent<MeshFilter>().sharedMesh;
                var effectData = new SerializedObject(effectPrefab);
                if (bow != effectData.FindProperty("_damagedBowMeshes").GetArrayElementAtIndex(2).objectReferenceValue)
                {
                    throw new Exception(name + ": damage reset on wreck.");
                }
                MethodInfo pose = typeof(BoatWreckEffect).GetMethod("ApplyPose", Private);
                pose.Invoke(effect, new object[] { 0.7f });
                foreach (BoatWreckPiece piece in wreck.GetComponentsInChildren<BoatWreckPiece>(true).Take(8))
                {
                    var pieceData = new SerializedObject(piece);
                    if (!pieceData.FindProperty("_impulseRelease").boolValue
                        || pieceData.FindProperty("_releaseTime").floatValue > 0.016f
                        || pieceData.FindProperty("_releaseDuration").floatValue > 0.23f
                        || Mathf.Abs(pieceData.FindProperty("_displacement").vector3Value.x) > 0.04f)
                    {
                        throw new Exception(name + ": immediate simultaneous break mismatch.");
                    }
                }
                pose.Invoke(effect, new object[] { 30f });
                if (wreck.transform.Find("Bow").position.y > -1f || wreck.transform.Find("Stern").position.y > -1f)
                {
                    throw new Exception(name + ": hull did not sink.");
                }
                if (!float.IsPositiveInfinity(effect.Duration)) { throw new Exception(name + ": floating timber expires."); }
                foreach (MeshFilter filter in wreck.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh || filter.sharedMesh.vertexCount == 0) { throw new Exception(name + ": missing mesh."); }
                }
                foreach (Transform child in wreck.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        throw new Exception(name + ": missing script.");
                    }
                }
            }
            finally
            {
                if (damage) { typeof(BoatHullDamage).GetMethod("OnDisable", Private).Invoke(damage, null); }
                UnityEngine.Object.DestroyImmediate(instance);
                if (wreck) { UnityEngine.Object.DestroyImmediate(wreck); }
            }
        }
        return new { boatVariants = names.Length, damageAndRepairCases = cases,
            lateSnapshot = true, wreckKeepsDamagedBow = true, simultaneousFastBreak = true, hullTrianglesPreserved = true,
            hullSinks = true, floatingTimberPreserved = true, playModeTested = false };
    }
}
