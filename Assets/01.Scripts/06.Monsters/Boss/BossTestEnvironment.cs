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
        [SyncVar] private BossPhase _phase;

        private double _attackPower;

        private Font _font;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;

        public BossSettings Settings => _settings;
        public Transform LighthouseModel => _lighthouseModel;
        public Transform TentacleModel => _tentacleModel;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public BossPhase Phase => _phase;
        public bool HasSummoned => _phase != BossPhase.WaitingForSummon;
        public bool HasValidSetup => _settings != null && _lighthouseModel != null
            && _tentacleModel != null && _voting != null && _settings.TestLighthouseMaxHealth > 0
            && _settings.AttackInterval > 0 && _settings.SpecialEndingAttackThreshold > 0;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
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
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _voting.SummonApproved += HandleSummonApproved;
        }

        public override void OnStopServer()
        {
            if (_voting != null)
            {
                _voting.SummonApproved -= HandleSummonApproved;
            }
            _attackPower = 0;
            _phase = BossPhase.WaitingForSummon;
            _currentHealth = 0;
            _maxHealth = 0;
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
            if (HasSummoned || !HasValidSetup || double.IsNaN(snapshot.AttackPower)
                || double.IsInfinity(snapshot.AttackPower) || snapshot.AttackPower < 0)
            {
                return;
            }

            // 고정된 공격력은 서버에만 보관하고, 클라이언트에는 진행 분기만 공개한다.
            _attackPower = snapshot.AttackPower;
            _phase = _attackPower >= _settings.SpecialEndingAttackThreshold ? BossPhase.SpecialEndingReady : BossPhase.NormalBattleReady;
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
            GUILayout.Label("보스 시험 환경 · 2단계", _labelStyle);
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
                    : _phase == BossPhase.SpecialEndingReady ? "특수 종료 연출 준비" : "소환 대기";
                GUILayout.Label($"소환: {(HasSummoned ? "승인됨" : "미승인")} · {phaseLabel}", _labelStyle);
                if (isServer && HasSummoned)
                {
                    GUILayout.Label($"고정 공격력: {_attackPower:0.##} (서버 전용)", _labelStyle);
                }
                GUILayout.Label("임시 테스트값 · 피해/연출/종료 미연결", _labelStyle);
            }
            GUILayout.EndArea();
        }

    }
}
