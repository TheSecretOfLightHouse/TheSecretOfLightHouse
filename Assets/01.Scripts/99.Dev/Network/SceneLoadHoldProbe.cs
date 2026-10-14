#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // JH07 test aid: reproduces a slow game scene load on a remote client by holding scene activation.
    // Unlike suspending the process, the client keeps running, so transport events arrive mid-load as they would on a slow PC.
    // Acts only with the "-jh07HoldSceneLoad <seconds>" command-line flag and never on the host.
    [DisallowMultipleComponent]
    public sealed class SceneLoadHoldProbe : MonoBehaviour
    {
        private const string HoldFlag = "-jh07HoldSceneLoad";

        [SerializeField]
        private LighthouseNetworkManager _manager;

        private float _holdSeconds;
        private AsyncOperation _heldLoad;
        private double _releaseAt;

        private void Awake()
        {
            if (!_manager)
            {
                _manager = GetComponent<LighthouseNetworkManager>();
            }

            _holdSeconds = ReadHoldSeconds();
        }

        private void Update()
        {
            if (_holdSeconds <= 0f || !_manager)
            {
                return;
            }

            AsyncOperation load = NetworkManager.loadingSceneAsync;

            if (_heldLoad == null)
            {
                // Only the lobby -> game load; the state message arrives before the scene message.
                if (load != null && !NetworkServer.active && _manager.ClientState.Phase == MatchPhase.Loading)
                {
                    _heldLoad = load;
                    _heldLoad.allowSceneActivation = false;
                    _releaseAt = Time.unscaledTimeAsDouble + _holdSeconds;
                    Debug.Log($"[{nameof(SceneLoadHoldProbe)}] Holding game scene activation for {_holdSeconds}s.");
                }

                return;
            }

            if (Time.unscaledTimeAsDouble >= _releaseAt)
            {
                _heldLoad.allowSceneActivation = true;
                _heldLoad = null;
                _holdSeconds = 0f;
                Debug.Log($"[{nameof(SceneLoadHoldProbe)}] Released game scene activation.");
            }
        }

        private static float ReadHoldSeconds()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == HoldFlag
                    && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                {
                    return seconds;
                }
            }

            return 0f;
        }
    }
}
#endif
