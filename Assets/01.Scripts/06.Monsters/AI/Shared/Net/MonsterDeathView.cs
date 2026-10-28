using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    public sealed class MonsterDeathView : NetworkBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _deathClip;
        [SerializeField] private Behaviour _presentationToDisable;
        [SerializeField] private GameObject _revealOnDeath;
        [SerializeField] private GameObject _hideOnDeath;

        [SyncVar] private double _deathStartedAt;
        [SyncVar(hook = nameof(OnDeathChanged))] private bool _hasDied;

        private MonsterBase _monster;
        private bool _hasStartedPresentation;
        private bool _hasFinishedPresentation;
        private bool _animatorWasEnabled;

        public float Duration => _deathClip != null ? _deathClip.length : 0f;

        private void Awake()
        {
            _monster = GetComponent<MonsterBase>();
        }

        private void LateUpdate()
        {
            if (!isClient || !_hasDied || _hasFinishedPresentation || _animator == null || _deathClip == null)
            {
                return;
            }
            BeginPresentation();
            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _deathStartedAt);
            _deathClip.SampleAnimation(_animator.gameObject, Mathf.Min(elapsed, _deathClip.length));
            _hasFinishedPresentation = elapsed >= _deathClip.length;
        }

        private void OnDisable()
        {
            RestoreAnimator();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_monster != null)
            {
                _monster.Died += HandleDied;
            }
        }

        public override void OnStopServer()
        {
            if (_monster != null)
            {
                _monster.Died -= HandleDied;
            }
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_hasDied)
            {
                BeginPresentation();
            }
        }

        public override void OnStopClient()
        {
            RestoreAnimator();
            base.OnStopClient();
        }

        [Server]
        private void HandleDied(MonsterBase monster)
        {
            if (_hasDied || monster != _monster)
            {
                return;
            }
            _deathStartedAt = NetworkTime.time;
            _hasDied = true;
        }

        private void OnDeathChanged(bool oldValue, bool newValue)
        {
            if (newValue && isClient)
            {
                BeginPresentation();
            }
        }

        private void BeginPresentation()
        {
            if (_hasStartedPresentation || _animator == null || _deathClip == null)
            {
                return;
            }
            _hasStartedPresentation = true;
            if (_presentationToDisable != null)
            {
                _presentationToDisable.enabled = false;
            }
            if (_hideOnDeath != null)
            {
                _hideOnDeath.SetActive(false);
            }
            if (_revealOnDeath != null)
            {
                _revealOnDeath.SetActive(true);
            }
            _animatorWasEnabled = _animator.enabled;
            _animator.enabled = false;
        }

        private void RestoreAnimator()
        {
            if (!_hasStartedPresentation)
            {
                return;
            }
            if (_animator != null)
            {
                _animator.enabled = _animatorWasEnabled;
            }
            _hasStartedPresentation = false;
            _hasFinishedPresentation = false;
        }
    }
}
