using Lighthouse.Player.Abilities.View;
using Lighthouse.Network.Session.View;
using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public sealed class NavigatorRouteSelectionPanel : MonoBehaviour
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

    private readonly PlayerController[] _targets =
        new PlayerController[4];

    private readonly List<PlayerController> _players =
        new List<PlayerController>(4);

    private readonly UnityAction[] _clickHandlers =
        new UnityAction[4];

    private AbilityTargetPresentation _targetPresentation;
    private UnityEngine.UI.Image[] _targetIcons;
    private Font _runtimeFont;
    private NetworkIdentity _boundPlayer;
    private NavigatorRouteAbility _ability;
    private NavigatorRouteRequest _request;
    private PlayerInput _playerInput;

    private bool _isOpen;
    private int _openedFrame;
    private bool _wasInputEnabled;
    private CursorLockMode _savedCursorLock;
    private bool _savedCursorVisible;

    private void Awake()
    {
        _targetPresentation = Resources.Load<AbilityTargetPresentation>("AbilityTargetPresentation");
        _targetIcons = AbilityTargetPresentation.CreateIcons(_playerButtons, _playerNames);
        if (_panelRoot)
        {
            // OS fonts must be created separately in each process.
            _runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                        "Malgun Gothic",
                        "맑은 고딕",
                        "Apple SD Gothic Neo",
                        "Noto Sans CJK KR",
                        "Arial"
                },
                32);

            foreach (UnityEngine.UI.Text label in
                _panelRoot.GetComponentsInChildren<
                    UnityEngine.UI.Text>(true))
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
            _playerButtons[i].onClick.AddListener(
                _clickHandlers[i]);
        }

        Hide();
    }

    private void Update()
    {
        // Ignore the Q press that opened this panel earlier in the same frame.
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (_isOpen && Time.frameCount > _openedFrame && keyboard != null
            && (keyboard.escapeKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame))
        {
            Hide();
            return;
        }

        NetworkIdentity localPlayer = NetworkClient.localPlayer;

        if (_isOpen
            && (!localPlayer || !_ability
                || !_ability.isActiveAndEnabled
                || _ability.IsFinished
                || !_request || !_request.isActiveAndEnabled))
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
            ? _boundPlayer.GetComponent<PlayerInput>()
            : null;

        if (_boundPlayer
            && _boundPlayer.TryGetComponent(
                out NavigatorRouteAbility ability))
        {
            BindAbility(ability);
        }
    }

    private void OnDisable()
    {
        Hide();
        BindAbility(null);
        _boundPlayer = null;
        _playerInput = null;
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
                _playerButtons[i].onClick.RemoveListener(
                    _clickHandlers[i]);
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

        if (SessionScreen.IsGameplayOverlayOpen
            || !_request || !_request.isActiveAndEnabled
            || _request.IsAwaitingResult)
        {
            if (_ability)
            {
                _ability.CompleteSelection();
            }

            return;
        }

        _savedCursorLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        _wasInputEnabled = _playerInput && _playerInput.enabled;
        _isOpen = true;
        _openedFrame = Time.frameCount;

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

        SetMessage("항로를 안내할 난파 동료를 선택하세요.");
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

        if (_ability)
        {
            _ability.CompleteSelection();
        }
    }

    private void BindAbility(NavigatorRouteAbility ability)
    {
        if (_request)
        {
            _request.Responded -= HandleResponse;
        }

        if (_ability)
        {
            _ability.SelectionRequested -= Show;
        }

        _ability = ability;
        _request = ability
            ? ability.GetComponent<NavigatorRouteRequest>()
            : null;

        if (_ability)
        {
            _ability.SelectionRequested += Show;
        }

        if (_request)
        {
            _request.Responded += HandleResponse;
        }
    }

    private void RefreshTargets()
    {
        _players.Clear();

        foreach (NetworkIdentity identity in
            NetworkClient.spawned.Values)
        {
            if (identity
                && identity.TryGetComponent(
                    out PlayerController player)
                && player.isActiveAndEnabled)
            {
                _players.Add(player);
            }
        }

        _players.Sort((a, b) => a.ActorID.CompareTo(b.ActorID));

        for (int i = 0; i < _targets.Length; i++)
        {
            PlayerController player =
                i < _players.Count ? _players[i] : null;

            _targets[i] = player;

            if (_targetPresentation && _targetIcons != null && i < _targetIcons.Length)
            {
                _targetPresentation.BindIcon(_targetIcons[i], player);
            }

            if (_playerButtons != null
                && i < _playerButtons.Length
                && _playerButtons[i])
            {
                _playerButtons[i].gameObject.SetActive(
                    player != null);

                _playerButtons[i].interactable =
                    CanSelectTarget(player)
                    && _request
                    && !_request.IsAwaitingResult;
            }

            if (_playerNames == null
                || i >= _playerNames.Length
                || !_playerNames[i])
            {
                continue;
            }

            if (!player)
            {
                _playerNames[i].text = string.Empty;
                continue;
            }

            string name = AbilityTargetPresentation.GetJobName(player);

            _playerNames[i].text = CanSelectTarget(player)
                ? $"{name} · 구조 대기"
                : $"{name} · 선택 불가";
        }
    }

    private bool CanSelectTarget(PlayerController player)
    {
        return player
            && player.isActiveAndEnabled
            && player.netIdentity != _boundPlayer
            && player.IsDowned
            && player.TryGetComponent(
                out PlayerWreckRecovery recovery)
            && recovery.isActiveAndEnabled
            && recovery.RescueTarget
            && recovery.RescueTarget.isActiveAndEnabled;
    }

    private void SelectTarget(int index)
    {
        if (!_isOpen || !_request
            || _request.IsAwaitingResult
            || index < 0 || index >= _targets.Length)
        {
            return;
        }

        PlayerController target = _targets[index];

        if (!CanSelectTarget(target))
        {
            RefreshTargets();
            SetMessage("해당 동료는 현재 구조 대기 상태가 아닙니다.");
            return;
        }

        SetMessage("구조 항로를 계산하는 중입니다...");
        SetButtonsEnabled(false);

        if (!_request.TryRequest(target))
        {
            RefreshTargets();

            if (_closeButton)
            {
                _closeButton.interactable = true;
            }

            SetMessage("요청을 보내지 못했습니다. 접속 상태를 확인해주세요.");
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (_playerButtons != null)
        {
            foreach (UnityEngine.UI.Button button in _playerButtons)
            {
                if (button)
                {
                    button.interactable = enabled;
                }
            }
        }

        if (_closeButton)
        {
            _closeButton.interactable = enabled;
        }
    }

    private void HandleResponse(NavigatorRouteResult result)
    {
        if (!_isOpen)
        {
            return;
        }

        if (result == NavigatorRouteResult.Success)
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
            NavigatorRouteResult.AlreadyUsed =>
                "오늘은 이미 사용했습니다. 다음 날 다시 사용할 수 있습니다.",

            NavigatorRouteResult.InvalidCaster =>
                "현재 상태에서는 항해사 스킬을 사용할 수 없습니다.",

            NavigatorRouteResult.InvalidTarget =>
                "구조 대기 중인 다른 동료를 선택해주세요.",

            NavigatorRouteResult.BoatUnavailable =>
                "항로 안내를 시작할 수 있는 내 배가 없습니다.",

            NavigatorRouteResult.RouteUnavailable =>
                "항로를 찾지 못했습니다. 사용권은 차감되지 않았습니다.",

            NavigatorRouteResult.MatchUnavailable =>
                "게임 진행 중에만 사용할 수 있습니다.",

            NavigatorRouteResult.ClockUnavailable =>
                "게임 시간 정보를 준비하는 중입니다.",

            _ =>
                "요청이 거절되었습니다. 사용권은 차감되지 않았습니다."
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