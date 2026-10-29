using System.Collections.Generic;
using UnityEngine;

// Keeps small, independently twinkling stars on the animated upper surface.
[DisallowMultipleComponent]
public class MosaWhaleConstellation : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer body;
    [SerializeField] private ParticleSystem stars;
    [SerializeField, Range(8, 100)] private int starCount = 45;
    [SerializeField] private float starSize = 0.43f;
    private Mesh baked;
    private int[] indices;
    private ParticleSystem.Particle[] particles;

    private void Start()
    {
        if (!body || !stars) { enabled = false; return; }
        baked = new Mesh();
        body.BakeMesh(baked);
        var vertices = baked.vertices;
        var normals = baked.normals;
        var bounds = body.bounds;
        var candidates = new List<int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = body.transform.position + body.transform.rotation * vertices[i];
            Vector3 normal = body.transform.TransformDirection(normals[i]).normalized;
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 center = transform.InverseTransformPoint(bounds.center);
            float width = bounds.extents.x / Mathf.Abs(transform.lossyScale.x);
            float length = bounds.extents.z / Mathf.Abs(transform.lossyScale.z);
            if (normal.y > 0.5f && world.y > bounds.center.y && Mathf.Abs(local.x - center.x) < width * 0.65f && Mathf.Abs(local.z - center.z) < length * 0.65f)
                candidates.Add(i);
        }
        if (candidates.Count == 0) { enabled = false; return; }
        indices = new int[starCount];
        particles = new ParticleSystem.Particle[starCount];
        var random = new System.Random(731);
        for (int i = 0; i < starCount; i++) indices[i] = candidates[random.Next(candidates.Count)];
        stars.Play();
    }

    private void LateUpdate()
    {
        if (indices == null) return;
        body.BakeMesh(baked);
        var vertices = baked.vertices;
        var normals = baked.normals;
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            Vector3 normal = body.transform.TransformDirection(normals[index]).normalized;
            particles[i].position = body.transform.position + body.transform.rotation * vertices[index] + normal * 0.06f;
            float twinkle = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * (1.1f + (i % 5) * 0.23f) + i * 2.17f), 2f);
            particles[i].startSize = starSize * (0.75f + twinkle * 0.65f);
            particles[i].startColor = new Color(0.35f + 0.5f * twinkle, 0.7f + 0.3f * twinkle, 1f, 0.5f + 0.5f * twinkle);
            particles[i].remainingLifetime = particles[i].startLifetime = 10f;
        }
        stars.SetParticles(particles, particles.Length);
    }

    private void OnDestroy() { if (baked) Destroy(baked); }
}



