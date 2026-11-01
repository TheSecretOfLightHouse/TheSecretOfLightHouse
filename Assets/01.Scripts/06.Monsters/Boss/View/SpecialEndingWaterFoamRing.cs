using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20002)]
    public sealed class SpecialEndingWaterFoamRing : MonoBehaviour
    {
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int InnerId = Shader.PropertyToID("_Inner");
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private SpecialEndingWaterSurfaceAnchor _anchor;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private AnimationCurve _radius = AnimationCurve.Linear(0f, 4f, 23f, 9f);
        [SerializeField] private AnimationCurve _opacity = new AnimationCurve();
        [SerializeField] private float _inner = 0.55f;
        private MaterialPropertyBlock _properties;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (_renderer == null)
            {
                return;
            }
            bool visible = _camera != null && _camera.enabled && _director != null
                && _anchor != null && _anchor.HasSurface && _anchor.HasContact;
            float opacity = visible ? _opacity.Evaluate((float)_director.time) : 0f;
            _renderer.enabled = opacity > 0.005f;
            if (!_renderer.enabled)
            {
                return;
            }
            Vector3 point = _anchor.transform.position;
            _properties.SetVector(CenterId, new Vector4(point.x, 0f, point.z, 0f));
            _properties.SetFloat(RadiusId, _radius.Evaluate((float)_director.time));
            _properties.SetFloat(OpacityId, opacity);
            _properties.SetFloat(InnerId, _inner);
            _renderer.SetPropertyBlock(_properties);
        }

        private void OnDisable()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }
    }
}
