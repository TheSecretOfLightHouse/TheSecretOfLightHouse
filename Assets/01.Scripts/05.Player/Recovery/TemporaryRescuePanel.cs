using System.Collections;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Recovery.View
{
    [DisallowMultipleComponent]
    public sealed class TemporaryRescuePanel : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panel;
        [SerializeField]
        private UnityEngine.UI.Button _rescueButton;
        [SerializeField]
        private UnityEngine.UI.Button _dismissButton;
        [SerializeField]
        private UnityEngine.UI.Text _promptText;

        private NetworkIdentity _boundPlayer;
        private PlayerController _controller;
        private PlayerRescuePrompt _prompt;
        private InteractionRelay _relay;
        private PlayerWreckRecovery _requestedTarget;
        private Coroutine _bindRoutine;
        private bool _isVisible;
        private bool _isAwaitingRescue;
        private Font _runtimeFont;

        private void Awake()
        {
            if (!_panel)
            {
                return;
            }

            // OS dynamic-font materials belong to the current process, not a saved scene asset.
            _runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" },
                32);

            UnityEngine.UI.Text[] labels =
                _panel.GetComponentsInChildren<UnityEngine.UI.Text>(true);

            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = _runtimeFont;
                labels[i].fontStyle = FontStyle.Bold;
            }
        }

        private void OnEnable()
        {
            if (!_panel || !_rescueButton || !_dismissButton || !_promptText)
            {
                Debug.LogError($"[{nameof(TemporaryRescuePanel)}] Missing UI references.", this);
                return;
            }

            _panel.SetActive(false);
            _rescueButton.onClick.AddListener(HandleRescueClicked);
            _dismissButton.onClick.AddListener(HandleDismissClicked);
            _bindRoutine = StartCoroutine(BindLocalPlayer());
        }

        private void Update()
        {
            if (!_prompt || !_prompt.isActiveAndEnabled || !_controller
                || !_boundPlayer || _boundPlayer != NetworkClient.localPlayer
                || _controller.IsDowned)
            {
                SetVisible(false);
                return;
            }

            SetVisible(_prompt.CurrentTarget);
            bool canClick = _isVisible && _relay && !_relay.IsAwaitingResult
                && !_isAwaitingRescue;
            _rescueButton.interactable = canClick;
            _dismissButton.interactable = _isVisible && !_isAwaitingRescue;
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            if (_rescueButton)
            {
                _rescueButton.onClick.RemoveListener(HandleRescueClicked);
            }

            if (_dismissButton)
            {
                _dismissButton.onClick.RemoveListener(HandleDismissClicked);
            }

            UnbindPlayer();
        }

        private void OnDestroy()
        {
            if (_runtimeFont)
            {
                Destroy(_runtimeFont);
            }
        }

        private IEnumerator BindLocalPlayer()
        {
            var delay = new WaitForSecondsRealtime(0.1f);

            while (true)
            {
                NetworkIdentity player = NetworkClient.localPlayer;

                if (_boundPlayer != player)
                {
                    UnbindPlayer();

                    if (player && player.TryGetComponent(out PlayerController controller)
                        && player.TryGetComponent(out PlayerRescuePrompt prompt)
                        && player.TryGetComponent(out InteractionRelay relay))
                    {
                        _boundPlayer = player;
                        _controller = controller;
                        _prompt = prompt;
                        _relay = relay;
                        _prompt.TargetChanged += HandleTargetChanged;
                        _relay.ResultReceived += HandleResultReceived;
                        HandleTargetChanged(_prompt.CurrentTarget);
                    }
                }

                yield return delay;
            }
        }

        private void UnbindPlayer()
        {
            SetVisible(false);

            if (_prompt)
            {
                _prompt.TargetChanged -= HandleTargetChanged;
            }

            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }

            _boundPlayer = null;
            _controller = null;
            _prompt = null;
            _relay = null;
            _requestedTarget = null;
            _isAwaitingRescue = false;
        }

        private void HandleTargetChanged(PlayerWreckRecovery target)
        {
            _promptText.text = "난파된 동료를 구조할까요?";
            SetVisible(target);
        }

        private void HandleRescueClicked()
        {
            if (!_prompt || !_relay || _relay.IsAwaitingResult || _isAwaitingRescue)
            {
                return;
            }

            _requestedTarget = _prompt.CurrentTarget;
            _isAwaitingRescue = true;

            // A host may receive its reply before TryRescueTarget returns.
            if (!_prompt.TryRescueTarget())
            {
                _isAwaitingRescue = false;
                _requestedTarget = null;
                _promptText.text = "구조 요청을 보내지 못했습니다.";
            }
        }

        private void HandleDismissClicked()
        {
            if (_prompt && !_isAwaitingRescue)
            {
                _prompt.DismissTarget();
            }
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (!_isAwaitingRescue)
            {
                return;
            }

            _isAwaitingRescue = false;

            if (result.IsSuccess)
            {
                if (_prompt && ReferenceEquals(_prompt.CurrentTarget, _requestedTarget))
                {
                    _prompt.DismissTarget();
                }
            }
            else
            {
                _promptText.text = "구조할 수 없습니다. 거리와 상태를 확인하세요.";
            }

            _requestedTarget = null;
        }

        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
            {
                return;
            }

            _isVisible = isVisible;

            if (_panel)
            {
                _panel.SetActive(isVisible);
            }

            if (_controller)
            {
                _controller.SetRescuePromptOpen(isVisible);
            }
        }
    }
}
