using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lighthouse.Workbench.Editor
{
    // Puts the craft effects (sparks, dust and SoundManager audio) on the workbench in the open scene (2026-10-27).
    // They sit on top of the bench's model, built from code so no particle asset is needed; the material is the URP
    // package's own particle material. Running it again rebuilds them in place. The shared Workbench prefab is left
    // alone until it is decided to carry them. Only the open scene changes, and nothing is saved.
    public static class WorkbenchCraftEffectsSetup
    {
        private const string RootName = "CraftEffects";
        private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        private const float VisibleDistance = 30f;

        [MenuItem("Lighthouse/Workbench/Add Craft Effects In Open Scene")]
        public static void SetUp()
        {
            WorkbenchNet bench = Object.FindFirstObjectByType<WorkbenchNet>(FindObjectsInactive.Include);

            if (!bench)
            {
                Debug.LogError($"[{nameof(WorkbenchCraftEffectsSetup)}] No {nameof(WorkbenchNet)} in the open scene.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);

            if (!material)
            {
                Debug.LogError($"[{nameof(WorkbenchCraftEffectsSetup)}] No particle material at {ParticleMaterialPath}.");
                return;
            }

            Undo.SetCurrentGroupName("Add Workbench Craft Effects");
            int group = Undo.GetCurrentGroup();
            Transform old = bench.transform.Find(RootName);

            if (old)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create craft effects");
            Undo.SetTransformParent(root.transform, bench.transform, "Parent craft effects");
            root.transform.position = GetTopCenter(bench);
            root.transform.rotation = Quaternion.identity;

            ParticleSystem sparks = CreateSparks(root.transform, material);
            ParticleSystem dust = CreateDust(root.transform, material);
            WorkbenchCraftEffects effects = root.AddComponent<WorkbenchCraftEffects>();
            SerializedObject serialized = new SerializedObject(effects);
            serialized.FindProperty("_workbench").objectReferenceValue = bench;
            serialized.FindProperty("_sparks").objectReferenceValue = sparks;
            serialized.FindProperty("_dust").objectReferenceValue = dust;
            serialized.FindProperty("_soundManagerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Lighthouse.Systems.Audio.SoundManager>(
                "Assets/02.Prefabs/Player/PlayerSoundManager.prefab");
            serialized.FindProperty("_hammerClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/08.Audio/Workbench/Pickaxe_Ring_A15.wav");
            serialized.FindProperty("_visibleDistance").floatValue = VisibleDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            Selection.activeObject = root;
            Debug.Log($"[{nameof(WorkbenchCraftEffectsSetup)}] Craft effects added under {bench.name}.");
        }

        // Three quick bursts, like hammer blows, of short orange streaks thrown up and falling back.
        private static ParticleSystem CreateSparks(Transform parent, Material material)
        {
            ParticleSystem system = CreateSystem(parent, "Sparks", material);
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.15f));
            main.gravityModifier = 1.2f;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 14),
                new ParticleSystem.Burst(0.25f, 12),
                new ParticleSystem.Burst(0.5f, 16)
            });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;
            // Cones point along local Z; turned to throw upward.
            shape.rotation = new Vector3(-90f, 0f, 0f);

            SetFadeOut(system, Color.white);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            return system;
        }

        // One soft puff of wood dust rising and spreading over the bench top.
        private static ParticleSystem CreateDust(Transform parent, Material material)
        {
            ParticleSystem system = CreateSystem(parent, "Dust", material);
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(0.62f, 0.55f, 0.45f, 0.45f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 30;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            SetFadeOut(system, Color.white);
            return system;
        }

        private static ParticleSystem CreateSystem(Transform parent, string name, Material material)
        {
            GameObject item = new GameObject(name, typeof(ParticleSystem));
            item.transform.SetParent(parent, false);
            ParticleSystem system = item.GetComponent<ParticleSystem>();
            // Adding the component starts it playing in the editor; stop before changing the duration.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            item.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return system;
        }

        private static void SetFadeOut(ParticleSystem system, Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;
        }

        private static Vector3 GetTopCenter(WorkbenchNet bench)
        {
            Renderer[] renderers = bench.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return bench.transform.position;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }
}
