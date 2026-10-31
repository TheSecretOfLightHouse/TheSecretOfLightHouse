using System.Collections.Generic;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SharedMapGraphic : UnityEngine.UI.MaskableGraphic
    {
        public static readonly Color[] InkColors =
        {
            new Color(0.20f, 0.13f, 0.08f), new Color(0.68f, 0.23f, 0.16f),
            new Color(0.15f, 0.35f, 0.46f), new Color(0.22f, 0.40f, 0.27f)
        };
        public static readonly float[] BrushRadii = { 1.8f, 3.8f, 6.8f };
        private readonly HashSet<ulong> _erased = new HashSet<ulong>();
        private readonly HashSet<ulong> _painted = new HashSet<ulong>();
        private SharedMapRuntime _runtime;
        private SharedMapBrush _brush;
        private uint _epoch = uint.MaxValue, _revision;
        private int _processed;

        public override Texture mainTexture => _brush != null ? _brush.Texture : Texture2D.whiteTexture;

        public void Bind(SharedMapRuntime runtime)
        {
            _runtime = runtime;
            if (_brush == null)
            {
                _brush = new SharedMapBrush();
            }
            SetAllDirty();
        }

        public bool TryFindStroke(Vector2 point, out SharedMapOperation operation)
        {
            operation = default;
            if (!_runtime)
            {
                return false;
            }
            CollectErased();
            float nearest = 0.02f;
            bool found = FindStroke(_runtime.Operations, point, ref nearest, ref operation);
            return FindStroke(_runtime.PendingOperations, point, ref nearest, ref operation) || found;
        }

        protected override void OnDestroy()
        {
            _brush?.Dispose();
            _brush = null;
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            vertices.Clear();
            if (!_runtime)
            {
                return;
            }
            RefreshPaint();
            Rect rect = rectTransform.rect;
            vertices.AddVert(new Vector2(rect.xMin, rect.yMin), Color.white, Vector2.zero);
            vertices.AddVert(new Vector2(rect.xMin, rect.yMax), Color.white, Vector2.up);
            vertices.AddVert(new Vector2(rect.xMax, rect.yMax), Color.white, Vector2.one);
            vertices.AddVert(new Vector2(rect.xMax, rect.yMin), Color.white, Vector2.right);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(0, 2, 3);
        }

        private void RefreshPaint()
        {
            bool rebuild = _brush == null || _epoch != _runtime.State.Epoch || _revision != _runtime.CanvasRevision
                || _processed > _runtime.Operations.Count;
            for (int i = _processed; !rebuild && i < _runtime.Operations.Count; i++)
            {
                SharedMapOperation item = _runtime.Operations[i];
                rebuild = item.IsErase && !WasPainted(item);
            }
            foreach (SharedMapOperation item in _runtime.PendingOperations)
            {
                rebuild |= item.IsErase && !WasPainted(item);
            }
            if (_brush == null)
            {
                _brush = new SharedMapBrush();
            }
            bool changed = rebuild;
            if (rebuild)
            {
                _brush.Clear();
                _painted.Clear();
                CollectErased();
                _processed = 0;
                _epoch = _runtime.State.Epoch;
                _revision = _runtime.CanvasRevision;
            }
            for (int i = _processed; i < _runtime.Operations.Count; i++)
            {
                changed |= Paint(_runtime.Operations[i]);
            }
            foreach (SharedMapOperation operation in _runtime.PendingOperations)
            {
                changed |= Paint(operation);
            }
            _processed = _runtime.Operations.Count;
            if (changed)
            {
                _brush.Upload();
            }
        }

        private bool Paint(SharedMapOperation operation)
        {
            if (WasPainted(operation))
            {
                return false;
            }
            if (operation.Sample != 0)
            {
                _painted.Add(SampleKey(operation));
            }
            if (operation.IsErase || _erased.Contains(StrokeKey(operation)))
            {
                return false;
            }
            _brush.Paint(operation.From, operation.To, BrushRadii[Mathf.Clamp(operation.BrushSize, 0, 2)],
                InkColors[Mathf.Clamp(operation.Color, 0, 3)]);
            return true;
        }

        private bool WasPainted(SharedMapOperation operation)
        {
            return operation.Sample != 0 && _painted.Contains(SampleKey(operation));
        }

        private bool FindStroke(IReadOnlyList<SharedMapOperation> operations, Vector2 point, ref float nearest, ref SharedMapOperation hit)
        {
            bool found = false;
            foreach (SharedMapOperation item in operations)
            {
                if (item.IsErase || _erased.Contains(StrokeKey(item)))
                {
                    continue;
                }
                float distance = SharedMapRules.DistanceToSegment(point, item.From, item.To);
                if (distance <= nearest)
                {
                    nearest = distance;
                    hit = item;
                    found = true;
                }
            }
            return found;
        }

        private void CollectErased()
        {
            _erased.Clear();
            CollectErased(_runtime.Operations);
            CollectErased(_runtime.PendingOperations);
        }

        private void CollectErased(IReadOnlyList<SharedMapOperation> operations)
        {
            foreach (SharedMapOperation item in operations)
            {
                if (item.IsErase)
                {
                    _erased.Add(StrokeKey(item));
                }
            }
        }

        private static ulong StrokeKey(SharedMapOperation operation)
        {
            return ((ulong)operation.Author << 32) | operation.Stroke;
        }

        private static ulong SampleKey(SharedMapOperation operation)
        {
            return ((ulong)operation.Writer << 32) | operation.Sample;
        }
    }
}
