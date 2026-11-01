using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SpecialEndingIslandBinding : MonoBehaviour
    {
        private static readonly HashSet<string> TowerPartNames = new HashSet<string>
        {
            "TowerBase", "TowerPlasterLower", "TowerPlasterMiddle", "TowerPlasterUpper",
            "TowerBand1", "TowerBand2", "Gallery_DeckSlab", "Gallery_FlaredSupport",
            "Gallery_LowerRail", "Gallery_RimBevel", "Gallery_ThirtyBalusters",
            "Gallery_UpperHandrail", "Lighthouse_GoldenLantern", "Pirates_Lantern_Spire"
        };

        private static readonly HashSet<string> TowerDetailNames = new HashSet<string>
        {
            "EntryDoor", "UpperWindow", "MiddleWindow", "BalconyCornice"
        };

        [SerializeField] private Transform _stage;
        [SerializeField] private Transform _previewIsland;
        [SerializeField] private Vector3 _sourceBasePoint = new Vector3(-1.1897792f, -0.475369f, 0.67f);
        [SerializeField] private float _decorationClearanceRadius = 6.5f;
        [SerializeField] private float _destructionTime = 18.25f;
        private Renderer[] _originals = new Renderer[0];
        private bool[] _savedEnabled = new bool[0];
        private Renderer[] _details = new Renderer[0];
        private bool[] _savedDetails = new bool[0];
        private PlayableDirector _director;
        private Renderer[] _fractures = new Renderer[0];
        private bool[] _savedFractures = new bool[0];
        private bool _isReplacing;

        private void Awake()
        {
            var presentation = GetComponent<Lighthouse.Tower.View.MainSceneSummonPresentation>();
            if (presentation != null)
            {
                _director = presentation.Director;
            }
            var fractures = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("DESTRUCT_", System.StringComparison.Ordinal))
                {
                    fractures.Add(renderer);
                }
            }
            _fractures = fractures.ToArray();
            _savedFractures = new bool[_fractures.Length];
            for (int i = 0; i < _fractures.Length; i++)
            {
                _savedFractures[i] = _fractures[i].enabled;
            }
            if (_previewIsland != null)
            {
                Bind(_previewIsland);
            }
        }

        private void LateUpdate()
        {
            if (_isReplacing)
            {
                UpdateDetails();
                for (int i = 0; i < _originals.Length; i++)
                {
                    if (_originals[i] != null)
                    {
                        bool tower = TowerPartNames.Contains(_originals[i].name)
                            || _originals[i].name.StartsWith("Slot", System.StringComparison.Ordinal);
                        _originals[i].enabled = tower && IsIntact && _savedEnabled[i];
                    }
                }
            }
        }

        private void OnDisable()
        {
            SetReplacing(false);
        }

        /// <summary>Aligns presentation art to the real lighthouse without changing gameplay objects.</summary>
        public void Bind(Transform island)
        {
            SetReplacing(false);
            if (island == null || _stage == null)
            {
                return;
            }
            var originals = new List<Renderer>();
            var details = new List<Renderer>();
            Transform towerBase = null;
            Renderer[] renderers = island.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (TowerPartNames.Contains(renderer.name) || renderer.name.StartsWith("Slot", System.StringComparison.Ordinal))
                {
                    originals.Add(renderer);
                    if (renderer.name == "TowerBase")
                    {
                        towerBase = renderer.transform;
                    }
                }
                else if (TowerDetailNames.Contains(renderer.name))
                {
                    details.Add(renderer);
                }
            }
            if (towerBase != null)
            {
                Vector3 center = island.InverseTransformPoint(towerBase.position);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.transform.IsChildOf(transform) || originals.Contains(renderer))
                    {
                        continue;
                    }
                    string name = renderer.name;
                    bool decoration = name.StartsWith("SM_Env_Tree_", System.StringComparison.Ordinal)
                        || name.StartsWith("SM_Env_Bush_", System.StringComparison.Ordinal)
                        || name.StartsWith("SM_Env_Fern_", System.StringComparison.Ordinal)
                        || name.StartsWith("Shore_Rock_", System.StringComparison.Ordinal);
                    if (!decoration)
                    {
                        continue;
                    }
                    Vector3 offset = island.InverseTransformPoint(renderer.bounds.center) - center;
                    float top = island.InverseTransformPoint(new Vector3(renderer.bounds.center.x,
                        renderer.bounds.max.y, renderer.bounds.center.z)).y;
                    // Clear the authored hand sweep while preserving the island floor and stairs.
                    if (offset.x * offset.x + offset.z * offset.z <= _decorationClearanceRadius * _decorationClearanceRadius
                        && top > center.y + 0.5f)
                    {
                        originals.Add(renderer);
                    }
                }
                // Both actors share this correction, preserving the authored finger contact geometry.
                transform.position += towerBase.position - _stage.TransformPoint(_sourceBasePoint);
            }
            _originals = originals.ToArray();
            _savedEnabled = new bool[_originals.Length];
            _details = details.ToArray();
            _savedDetails = new bool[_details.Length];
        }

        /// <summary>Replaces renderers only; tower colliders and server state remain authoritative.</summary>
        public void SetReplacing(bool value)
        {
            if (_isReplacing == value)
            {
                return;
            }
            _isReplacing = value;
            for (int i = 0; i < _details.Length; i++)
            {
                if (_details[i] == null)
                {
                    continue;
                }
                if (value)
                {
                    _savedDetails[i] = _details[i].enabled;
                }
                else
                {
                    _details[i].enabled = _savedDetails[i];
                }
            }
            if (value)
            {
                UpdateDetails();
            }
            for (int i = 0; i < _originals.Length; i++)
            {
                if (_originals[i] == null)
                {
                    continue;
                }
                if (value)
                {
                    _savedEnabled[i] = _originals[i].enabled;
                    _originals[i].enabled = false;
                }
                else
                {
                    _originals[i].enabled = _savedEnabled[i];
                }
            }
        }

        private void UpdateDetails()
        {
            // Keep original architectural details until the tower actually breaks.
            bool intact = IsIntact;
            for (int i = 0; i < _fractures.Length; i++)
            {
                if (_fractures[i] != null)
                {
                    _fractures[i].enabled = !intact && _savedFractures[i];
                }
            }
            for (int i = 0; i < _details.Length; i++)
            {
                if (_details[i] != null)
                {
                    _details[i].enabled = intact && _savedDetails[i];
                }
            }
        }

        private bool IsIntact => _director == null || _director.time < _destructionTime;
    }
}
