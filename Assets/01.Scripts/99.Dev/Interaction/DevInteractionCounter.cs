#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Interaction
{
    // JH02 test target state: counts successful test interactions so duplicates show up as extra counts.
    [DisallowMultipleComponent]
    public sealed class DevInteractionCounter : NetworkBehaviour
    {
        private static readonly List<DevInteractionCounter> ActiveCounters = new List<DevInteractionCounter>();
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        private string _label = "Target";
        [SerializeField]
        private Renderer _renderer;

        [SyncVar(hook = nameof(OnCountChanged))]
        private int _count;

        private MaterialPropertyBlock _block;

        public static IReadOnlyList<DevInteractionCounter> Active => ActiveCounters;

        public string Label => _label;
        public int Count => _count;

        private void OnEnable()
        {
            ActiveCounters.Add(this);
        }

        private void OnDisable()
        {
            ActiveCounters.Remove(this);
        }

        public static DevInteractionCounter FindByLabel(string label)
        {
            for (int i = 0; i < ActiveCounters.Count; i++)
            {
                if (ActiveCounters[i]._label == label)
                {
                    return ActiveCounters[i];
                }
            }

            return null;
        }

        [Server]
        public int Increment()
        {
            _count++;
            return _count;
        }

        private void OnCountChanged(int oldValue, int newValue)
        {
            if (!_renderer)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, Color.HSVToRGB(newValue * 0.17f % 1f, 0.7f, 0.9f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
#endif
