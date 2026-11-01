using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.View;
using UnityEditor;
using UnityEngine;

namespace Lighthouse.EditorTools.Ocean
{
    public static class WaterSplashPrefabBuilder
    {
        public const string PrefabFolder = "Assets/02.Prefabs/World/Splash";
        public const string ImpactPrefabPath = PrefabFolder + "/WaterSplash_ImpactColumn.prefab";
        public const string MuzzlePrefabPath = PrefabFolder + "/WaterSplash_MuzzleSpray.prefab";

        private static readonly Vector3 Up = new Vector3(-90f, 0f, 0f);
        private static readonly Color WaterTint = new Color(0.62f, 0.9f, 0.92f, 0.85f);
        private static readonly Color FoamTint = new Color(1f, 1f, 1f, 0.9f);

        [MenuItem("TheLightHouse/World/Water/Build Splash Effect Prefabs")]
        public static void Build()
        {
            Material body = WaterSplashAssetFactory.BuildBodyMaterial();
            Material soft = WaterSplashAssetFactory.BuildSoftMaterial();
            Material ring = WaterSplashAssetFactory.BuildRingMaterial();
            if (body == null || soft == null || ring == null)
            {
                return;
            }
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/02.Prefabs/World", "Splash");
            }
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            SavePrefab(ImpactPrefabPath, "WaterSplash_ImpactColumn", WaterSplashKind.ImpactColumn, new Vector2(0.9f, 2f), root =>
            {
                var column = CreateColumn(root, "Column", body, BuildBaseMesh(sphere));
                var drops = CreateStreaks(root, "Drops", soft, 34, 0.04f, new Vector2(0.9f, 1.4f), new Vector2(4f, 8f), new Vector2(0.07f, 0.15f), 1.1f, WaterTint);
                ConfigureCone(drops, 30f, 0.25f);
                var fall = CreateStreaks(root, "Fall", soft, 16, 0.4f, new Vector2(0.5f, 0.8f), new Vector2(0f, 0.3f), new Vector2(0.06f, 0.12f), 1.6f, WaterTint);
                ConfigureDisc(fall, 0.7f, 2f);
                var foam = CreatePuff(root, "Foam", soft, 12, new Vector2(0.6f, 1f), new Vector2(0.8f, 2.2f), new Vector2(0.35f, 0.8f), 0.35f);
                var ripple = CreateRipple(root, "Ripple", ring, 1.1f, 0.8f, 5f);
                return new[] { column, drops, fall, foam, ripple };
            });
            SavePrefab(MuzzlePrefabPath, "WaterSplash_MuzzleSpray", WaterSplashKind.MuzzleSpray, new Vector2(0.7f, 1.3f), root =>
            {
                var mist = CreateStreaks(root, "Mist", soft, 20, 0f, new Vector2(0.4f, 0.7f), new Vector2(1.5f, 3f), new Vector2(0.05f, 0.12f), 1.2f, WaterTint);
                ConfigureCone(mist, 40f, 0.2f);
                var foam = CreatePuff(root, "Foam", soft, 8, new Vector2(0.4f, 0.7f), new Vector2(0.5f, 1.4f), new Vector2(0.25f, 0.55f), 0.2f);
                var ripple = CreateRipple(root, "Ripple", ring, 0.8f, 0.5f, 4f);
                return new[] { mist, foam, ripple };
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(WaterSplashPrefabBuilder)}] Built splash prefabs.");
        }

        private delegate ParticleSystem[] SystemFactory(Transform root);

