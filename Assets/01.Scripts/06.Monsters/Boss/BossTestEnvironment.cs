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

        [SyncVar] private float _currentHealth;
        [SyncVar] private float _maxHealth;

        private Font _font;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;

        public BossSettings Settings => _settings;
        public Transform LighthouseModel => _lighthouseModel;
        public Transform TentacleModel => _tentacleModel;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool HasValidSetup => _settings != null && _lighthouseModel != null
            && _tentacleModel != null && _settings.TestLighthouseMaxHealth > 0
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
        }

        public override void OnStopServer()
        {
            _currentHealth = 0;
            _maxHealth = 0;
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
                Mathf.Max(16, Screen.height - 170), width, 154), _panelStyle);
            GUILayout.Label("보스 시험 환경 · 1단계", _labelStyle);
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
                GUILayout.Label("임시 테스트값 · 소환/공격 미연결", _labelStyle);
            }
            GUILayout.EndArea();
        }

    }
}
