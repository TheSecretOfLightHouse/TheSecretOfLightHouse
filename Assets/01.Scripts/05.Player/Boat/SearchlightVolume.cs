using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class SearchlightVolume : MonoBehaviour
{
    [SerializeField]
    private Light _source;

    [SerializeField, Range(0.1f, 1f)]
    private float _widthMultiplier = 0.55f;

    private MeshRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    private void LateUpdate()
    {
        if (!_source)
        {
            _renderer.enabled = false;
            return;
        }

        float length = Mathf.Max(0.01f, _source.range);
        float diameter = 2f * Mathf.Tan(_source.spotAngle * Mathf.Deg2Rad * 0.5f) * length * _widthMultiplier;
        transform.localPosition = Vector3.forward * (length * 0.5f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(diameter, diameter, length);
        _renderer.enabled = _source.isActiveAndEnabled;
    }
}