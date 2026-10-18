using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DisallowMultipleComponent]
    public sealed class TentacleAnimationView : MonoBehaviour
    {
        private const float LastFrameNormalizedTime = 0.9999f;
        private static readonly int ClimbHash = Animator.StringToHash("Base Layer.Climb");
        private static readonly int WrappedHash = Animator.StringToHash("Base Layer.Wrapped");
        private static readonly int SqueezeHash = Animator.StringToHash("Base Layer.Squeeze");

        [SerializeField] private BossTestEnvironment _boss;
        [SerializeField] private Animator _animator;

        private Renderer[] _renderers;
        private bool[] _originalRendererEnabled;
        private bool _hasVisibility;
        private bool _isVisible;
        private int _sampledStateHash;
        private float _sampledNormalizedTime = -1;

        public bool HasValidSetup => _boss != null && _animator != null
            && _animator.runtimeAnimatorController != null;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalRendererEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalRendererEnabled[i] = _renderers[i].enabled;
            }
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.speed = 0;
            }
            SetVisible(false);
        }

        private void LateUpdate()
        {
            // 씬 네트워크 객체는 스폰 전 Awake가 실행되지 않으므로 isClient를 직접 조회하지 않는다.
            bool visible = NetworkClient.isConnected && NetworkClient.ready && HasValidSetup && _boss.MaxHealth > 0
                && _boss.HasSummoned && _boss.WrapDuration > 0 && _boss.SqueezeDuration > 0;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            bool hasStopped = _boss.Phase == BossPhase.LighthouseDestroyed
                || _boss.Phase == BossPhase.BattleStopped;
            double now = hasStopped ? _boss.VisualStoppedAt : NetworkTime.time;
            double wrapElapsed = System.Math.Max(0d, now - _boss.WrapStartedAt);
            int stateHash = WrappedHash;
            float normalizedTime = 0;
            if (wrapElapsed < _boss.WrapDuration)
            {
                stateHash = ClimbHash;
                normalizedTime = (float)(wrapElapsed / _boss.WrapDuration);
            }
            else if (_boss.AttackSequence > 0)
            {
                double attackElapsed = System.Math.Max(0d, now - _boss.AttackStartedAt);
                if (attackElapsed < _boss.SqueezeDuration)
                {
                    stateHash = SqueezeHash;
                    normalizedTime = (float)(attackElapsed / _boss.SqueezeDuration);
                }
            }

            normalizedTime = Mathf.Min(normalizedTime, LastFrameNormalizedTime);
            if (_sampledStateHash == stateHash && _sampledNormalizedTime == normalizedTime)
            {
                return;
            }

            // 서버 시작 시각으로 자세를 구해 수신이 늦어도 처음부터 재생하지 않는다.
            _animator.Play(stateHash, 0, normalizedTime);
            _animator.Update(0);
            _sampledStateHash = stateHash;
            _sampledNormalizedTime = normalizedTime;
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_hasVisibility && _isVisible == visible)
            {
                return;
            }
            _hasVisibility = true;
            _isVisible = visible;
            _sampledNormalizedTime = -1;
            if (_animator != null)
            {
                // 자동 재생을 막아 소환 전 노출과 호스트의 이중 재생을 방지한다.
                _animator.speed = 0;
                _animator.enabled = visible;
            }
            if (_renderers == null)
            {
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible && _originalRendererEnabled[i];
                }
            }
        }
    }
}
