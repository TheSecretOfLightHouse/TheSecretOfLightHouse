using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Tower.View
{
    public sealed class MainSceneSummonPresentation : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioListener _listener;
        private Renderer[] _renderers;
        private bool[] _rendererEnabled;
        private bool _started;

        public bool IsReady => _director != null && _camera != null && _director.playableAsset != null
            && _director.duration > 0d && !double.IsNaN(_director.duration) && !double.IsInfinity(_director.duration);
        public PlayableDirector Director => _director;
        public Camera Camera => _camera;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _rendererEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _rendererEnabled[i] = _renderers[i].enabled;
            }
            SetVisible(false);
            SetCamera(false);
        }

        private void OnDisable()
        {
            Finish(false);
        }

        /// <summary>Uses the main camera's environment settings without moving that camera.</summary>
        public void MatchCamera(Camera source)
        {
            if (source == null || source == _camera)
            {
                return;
            }
            float lens = _camera.fieldOfView;
            _camera.CopyFrom(source);
            _camera.fieldOfView = lens;
            _camera.enabled = false;
            Component from = source.GetComponent("UniversalAdditionalCameraData");
            Component to = _camera.GetComponent("UniversalAdditionalCameraData");
            if (from != null && to != null)
            {
                foreach (string name in new[] { "renderPostProcessing", "volumeLayerMask", "antialiasing", "antialiasingQuality", "dithering" })
                {
                    var property = from.GetType().GetProperty(name);
                    if (property != null && property.CanRead && property.CanWrite)
                    {
                        property.SetValue(to, property.GetValue(from, null), null);
                    }
                }
            }
        }

        /// <summary>Runs the Timeline on these persistent, real tentacle objects.</summary>
        public void Synchronize(double elapsed)
        {
            if (!_started)
            {
                _started = true;
                SetVisible(true);
                SetCamera(true);
                _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                _director.extrapolationMode = DirectorWrapMode.Hold;
                _director.time = Math.Min(elapsed, _director.duration);
                _director.Play();
            }
            if (Math.Abs(_director.time - elapsed) > 0.1d)
            {
                _director.time = Math.Min(elapsed, _director.duration);
                _director.Evaluate();
            }
        }

        /// <summary>Holds the final pose for combat, or hides actors when cancelled.</summary>
        public void Finish(bool completed)
        {
            SetCamera(false);
            if (_director != null)
            {
                if (completed && IsReady)
                {
                    SetVisible(true);
                    _director.time = _director.duration;
                    _director.Evaluate();
                    _director.Pause();
                }
                else
                {
                    _director.Stop();
                    SetVisible(false);
                }
            }
            _started = false;
        }

        private void SetCamera(bool value)
        {
            if (_camera != null)
            {
                _camera.enabled = value;
            }
            if (_listener != null)
            {
                _listener.enabled = value;
            }
        }

        private void SetVisible(bool value)
        {
            if (_renderers == null)
            {
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = value && _rendererEnabled[i];
                }
            }
        }
    }
}
