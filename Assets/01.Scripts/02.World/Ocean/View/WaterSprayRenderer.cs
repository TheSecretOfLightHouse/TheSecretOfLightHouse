using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Ocean.View
{
    public sealed class WaterSprayRenderer : IDisposable
    {
        public const int Capacity = 8192;
        private readonly ComputeBuffer _particles;
        private readonly ComputeBuffer _counter;
        private readonly ComputeShader _solver;
        private readonly SurfaceInteractionGrid _grid;
        private readonly Material _material;
        private readonly Material _crest;
        private readonly int _update;
        private readonly int _shore;
        private readonly int _contact;
        private int _frame;

        private struct Particle
        {
            public Vector4 PositionAge;
            public Vector4 VelocityLife;
            public Vector4 SizeSeed;
        }

        public ComputeBuffer Particles => _particles;
        public ComputeBuffer Counter => _counter;

        public WaterSprayRenderer(SurfaceInteractionGrid grid, Shader shader, Shader crestShader)
        {
            _solver = grid.Simulation;
            _grid = grid;
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _crest = new Material(crestShader) { hideFlags = HideFlags.DontSave };
            _particles = new ComputeBuffer(Capacity, 48);
            _counter = new ComputeBuffer(1, sizeof(uint));
            _particles.SetData(new Particle[Capacity]);
            _counter.SetData(new uint[1]);
            _update = _solver.FindKernel("SprayUpdate");
            _shore = _solver.FindKernel("ShoreSpray");
            _contact = _solver.FindKernel("ContactSpray");
            _solver.SetInt("_SprayCapacity", Capacity);
            _solver.SetBuffer(_update, "_SprayParticles", _particles);
            _solver.SetTexture(_update, "_Background", grid.Background);
            _solver.SetBuffer(_shore, "_SprayParticles", _particles);
            _solver.SetBuffer(_shore, "_SprayCounter", _counter);
            _solver.SetBuffer(_contact, "_SprayParticles", _particles);
            _solver.SetBuffer(_contact, "_SprayCounter", _counter);
            _solver.SetTexture(_contact, "_Background", grid.Background);
            _solver.SetTexture(_contact, "_Domain", grid.Domain);
            _material.SetBuffer("_SprayParticles", _particles);
        }

        public void Update(Vector3 focus, float deltaTime, int layer)
        {
            _solver.SetFloat("_SprayDelta", Mathf.Min(0.05f, deltaTime));
            _solver.SetInt("_SprayFrame", _frame++);
            _solver.SetVector("_SprayFocus", focus);
            _solver.SetTexture(_update, "_SprayState", _grid.State);
            _solver.Dispatch(_update, Capacity / 64, 1, 1);
            _solver.Dispatch(_shore, 48, 1, 1);
            _solver.Dispatch(_contact, SurfaceInteractionGrid.Resolution / 8, SurfaceInteractionGrid.Resolution / 8, 1);
            Graphics.DrawProcedural(_material, new Bounds(focus, new Vector3(100f, 40f, 100f)),
                MeshTopology.Triangles, 6, Capacity, null, null, ShadowCastingMode.Off, false, layer);
            _crest.SetVector("_SurfFocus", focus);
            Graphics.DrawProcedural(_crest, new Bounds(focus, new Vector3(140f, 40f, 140f)),
                MeshTopology.Triangles, 96 * 12 * 6, 32 * 3, null, null, ShadowCastingMode.Off, false, layer);
        }

        public void Dispose()
        {
            _particles.Release();
            _counter.Release();
            UnityEngine.Object.Destroy(_material);
            UnityEngine.Object.Destroy(_crest);
        }
    }
}
