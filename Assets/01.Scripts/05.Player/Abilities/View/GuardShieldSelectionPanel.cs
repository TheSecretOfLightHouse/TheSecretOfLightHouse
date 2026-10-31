using Lighthouse.Player.Abilities.View;
using UnityEngine;
using Lighthouse.Network.Session.View;
using Lighthouse.Player.Abilities.Net;
using Lighthouse.Player.Abilities.Rules;

public sealed class GuardShieldSelectionPanel : MonoBehaviour
{
    [SerializeField]
    private GameObject _panelRoot;

    [SerializeField]
    private UnityEngine.UI.Button[] _playerButtons;

    [SerializeField]
    private UnityEngine.UI.Text[] _playerNames;

    [SerializeField]
    private UnityEngine.UI.Button _closeButton;

    [SerializeField]
    private UnityEngine.UI.Text _message;

    private readonly PlayerController[] _targets = new PlayerController[4];
    private readonly System.Collections.Generic.List<PlayerController> _players =
        new System.Collections.Generic.List<PlayerController>(4);
    private readonly UnityEngine.Events.UnityAction[] _clickHandlers =
    new UnityEngine.Events.UnityAction[4];

    private AbilityTargetPresentation _targetPresentation;
    private UnityEngine.UI.Image[] _targetIcons;
    private Font _runtimeFont;
    private GuardShieldAbility _boundAbility;
    private PlayerController _boundController;
    private Mirror.NetworkIdentity _boundPlayer;
    private bool _isOpen;
    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;
    private UnityEngine.InputSystem.PlayerInput _playerInput;
    private bool _wasInputEnabled;
    private GuardShieldRequest _request;

    public event System.Action<PlayerController> TargetSelected;

    private void Awake()
    {
        _targetPresentation = Resources.Load<AbilityTargetPresentation>("AbilityTargetPresentation");
        _targetIcons = AbilityTargetPresentation.CreateIcons(_playerButtons, _playerNames);
        if (_panelRoot)
        {
            // Dynamic OS fonts must be created in each process rather than saved into the scene.
            _runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);

            foreach (UnityEngine.UI.Text label in _panelRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                label.font = _runtimeFont;
                label.fontStyle = FontStyle.Bold;
            }
        }

        for (int i = 0; i < _clickHandlers.Length; i++)
        {
            if (_playerButtons == null
                || i >= _playerButtons.Length
                || !_playerButtons[i])
            {
                continue;
            }

            int index = i;
            _clickHandlers[i] = () => SelectTarget(index);
            _playerButtons[i].onClick.AddListener(_clickHandlers[i]);
        }

