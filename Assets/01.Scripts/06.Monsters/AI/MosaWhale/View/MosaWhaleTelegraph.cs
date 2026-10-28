using Lighthouse.Monsters.MosaWhale.Server;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.Monsters.MosaWhale.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MosaWhaleMovement))]
    public sealed class MosaWhaleTelegraph : MonoBehaviour
    {
        private const int CircleSegments = 96;

        [SerializeField] private Material _material;
        [SerializeField] private Color _color = new Color(1f, 0.35f, 0.05f, 0.9f);
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.2f;
        [SerializeField] private float _surfaceOffset = 0.1f;

        private MosaWhaleMovement _movement;
        private LineRenderer _line;

        public bool IsVisible => _line != null && _line.enabled;

        private void Awake()
        {
            _movement = GetComponent<MosaWhaleMovement>();
            var child = new GameObject("MosaWhaleAttackArea");
            child.transform.SetParent(transform, false);
            _line = child.AddComponent<LineRenderer>();
            _line.sharedMaterial = _material;
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = CircleSegments;
            _line.startColor = _line.endColor = _color;
            _line.widthMultiplier = _lineWidth;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            bool visible = _movement.isClient && _movement.isActiveAndEnabled
                && _movement.IsTelegraphVisible && _movement.HasAttackCenter && _material != null;
            _line.enabled = visible;
            if (!visible)
            {
                return;
            }
            Vector3 center = _movement.AttackCenter + Vector3.up * _surfaceOffset;
            float radius = _movement.AttackRadius;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / CircleSegments);
                _line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDisable()
        {
            if (_line != null)
            {
                _line.enabled = false;
            }
        }
    }
}