        private static void SavePrefab(string path, string name, WaterSplashKind kind, Vector2 scaleRange, SystemFactory factory)
        {
            var root = new GameObject(name);
            var effect = root.AddComponent<WaterSplashEffect>();
            ParticleSystem[] systems = factory(root.transform);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_kind").intValue = (int)kind;
            serialized.FindProperty("_scaleRange").vector2Value = scaleRange;
            SerializedProperty list = serialized.FindProperty("_systems");
            list.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static ParticleSystem CreateBase(Transform parent, string name, Material material, ParticleSystemRenderMode mode, int maxParticles, float burstTime, int burstCount)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(burstTime, (short)burstCount) });
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return system;
        }

        private static ParticleSystem CreateMeshBlob(Transform parent, string name, Material material, Mesh mesh, Vector3 size, float speed, float lifetime)
        {
            ParticleSystem system = CreateBase(parent, name, material, ParticleSystemRenderMode.Mesh, 2, 0f, 1);
            system.GetComponent<ParticleSystemRenderer>().mesh = mesh;
            system.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.gravityModifier = 1f;
            main.startSize3D = true;
            main.startSizeX = size.x;
            main.startSizeY = size.y;
            main.startSizeZ = size.z;
            main.startColor = new Color(0.55f, 0.92f, 0.94f, 0.75f);
            ConfigureCone(system, 2f, 0.02f);
            ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.separateAxes = true;
            sizeModule.x = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.35f, 0.3f, 1f, 1f, 0.4f));
            sizeModule.z = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.35f, 0.3f, 1f, 1f, 0.4f));
            sizeModule.y = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.2f, 0.3f, 1f, 1f, 0.3f));
            ConfigureFade(system, 0.7f);
            return system;
        }

        // The pivot sits at the base so the column grows out of the surface instead of floating up as a ball.
        private static Mesh BuildBaseMesh(Mesh source)
        {
            const string path = PrefabFolder + "/WaterSplash_ColumnMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "WaterSplash_ColumnMesh" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            Vector3[] vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].y += source.bounds.extents.y;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static ParticleSystem CreateColumn(Transform parent, string name, Material material, Mesh mesh)
        {
            ParticleSystem system = CreateBase(parent, name, material, ParticleSystemRenderMode.Mesh, 2, 0f, 1);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startSize3D = true;
            main.startSizeX = 0.8f;
            main.startSizeY = 2f;
            main.startSizeZ = 0.8f;
            main.startColor = new Color(0.78f, 1f, 0.98f, 1f);
            ConfigureCone(system, 0f, 0.01f);
            ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.separateAxes = true;
            sizeModule.x = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 0.15f, 1f, 0.6f, 1.15f, 1f, 0.7f));
            sizeModule.z = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 0.15f, 1f, 0.6f, 1.15f, 1f, 0.7f));
            sizeModule.y = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.05f, 0.12f, 1f, 0.55f, 0.85f, 1f, 0.25f));
            ConfigureFade(system, 0.95f);
            return system;
        }

        private static ParticleSystem CreateStreaks(Transform parent, string name, Material material, int count, float burstTime, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, Color color)
        {
            ParticleSystem system = CreateBase(parent, name, material, ParticleSystemRenderMode.Stretch, count + 8, burstTime, count);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.06f;
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            main.startColor = color;
            ConfigureFade(system, 0.9f);
            return system;
        }

        private static ParticleSystem CreatePuff(Transform parent, string name, Material material, int count, Vector2 lifetime, Vector2 speed, Vector2 size, float radius)
        {
            ParticleSystem system = CreateBase(parent, name, material, ParticleSystemRenderMode.Billboard, count + 4, 0f, count);
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = FoamTint;
            main.gravityModifier = 0f;
            ConfigureDisc(system, radius, 0.05f);
            ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.6f, 1f, 1.3f));
            ConfigureFade(system, 0.85f);
            return system;
        }

        private static ParticleSystem CreateRipple(Transform parent, string name, Material material, float lifetime, float size, float growth)
        {
            ParticleSystem system = CreateBase(parent, name, material, ParticleSystemRenderMode.HorizontalBillboard, 2, 0f, 1);
            system.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = new Color(0.85f, 1f, 1f, 0.6f);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 1f, growth));
            ConfigureFade(system, 0.6f);
            return system;
        }

        private static void ConfigureCone(ParticleSystem system, float angle, float radius)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = Up;
        }

        private static void ConfigureDisc(ParticleSystem system, float radius, float height)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.rotation = Up;
            shape.position = new Vector3(0f, height, 0f);
        }

        private static void ConfigureFade(ParticleSystem system, float peak)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peak, 0.08f),
                    new GradientAlphaKey(peak * 0.6f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        private static AnimationCurve Curve(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }
    }
}
