using UnityEngine;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatDurability))]
    public sealed class BoatHullDamage : MonoBehaviour
    {
        [SerializeField] private MeshFilter _model;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Mesh[] _damageMeshes;
        [SerializeField, Range(0f, 1f)] private float _firstDamageRatio = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _secondDamageRatio = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _thirdDamageRatio = 0.25f;

        private BoatDurability _durability;
        private Mesh _intactMesh;
        private Material[] _intactMaterials;
        private int _stage = -1;

        public int DamageStage => GetStage(_durability.CurrentDurability, _durability.MaxDurability);

        private void Awake()
        {
            _durability = GetComponent<BoatDurability>();
            if (_model && _renderer)
            {
                _intactMesh = _model.sharedMesh;
                _intactMaterials = _renderer.sharedMaterials;
            }
        }

        private void OnEnable()
        {
            _durability.DurabilityChanged += HandleDurabilityChanged;
        }

        private void LateUpdate()
        {
            // Server-side setters and client SyncVar hooks can arrive in different orders.
            ApplyStage(DamageStage);
        }

        private void OnDisable()
        {
            _durability.DurabilityChanged -= HandleDurabilityChanged;
        }

        private void HandleDurabilityChanged(float current, float maximum)
        {
            ApplyStage(GetStage(current, maximum));
        }

        private int GetStage(float current, float maximum)
        {
            float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 1f;
            if (ratio <= _thirdDamageRatio)
            {
                return 3;
            }

            if (ratio <= _secondDamageRatio)
            {
                return 2;
            }

            if (ratio <= _firstDamageRatio)
            {
                return 1;
            }

            return 0;
        }

        private void ApplyStage(int stage)
        {
            if (_stage == stage || !_model || !_renderer || !_intactMesh)
            {
                return;
            }

            if (stage > 0 && (_damageMeshes == null || _damageMeshes.Length < stage || !_damageMeshes[stage - 1]))
            {
                return;
            }

            _stage = stage;
            _model.sharedMesh = stage == 0 ? _intactMesh : _damageMeshes[stage - 1];
            _renderer.sharedMaterials = _intactMaterials;
        }
    }
}
