using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Islands.Editor
{
    internal sealed class TerrainMeshBuilder
    {
        private readonly TerrainData _data;
        private readonly TerrainSplatSampler _splat;
        private readonly int _cellsX;
        private readonly int _cellsZ;
        private readonly int[] _remap;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();

        private TerrainMeshBuilder(TerrainData data, float step, TerrainSplatSampler splat)
        {
            _data = data;
            _splat = splat;
            _cellsX = Mathf.Max(1, Mathf.CeilToInt(data.size.x / step));
            _cellsZ = Mathf.Max(1, Mathf.CeilToInt(data.size.z / step));
            _remap = new int[(_cellsX + 1) * (_cellsZ + 1)];
            for (int i = 0; i < _remap.Length; i++)
            {
                _remap[i] = -1;
            }
        }

        // Pass a null splat for collision meshes: they need positions only.
        public static Mesh Build(TerrainData data, float step, TerrainSplatSampler splat, string meshName)
        {
            var builder = new TerrainMeshBuilder(data, step, splat);
            builder.Fill();
            return builder.CreateMesh(meshName);
        }

        private void Fill()
        {
            int holes = _data.holesResolution;
            for (int cz = 0; cz < _cellsZ; cz++)
            {
                int holeZ = Mathf.Min((int)((cz + 0.5f) / _cellsZ * holes), holes - 1);
                for (int cx = 0; cx < _cellsX; cx++)
                {
                    int holeX = Mathf.Min((int)((cx + 0.5f) / _cellsX * holes), holes - 1);
                    if (_data.IsHole(holeX, holeZ))
                    {
                        continue;
                    }

                    int a = GetVertex(cx, cz);
                    int b = GetVertex(cx, cz + 1);
                    int c = GetVertex(cx + 1, cz);
                    int d = GetVertex(cx + 1, cz + 1);
                    _triangles.Add(a);
                    _triangles.Add(b);
                    _triangles.Add(d);
                    _triangles.Add(a);
                    _triangles.Add(d);
                    _triangles.Add(c);
                }
            }
        }

        private int GetVertex(int ix, int iz)
        {
            int key = iz * (_cellsX + 1) + ix;
            if (_remap[key] >= 0)
            {
                return _remap[key];
            }

            float u = ix / (float)_cellsX;
            float v = iz / (float)_cellsZ;
            _remap[key] = _vertices.Count;
            _vertices.Add(new Vector3(u * _data.size.x, _data.GetInterpolatedHeight(u, v), v * _data.size.z));
            if (_splat != null)
            {
                _normals.Add(_data.GetInterpolatedNormal(u, v));
                _colors.Add(_splat.Sample(u, v));
            }

            return _remap[key];
        }

        private Mesh CreateMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            if (_splat != null)
            {
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
            }

            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
