using Mirror;
using Lighthouse.Tower.View;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Lighthouse.Dev.Cinematics
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class SummonCinematicPreview : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private AudioListener _listener;
        private bool _isStandalone;
        private MainSceneSummonPresentation _presentation;
        private Camera _gameplayCamera;
        private AudioListener _gameplayListener;
        private bool _cameraWasEnabled;
        private bool _listenerWasEnabled;

        private void Awake()
        {
            if (_director == null)
            {
                _director = GetComponent<PlayableDirector>();
            }
            if (_previewCamera != null)
            {
                _previewCamera.enabled = false;
            }
            if (_listener != null)
            {
                _listener.enabled = false;
            }
        }

        private void Start()
        {
            Replay();
        }

        [ContextMenu("Preview/Replay")]
        public void Replay()
        {
            // Network presentation remains exclusively controlled by SummonCinematicView.
            if (!Application.isPlaying || NetworkClient.active || NetworkServer.active || SceneManager.sceneCount != 1
                || _director == null || _previewCamera == null)
            {
                return;
            }
            _isStandalone = true;
            _presentation = GetComponentInParent<MainSceneSummonPresentation>();
            if (_presentation != null)
            {
                if (_gameplayCamera == null)
                {
                    _gameplayCamera = Camera.main;
                    if (_gameplayCamera != null && _gameplayCamera != _previewCamera)
                    {
                        _cameraWasEnabled = _gameplayCamera.enabled;
                        _gameplayListener = _gameplayCamera.GetComponent<AudioListener>();
                        _listenerWasEnabled = _gameplayListener != null && _gameplayListener.enabled;
                    }
                }
                _presentation.MatchCamera(_gameplayCamera);
                if (_gameplayCamera != null && _gameplayCamera != _previewCamera)
                {
                    _gameplayCamera.enabled = false;
                    if (_gameplayListener != null)
                    {
                        _gameplayListener.enabled = false;
                    }
                }
                _presentation.Synchronize(0d);
                _director.extrapolationMode = DirectorWrapMode.Loop;
                return;
            }
            _previewCamera.enabled = true;
            if (_listener != null)
            {
                _listener.enabled = true;
            }
            _director.Stop();
            _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            _director.extrapolationMode = DirectorWrapMode.Loop;
            _director.time = 0d;
            _director.Play();
        }

        private void OnDisable()
        {
            if (!_isStandalone)
            {
                return;
            }
            if (_presentation != null)
            {
                _presentation.Finish(false);
            }
            if (_gameplayCamera != null && _gameplayCamera != _previewCamera)
            {
                _gameplayCamera.enabled = _cameraWasEnabled;
            }
            if (_gameplayListener != null)
            {
                _gameplayListener.enabled = _listenerWasEnabled;
            }
            if (_director != null)
            {
                _director.Stop();
            }
            if (_previewCamera != null)
            {
                _previewCamera.enabled = false;
            }
            if (_listener != null)
            {
                _listener.enabled = false;
            }
        }
    }
}
