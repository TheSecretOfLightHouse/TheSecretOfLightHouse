using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.AI.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MantaRiseAttack))]
    public sealed class MantaElectricView : MonoBehaviour
    {
        private static readonly int ActivityId = Shader.PropertyToID("_Activity");
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _warningColor = new Color(1f, 0.65f, 0.1f, 0.5f);
        [SerializeField] private Color _electricColor = new Color(0.15f, 0.8f, 1f, 0.8f);
        private MantaRiseAttack _attack;
        private LineRenderer _area;
        private MaterialPropertyBlock _properties;

        private void Awake()
        {
            _attack = GetComponent<MantaRiseAttack>();
            _area = CreateLine("ElectricArea");
            _properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            bool visible = _attack.isClient && _attack.isActiveAndEnabled && _lineMaterial != null
                && (_attack.ElectricPoints.Count >= 2
                    || (_attack.ElectricPoints.Count == 1 && _attack.Phase == MantaAttackPhase.Laying))
                && NetworkTime.time < _attack.ExpiresAt;
            _area.enabled = visible;
            if (!visible)
            {
                return;
            }
            bool active = NetworkTime.time >= _attack.DischargeAt;
            Color color = active ? _electricColor : _warningColor;
            _area.startColor = _area.endColor = color;
            _area.widthMultiplier = _attack.ActiveLineWidth;
            _properties.SetFloat(ActivityId, active ? 1f : 0f);
            _properties.SetFloat(EffectTimeId, (float)(NetworkTime.time % 4096d));
            _area.SetPropertyBlock(_properties);
            int count = _attack.ElectricPoints.Count;
            bool laying = _attack.Phase == MantaAttackPhase.Laying;
            _area.positionCount = count + (laying ? 1 : 0);
            for (int index = 0; index < count; index++)
            {
                Vector3 point = _attack.ElectricPoints[index];
                _area.SetPosition(index, point);
            }
            if (laying)
            {
                Vector3 tail = _attack.TailPosition;
                tail.y = _attack.ElectricPoints[count - 1].y;
                _area.SetPosition(count, tail);
            }
        }

        private void OnDisable()
        {
            if (_area != null)
            {
                _area.enabled = false;
            }
        }

        private LineRenderer CreateLine(string objectName)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.TransformZ;
            child.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.startWidth = line.endWidth = 1f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }
    }
}
