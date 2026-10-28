using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.Monsters.AI.View
{
    public sealed class MonsterHitFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private SkinnedMeshRenderer[] _renderers;
        [SerializeField, Min(0.01f)] private float _duration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _redBlend = 0.45f;
        [SerializeField] private Color _hitColor = new Color(1f, 0.02f, 0.02f, 1f);

        private readonly List<RendererState> _states = new List<RendererState>();
        private Coroutine _routine;

        private sealed class RendererState
        {
            public SkinnedMeshRenderer Renderer;
            public MaterialPropertyBlock[] OriginalBlocks;
            public Color[] BaseColors;
        }

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            var seen = new HashSet<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in _renderers)
            {
                if (renderer == null || !seen.Add(renderer))
                {
                    continue;
                }
                int count = renderer.sharedMaterials.Length;
                var state = new RendererState
                {
                    Renderer = renderer,
                    OriginalBlocks = new MaterialPropertyBlock[count],
                    BaseColors = new Color[count]
                };
                for (int i = 0; i < count; i++)
                {
                    state.OriginalBlocks[i] = new MaterialPropertyBlock();
                }
                _states.Add(state);
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                Restore();
            }
        }

        public void PlayHit()
        {
            if (!isActiveAndEnabled || _states.Count == 0)
            {
                return;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            else
            {
                foreach (RendererState state in _states)
                {
                    if (state.Renderer == null)
                    {
                        continue;
                    }
                    Material[] materials = state.Renderer.sharedMaterials;
                    for (int i = 0; i < state.OriginalBlocks.Length; i++)
                    {
                        state.Renderer.GetPropertyBlock(state.OriginalBlocks[i], i);
                        Material material = i < materials.Length ? materials[i] : null;
                        state.BaseColors[i] = state.OriginalBlocks[i].HasColor(BaseColorId)
                            ? state.OriginalBlocks[i].GetColor(BaseColorId)
                            : material != null && material.HasProperty(BaseColorId)
                                ? material.GetColor(BaseColorId) : Color.white;
                    }
                }
            }
            _routine = StartCoroutine(ShowHit());
        }

        private IEnumerator ShowHit()
        {
            float elapsed = 0f;
            var block = new MaterialPropertyBlock();
            while (elapsed < _duration)
            {
                float strength = _redBlend * (1f - elapsed / _duration);
                foreach (RendererState state in _states)
                {
                    if (state.Renderer == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < state.BaseColors.Length; i++)
                    {
                        state.Renderer.GetPropertyBlock(block, i);
                        block.SetColor(BaseColorId, Color.Lerp(state.BaseColors[i], _hitColor, strength));
                        state.Renderer.SetPropertyBlock(block, i);
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Restore();
            _routine = null;
        }

        private void Restore()
        {
            foreach (RendererState state in _states)
            {
                if (state.Renderer == null)
                {
                    continue;
                }
                for (int i = 0; i < state.OriginalBlocks.Length; i++)
                {
                    state.Renderer.SetPropertyBlock(state.OriginalBlocks[i], i);
                }
            }
        }
    }
}
