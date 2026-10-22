#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Modules.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Debris.Server;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Modules
{
    // JH04 stand-in for World's shore services, which only exist on the generated map (2026-10-22, T34).
    // On the server it answers shore spots from hand-placed points and runs World's real lifetime tracker on a local clock.
    // The host also gets a button that drops their own carried module as if they had not been rescued, standing in
    // for Player's respawn result. When World's services are already there, it leaves them alone.
    [DisallowMultipleComponent]
    public sealed class ShoreDropStub : MonoBehaviour, IShoreDropLocator
    {
        private const float Width = 300f;

        [Tooltip("Shore spots on the ground. A request's slot picks one, wrapping around.")]
        [SerializeField]
        private Transform[] _points = new Transform[0];
        [Tooltip("Lifetime source. A short module lifetime makes rotting easy to watch.")]
        [SerializeField]
        private DebrisConfig _config;
        [SerializeField]
        private ModuleInteractionHandler _modules;
        [Tooltip("Answer every spot request with ShoreNotFound, to try the failure path.")]
        [SerializeField]
        private bool _isFailing;
        [SerializeField]
        private float _top = 10f;

        private DebrisLifetimeTracker _tracker;
        private bool _isBound;
        private string _lastResult = "-";

        private void Update()
        {
            if (!NetworkServer.active)
            {
                Release();
                return;
            }

            if (!_isBound)
            {
                Register();
            }

            if (_tracker != null)
            {
                _tracker.Tick();
            }
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnGUI()
        {
            if (!NetworkServer.active || !NetworkClient.localPlayer || !_modules)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10f, _top, Width, 110f), GUI.skin.box);
            GUILayout.Label($"해안 드롭 시험 (호스트)  마지막: {_lastResult}");
            GUILayout.Label($"만료 대기 {(_tracker != null ? _tracker.Count : 0)}개");
            _isFailing = GUILayout.Toggle(_isFailing, "위치 찾기 실패로 응답");

            if (GUILayout.Button("내 모듈을 해안에 떨어뜨리기 (미구조 가정)"))
            {
                LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
                bool isDropped = manager && manager.Connections.TryGetActorId(NetworkServer.localConnection, out uint actorId)
                    && _modules.TryDropCarriedToShore(actorId, NetworkClient.localPlayer.transform.position);
                _lastResult = isDropped ? "떨어뜨림" : "실패(변화 없음)";
            }

            GUILayout.EndArea();
        }

        public ShoreDropResult GetDropPoint(in ShoreDropRequest request)
        {
            if (_isFailing)
            {
                return ShoreDropResult.Fail(ShoreFailure.ShoreNotFound);
            }

            if (_points.Length == 0)
            {
                return ShoreDropResult.Fail(ShoreFailure.NoCandidateIsland);
            }

            Transform point = _points[Mathf.Abs(request.Slot) % _points.Length];
            return point ? ShoreDropResult.Success(point.position, ShoreIslandSelector.LighthouseIslandIndex)
                : ShoreDropResult.Fail(ShoreFailure.UnsafeSpot);
        }

        private void Register()
        {
            if (!WorldServices.TryGet(out IShoreDropLocator _))
            {
                WorldServices.Register<IShoreDropLocator>(this);
            }

            if (_config && !WorldServices.TryGet(out IDebrisTracker _))
            {
                _tracker = new DebrisLifetimeTracker(new LocalClock(), _config);
                WorldServices.Register<IDebrisTracker>(_tracker);
            }

            // Also when World's services were already there, so this is not retried every frame.
            _isBound = true;
        }

        private void Release()
        {
            WorldServices.Unregister<IShoreDropLocator>(this);
            _isBound = false;

            if (_tracker != null)
            {
                WorldServices.Unregister<IDebrisTracker>(_tracker);
                _tracker.Dispose();
                _tracker = null;
            }
        }
    }
}
#endif