        Hide();
    }

    private void Update()
    {
        Mirror.NetworkIdentity localPlayer =
            Mirror.NetworkClient.localPlayer;

        if (_isOpen && (!localPlayer || !_boundAbility
            || !_boundAbility.isActiveAndEnabled || _boundAbility.IsFinished
            || !GuardShieldAbility.IsAboardOwnedBoat(_boundController)))
        {
            Hide();
        }

        if (_boundPlayer == localPlayer)
        {
            return;
        }

        Hide();
        BindAbility(null);
        _boundPlayer = localPlayer;
        _playerInput = _boundPlayer
            ? _boundPlayer.GetComponent<UnityEngine.InputSystem.PlayerInput>()
            : null;

        if (_boundPlayer
            && _boundPlayer.TryGetComponent(out GuardShieldAbility ability))
        {
            BindAbility(ability);
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        BindAbility(null);

        for (int i = 0; i < _clickHandlers.Length; i++)
        {
            if (_playerButtons != null
                && i < _playerButtons.Length
                && _playerButtons[i]
                && _clickHandlers[i] != null)
            {
                _playerButtons[i].onClick.RemoveListener(_clickHandlers[i]);
            }
        }
        if (_runtimeFont)
        {
            Destroy(_runtimeFont);
        }
    }

    public void Show()
    {
        if (!_panelRoot || _isOpen || !isActiveAndEnabled)
        {
            return;
        }

        if (!GuardShieldAbility.IsAboardOwnedBoat(_boundController))
        {
            if (_boundAbility)
            {
                _boundAbility.CompleteSelection();
            }
            return;
        }

        if (SessionScreen.IsGameplayOverlayOpen)
        {
            if (_boundAbility)
            {
                _boundAbility.CompleteSelection();
            }

            return;
        }

        if (_request && _request.IsAwaitingResult)
        {
            if (_boundAbility)
            {
                _boundAbility.CompleteSelection();
            }

            return;
        }

        _savedCursorLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        _isOpen = true;

        _wasInputEnabled = _playerInput && _playerInput.enabled;
        SessionScreen.SetGameplayOverlayOpen(this, true);
        if (_playerInput)
        {
            _playerInput.enabled = false;
        }

        RefreshTargets();
        if (_closeButton)
        {
            _closeButton.interactable = true;
        }
        SetMessage("선택한 배가 받는 피해를 1회 완전히 막습니다");
        _panelRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (_panelRoot)
        {
            _panelRoot.SetActive(false);
        }

        if (_isOpen)
        {
            _isOpen = false;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;

            if (_playerInput && _wasInputEnabled)
            {
                _playerInput.enabled = true;
            }

            _wasInputEnabled = false;
        }

        if (_boundAbility)
        {
            _boundAbility.CompleteSelection();
        }
    }

    private void RefreshTargets()
    {
        _players.Clear();

        foreach (Mirror.NetworkIdentity identity in Mirror.NetworkClient.spawned.Values)
        {
            if (identity
                && identity.TryGetComponent(out PlayerController player)
                && player.isActiveAndEnabled)
            {
                _players.Add(player);
            }
        }

        _players.Sort((a, b) => a.ActorID.CompareTo(b.ActorID));

        for (int i = 0; i < _targets.Length; i++)
        {
            PlayerController player = i < _players.Count ? _players[i] : null;
            _targets[i] = player;

            if (_targetPresentation && _targetIcons != null && i < _targetIcons.Length)
            {
                _targetPresentation.BindIcon(_targetIcons[i], player);
            }

            if (_playerButtons == null || i >= _playerButtons.Length || !_playerButtons[i])
            {
                continue;
            }

            _playerButtons[i].gameObject.SetActive(player != null);
            _playerButtons[i].interactable = false;

            if (_playerNames == null || i >= _playerNames.Length || !_playerNames[i])
            {
                continue;
            }

            if (!player)
            {
                _playerNames[i].text = string.Empty;
                continue;
            }

            _playerNames[i].text = AbilityTargetPresentation.GetJobName(player);

            BoatController boat = player.OwnedBoat;
            bool canProtect = GuardShieldAbility.IsAboardOwnedBoat(player)
                && boat.TryGetComponent(out BoatShield shield)
                && shield.isActiveAndEnabled && !shield.HasShield
                && (!_request || !_request.IsAwaitingResult);

            _playerButtons[i].interactable = canProtect;

            if (!canProtect)
            {
                _playerNames[i].text += " · 보호 가능한 배 없음";
            }
        }
    }
    private void SelectTarget(int index)
    {
        if (!_isOpen || index < 0 || index >= _targets.Length
            || (_request && _request.IsAwaitingResult))
        {
            return;
        }

        PlayerController target = _targets[index];

        if (!GuardShieldAbility.IsAboardOwnedBoat(_boundController))
        {
            Hide();
            return;
        }

        if (!GuardShieldAbility.IsAboardOwnedBoat(target))
        {
            RefreshTargets();
            SetMessage("배에 탑승 중인 선원을 선택해주세요.");
            return;
        }

        BoatController boat = target.OwnedBoat;

        if (!boat || boat.IsWrecked
            || !boat.TryGetComponent(out BoatShield shield)
            || !shield.isActiveAndEnabled)
        {
            RefreshTargets();
            return;
        }

        if (!_request)
        {
            SetMessage("경비병 요청 컴포넌트 연결을 확인해주세요.");
            return;
        }

        SetMessage("쉴드 부여를 요청하는 중입니다...");
        foreach (UnityEngine.UI.Button button in _playerButtons)
        {
            if (button)
            {
                button.interactable = false;
            }
        }
        if (_closeButton)
        {
            _closeButton.interactable = false;
        }

        if (!_request.TryRequest(target))
        {
            RefreshTargets();
            if (_closeButton)
            {
                _closeButton.interactable = true;
            }
            SetMessage("요청을 보내지 못했습니다. 접속 상태를 확인해주세요.");
            return;
        }

        TargetSelected?.Invoke(target);
    }

    public void BindAbility(GuardShieldAbility ability)
    {
        if (_request)
        {
            _request.Responded -= HandleShieldResponse;
        }

        if (_boundAbility)
        {
            _boundAbility.SelectionRequested -= Show;
        }

        _boundAbility = ability;
        _boundController = ability ? ability.GetComponent<PlayerController>() : null;
        _request = ability ? ability.GetComponent<GuardShieldRequest>() : null;

        if (_boundAbility)
        {
            _boundAbility.SelectionRequested += Show;
        }

        if (_request)
        {
            _request.Responded += HandleShieldResponse;
        }
    }

    private void HandleShieldResponse(GuardShieldResult result)
    {
        if (!_isOpen)
        {
            return;
        }

        if (result == GuardShieldResult.Success)
        {
            Hide();
            return;
        }

        RefreshTargets();
        if (_closeButton)
        {
            _closeButton.interactable = true;
        }

        SetMessage(result switch
        {
            GuardShieldResult.AlreadyUsed => "오늘은 이미 사용했습니다. 다음 날 다시 사용할 수 있습니다.",
            GuardShieldResult.AlreadyShielded => "이미 쉴드가 있는 배입니다. 다른 선원을 선택해주세요.",
            GuardShieldResult.InvalidTarget => "대상 배를 보호할 수 없습니다. 다시 선택해주세요.",
            GuardShieldResult.InvalidCaster => "자신의 배에 탑승한 상태에서 사용해주세요.",
            GuardShieldResult.ClockUnavailable => "게임 시간 정보를 준비하는 중입니다.",
            GuardShieldResult.MatchUnavailable => "게임 진행 중에만 사용할 수 있습니다.",
            _ => "요청이 거절되었습니다. 사용권은 차감되지 않았습니다."
        });
    }

    private void SetMessage(string message)
    {
        if (_message)
        {
            _message.text = message;
        }
    }
}
