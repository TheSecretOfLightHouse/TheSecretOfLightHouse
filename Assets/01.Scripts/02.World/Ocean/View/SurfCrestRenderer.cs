using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Ocean.View
{
    public sealed class SurfCrestRenderer : IDisposable
    {
        private const int SegmentsPerStrip = 96;
        private const int PiecesPerStrip = 20;
        private const int StripInstances = 32 * 3;
        private const float DrawExtent = 140f;
        private const float DrawHeight = 40f;
        private static readonly int SurfFocusId = Shader.PropertyToID("_SurfFocus");
        private readonly Material _material;

        public SurfCrestRenderer(Shader shader)
        {
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        public void Update(Vector3 focus, int layer)
        {
            _material.SetVector(SurfFocusId, focus);
            Graphics.DrawProcedural(_material, new Bounds(focus, new Vector3(DrawExtent, DrawHeight, DrawExtent)),
                MeshTopology.Triangles, SegmentsPerStrip * PiecesPerStrip * 6, StripInstances, null, null,
                ShadowCastingMode.Off, false, layer);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(_material);
        }
    }
}
