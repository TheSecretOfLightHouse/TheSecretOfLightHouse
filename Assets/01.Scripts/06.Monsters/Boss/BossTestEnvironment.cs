using System.Collections;
using Lighthouse.Mafia.AbyssGauge.Rules;
using Lighthouse.Mafia.Voting.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class BossTestEnvironment : NetworkBehaviour
    {
        [SerializeField] private BossSettings _settings;
        [SerializeField] private Transform _lighthouseModel;
        [SerializeField] private Transform _tentacleModel;
        [SerializeField] private VotingNetworkController _voting;

        [SyncVar] private float _currentHealth;
        [SyncVar] private float _maxHealth;
        [SyncVar] private float _wrapDuration;
        [SyncVar] private float _squeezeDuration;
        [SyncVar] private double _wrapStartedAt;
        [SyncVar] private double _attackStartedAt;
        [SyncVar] private double _visualStoppedAt;
        [SyncVar] private uint _attackSequence;
        [SyncVar] private bool _isSpecialEnding;
        [SyncVar] private double _specialEndingStartedAt;
        [SyncVar] private BossPhase _phase;

        private double _attackPower;
        private Coroutine _damageRoutine;

        private Font _font;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;

        public BossSettings Settings => _settings;
        public Transform LighthouseModel => _lighthouseModel;
        public Transform TentacleModel => _tentacleModel;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public BossPhase Phase => _phase;
        public float WrapDuration => _wrapDuration;
        public float SqueezeDuration => _squeezeDuration;
        public double WrapStartedAt => _wrapStartedAt;
        public double AttackStartedAt => _attackStartedAt;
        public double VisualStoppedAt => _visualStoppedAt;
        public uint AttackSequence => _attackSequence;
        public bool IsSpecialEnding => _isSpecialEnding;
        public double SpecialEndingStartedAt => _specialEndingStartedAt;
        public event System.Action SpecialEndingStarted;
        public event System.Action SpecialEndingCompleted;
        public bool HasSummoned => _phase != BossPhase.WaitingForSummon;
        public bool HasValidSetup => _settings != null && _lighthouseModel != null
            && _tentacleModel != null && _voting != null && IsPositiveFinite(_settings.TestLighthouseMaxHealth)
            && IsPositiveFinite(_settings.AttackInterval) && IsPositiveFinite(_settings.SpecialEndingAttackThreshold)
            && IsPositiveFinite(_settings.WrapDuration) && IsPositiveFinite(_settings.SqueezeDuration)
            && IsPositiveFinite(_settings.DamageNormalizedTime) && _settings.DamageNormalizedTime <= 1
            && _settings.AttackInterval >= _settings.SqueezeDuration;

        private bool CanContinueBattle => isServer && isActiveAndEnabled && _currentHealth > 0
            && (_phase == BossPhase.Wrapping || _phase == BossPhase.NormalBattleActive);

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
        }

        private void OnDisable()
        {
            StopDamageRoutine();
            if (isServer)
            {
                StopBattle();
            }
        }

        public override void OnStartServer()
        {
            if (!HasValidSetup)
            {
                Debug.LogError($"[{nameof(BossTestEnvironment)}] Assign valid settings and both test models.", this);
                return;
            }

            _maxHealth = _settings.TestLighthouseMaxHealth;
            _currentHealth = _maxHealth;
            _wrapDuration = _settings.WrapDuration;
            _squeezeDuration = _settings.SqueezeDuration;
            _wrapStartedAt = 0;
            _attackStartedAt = 0;
            _visualStoppedAt = 0;
            _attackSequence = 0;
            _isSpecialEnding = false;
            _specialEndingStartedAt = 0;
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _voting.SummonApproved += HandleSummonApproved;
            _voting.VotingEnded += HandleVotingEnded;
        }

        public override void OnStopServer()
        {
            StopDamageRoutine();
            if (_voting != null)
            {
                _voting.SummonApproved -= HandleSummonApproved;
                _voting.VotingEnded -= HandleVotingEnded;
            }
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _currentHealth = 0;
            _maxHealth = 0;
            _wrapDuration = 0;
            _squeezeDuration = 0;
            _wrapStartedAt = 0;
            _attackStartedAt = 0;
            _visualStoppedAt = 0;
            _attackSequence = 0;
            _isSpecialEnding = false;
            _specialEndingStartedAt = 0;
        }

        [Server]
        public bool TryStartSpecialEnding()
        {
            if (!isActiveAndEnabled || _phase != BossPhase.SpecialEndingReady)
            {
                return false;
            }
            _specialEndingStartedAt = NetworkTime.time;
            _phase = BossPhase.SpecialEndingPlaying;
            SpecialEndingStarted?.Invoke();
            return true;
        }

        [Server]
        public bool TryCompleteSpecialEnding()
        {
            if (!isActiveAndEnabled || _phase != BossPhase.SpecialEndingPlaying)
            {
                return false;
            }

            // 서버에서 특수 종료 연출이 완료되면 승패 연결을 위해 상태를 갱신한다.
            _phase = BossPhase.SpecialEndingCompleted;
            SpecialEndingCompleted?.Invoke();
            return true;
        }

        [Server]
        public bool TryGetAttackPower(out double attackPower)
        {
            attackPower = _attackPower;
            return HasSummoned;
        }

        [Server]
        private void HandleSummonApproved(AbyssGaugeSnapshot snapshot)
        {
            if (HasSummoned || !isActiveAndEnabled || !HasValidSetup || double.IsNaN(snapshot.AttackPower)
                || double.IsInfinity(snapshot.AttackPower) || snapshot.AttackPower < 0)
            {
                return;
            }

            // 고정된 공격력은 서버에만 보관하고, 클라이언트에는 진행 분기만 공개한다.
            _attackPower = snapshot.AttackPower;
            _isSpecialEnding = _attackPower >= _settings.SpecialEndingAttackThreshold;
            _wrapStartedAt = NetworkTime.time;
            _phase = _isSpecialEnding ? BossPhase.SpecialEndingReady : BossPhase.NormalBattleReady;
            if (_isSpecialEnding)
            {
                // 표현이 준비될 때까지 자동 완료하지 않고 서버 완료 연결을 기다린다.
                TryStartSpecialEnding();
            }
            if (_phase == BossPhase.NormalBattleReady)
            {
                _phase = BossPhase.Wrapping;
                _damageRoutine = StartCoroutine(DealPeriodicDamage(_settings.AttackInterval,
                    _squeezeDuration * _settings.DamageNormalizedTime));
            }
        }

        private IEnumerator DealPeriodicDamage(float attackInterval, float damageDelay)
        {
            // 클라이언트의 애니메이션 콜백 대신 서버의 시계로 감기와 타격 시점을 정한다.
            yield return new WaitForSecondsRealtime(_wrapDuration);
            if (!CanContinueBattle)
            {
                _damageRoutine = null;
                yield break;
            }

            _phase = BossPhase.NormalBattleActive;
            yield return new WaitForSecondsRealtime(attackInterval);
            var hitDelay = new WaitForSecondsRealtime(damageDelay);
            var recoveryDelay = new WaitForSecondsRealtime(attackInterval - damageDelay);
            while (CanContinueBattle)
            {
                _attackStartedAt = NetworkTime.time;
                _attackSequence++;
                yield return hitDelay;
                if (!CanContinueBattle)
                {
                    break;
                }

                // 고정 공격력의 정밀도를 유지하도록 double로 차감한 뒤 동기화할 HP 값으로 변환한다.
                _currentHealth = (float)System.Math.Max(0d, _currentHealth - _attackPower);
                if (_currentHealth <= 0)
                {
                    _visualStoppedAt = NetworkTime.time;
                    _phase = BossPhase.LighthouseDestroyed;
                }
                else if (recoveryDelay.waitTime > 0)
                {
                    yield return recoveryDelay;
                }
            }
            _damageRoutine = null;
        }

        [Server]
        private void HandleVotingEnded()
        {
            StopBattle();
        }

        [Server]
        private void StopBattle()
        {
            StopDamageRoutine();
            if (HasSummoned && _phase != BossPhase.LighthouseDestroyed && _phase != BossPhase.BattleStopped
                && _phase != BossPhase.SpecialEndingCompleted)
            {
                _visualStoppedAt = NetworkTime.time;
                _phase = BossPhase.BattleStopped;
            }
        }

        private void StopDamageRoutine()
        {
            if (_damageRoutine != null)
            {
                StopCoroutine(_damageRoutine);
                _damageRoutine = null;
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }

        private void OnGUI()
        {
            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 8, 8)
                };
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = _font,
                    fontSize = 14,
                    wordWrap = true
                };
            }

            float leftWidth = Mathf.Min(540, Screen.width - 32);
            if (NetworkServer.active)
            {
                leftWidth = Mathf.Min(leftWidth, (Screen.width - 48) * 0.62f);
            }
            float width = Mathf.Max(140, Mathf.Min(300, Screen.width - leftWidth - 48));
            GUILayout.BeginArea(new Rect(Screen.width - width - 16,
                Mathf.Max(16, Screen.height - 224), width, 208), _panelStyle);
            GUILayout.Label("보스 시험 환경 · 5단계 연결", _labelStyle);
            if (!HasValidSetup)
            {
                GUILayout.Label("보스 설정과 모델 참조를 확인하세요.", _labelStyle);
            }
            else
            {
                bool hasServerState = (isServer || isClient) && _maxHealth > 0;
                float maxHealth = hasServerState ? _maxHealth : _settings.TestLighthouseMaxHealth;
                float health = hasServerState ? _currentHealth : maxHealth;
                GUILayout.Label($"등대 HP: {health:0.##} / {maxHealth:0.##}", _labelStyle);
                GUILayout.Label(hasServerState ? "서버 HP 상태" : "접속 전 설정 미리보기", _labelStyle);
                GUILayout.Label($"공격 간격 {_settings.AttackInterval:0.##}초 · 특수 기준 {_settings.SpecialEndingAttackThreshold:0.##}", _labelStyle);
                string phaseLabel = _phase == BossPhase.NormalBattleReady ? "일반 전투 준비"
                    : _phase == BossPhase.Wrapping ? "촉수 감기"
                    : _phase == BossPhase.NormalBattleActive ? "일반 전투 진행"
                    : _phase == BossPhase.SpecialEndingReady ? "특수 종료 연출 준비"
                    : _phase == BossPhase.SpecialEndingPlaying ? "크툴루 등장 연출 대기(표현 미구현)"
                    : _phase == BossPhase.SpecialEndingCompleted ? "특수 연출 완료·승패 연결 대기"
                    : _phase == BossPhase.LighthouseDestroyed ? "등대 HP 소진"
                    : _phase == BossPhase.BattleStopped ? "시험 전투 중지" : "소환 대기";
                GUILayout.Label($"소환: {(HasSummoned ? "승인됨" : "미승인")} · {phaseLabel}", _labelStyle);
                if (isServer && HasSummoned)
                {
                    GUILayout.Label($"고정 공격력: {_attackPower:0.##} (서버 전용)", _labelStyle);
                }
                GUILayout.Label("촉수 연출·시험 피해 연결 · 본편 승패 미연결", _labelStyle);
            }
            GUILayout.EndArea();
        }

    }
}
