using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(30001)]
    [DisallowMultipleComponent]
    public sealed class SpecialEndingLighting : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Material _skybox;
        [SerializeField] private Light[] _lights = new Light[0];
        [SerializeField] private Behaviour _volume;
        private readonly Dictionary<Light, bool> _savedLights = new Dictionary<Light, bool>();
        private bool _isPlaying;
        private Material _previousSkybox;
        private Material _runtimeSkybox;
        private AmbientMode _ambientMode;
        private Color _ambientLight;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float _ambientIntensity;
        private bool _fog;
        private Color _fogColor;
        private FogMode _fogMode;
        private float _fogDensity;
        private CameraClearFlags _clearFlags;
        private Component _cameraData;
        private PropertyInfo _postProcessingProperty;
        private PropertyInfo _volumeMaskProperty;
        private bool _postProcessing;
        private LayerMask _volumeMask;

        private void LateUpdate()
        {
            if (_isPlaying)
            {
                ApplyLook();
            }
        }

        private void OnDisable()
        {
            SetPlaying(false);
        }

        public void SetPlaying(bool value)
        {
            if (_isPlaying == value)
            {
                return;
            }
            _isPlaying = value;
            if (value)
            {
                _previousSkybox = RenderSettings.skybox;
                if (_skybox != null)
                {
                    _runtimeSkybox = new Material(_skybox);
                }
                _ambientMode = RenderSettings.ambientMode;
                _ambientLight = RenderSettings.ambientLight;
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _fog = RenderSettings.fog;
                _fogColor = RenderSettings.fogColor;
                _fogMode = RenderSettings.fogMode;
                _fogDensity = RenderSettings.fogDensity;
                if (_camera != null)
                {
                    _clearFlags = _camera.clearFlags;
                    _cameraData = _camera.GetComponent("UniversalAdditionalCameraData");
                    if (_cameraData != null)
                    {
                        _postProcessingProperty = _cameraData.GetType().GetProperty("renderPostProcessing");
                        _volumeMaskProperty = _cameraData.GetType().GetProperty("volumeLayerMask");
                        if (_postProcessingProperty != null && _volumeMaskProperty != null)
                        {
                            _postProcessing = (bool)_postProcessingProperty.GetValue(_cameraData, null);
                            _volumeMask = (LayerMask)_volumeMaskProperty.GetValue(_cameraData, null);
                            _postProcessingProperty.SetValue(_cameraData, true, null);
                            _volumeMaskProperty.SetValue(_cameraData, (LayerMask)(_volumeMask.value | 1), null);
                        }
                    }
                }
                // Capture scene lights once; the weather and server state remain independent.
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Light light in root.GetComponentsInChildren<Light>(true))
                        {
                            if (light.type == LightType.Directional && !light.transform.IsChildOf(transform))
                            {
                                _savedLights[light] = light.enabled;
                            }
                        }
                    }
                }
                ApplyLook();
            }
            else
            {
                RenderSettings.skybox = _previousSkybox;
                if (_runtimeSkybox != null)
                {
                    Destroy(_runtimeSkybox);
                    _runtimeSkybox = null;
                }
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientLight;
                RenderSettings.ambientSkyColor = _ambientSky;
                RenderSettings.ambientEquatorColor = _ambientEquator;
                RenderSettings.ambientGroundColor = _ambientGround;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.fog = _fog;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogDensity = _fogDensity;
                if (_camera != null)
                {
                    _camera.clearFlags = _clearFlags;
                }
                if (_cameraData != null && _postProcessingProperty != null && _volumeMaskProperty != null)
                {
                    _postProcessingProperty.SetValue(_cameraData, _postProcessing, null);
                    _volumeMaskProperty.SetValue(_cameraData, _volumeMask, null);
                }
                foreach (KeyValuePair<Light, bool> entry in _savedLights)
                {
                    if (entry.Key != null)
                    {
                        entry.Key.enabled = entry.Value;
                    }
                }
                _savedLights.Clear();
            }
            if (_volume != null)
            {
                _volume.enabled = value;
            }
            foreach (Light light in _lights)
            {
                if (light != null)
                {
                    light.enabled = value;
                }
            }
        }

        private void ApplyLook()
        {
            RenderSettings.skybox = _runtimeSkybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.035f, 0.055f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.018f, 0.03f, 0.05f);
            RenderSettings.ambientGroundColor = new Color(0.008f, 0.012f, 0.02f);
            RenderSettings.ambientIntensity = 0.45f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.085f);
            RenderSettings.fogDensity = 0.0045f;
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.Skybox;
            }
            foreach (KeyValuePair<Light, bool> entry in _savedLights)
            {
                if (entry.Key != null)
                {
                    entry.Key.enabled = false;
                }
            }
        }
    }
}
