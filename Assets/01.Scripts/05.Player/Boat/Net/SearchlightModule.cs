using UnityEngine;

[DisallowMultipleComponent]
public sealed class SearchlightModule : BoatModule
{
    [SerializeField]
    private Light _searchlight;

    [SerializeField]
    private Light _fillLight;

    [SerializeField]
    private Renderer _beamRenderer;

    public override BoatModuleType ModuleType => BoatModuleType.Searchlight;

    protected override void Awake()
    {
        base.Awake();

        if (!_beamRenderer && _searchlight)
        {
            _beamRenderer = _searchlight.GetComponentInChildren<MeshRenderer>(true);
        }

        if (_searchlight)
        {
            _searchlight.enabled = false;
        }

        if (_fillLight)
        {
            _fillLight.enabled = false;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = false;
        }
    }

    protected override bool CanApplyEffect()
    {
        return _searchlight;
    }

    protected override void ApplyEffect()
    {
        if (_searchlight)
        {
            _searchlight.enabled = true;
        }

        if (_fillLight)
        {
            _fillLight.enabled = true;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = true;
        }
    }

    protected override void RemoveEffect()
    {
        if (_searchlight)
        {
            _searchlight.enabled = false;
        }

        if (_fillLight)
        {
            _fillLight.enabled = false;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = false;
        }
    }
}
