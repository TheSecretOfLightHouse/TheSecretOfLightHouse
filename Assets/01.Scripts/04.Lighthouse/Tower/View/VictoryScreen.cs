using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // Result screen for the end of the match (victory sketch 2026-10-28): the winning side's symbol, title and line
    // over a dark backdrop, with a confirm button that closes it. It replaces the end line that used to sit in the
    // match effects' centre title. Phase and outcome are SyncVars without hooks, so they are read every frame. The
    // first phase seen is taken as it is, so a late joiner on an ended match gets the screen without the entrance.
    // With the session flow a line counts down to the return to the lobby (TowerMatchFlow, 2026-10-29); closing the
    // screen does not change when that happens.
    [DisallowMultipleComponent]
    public sealed class VictoryScreen : MonoBehaviour
    {
        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private CanvasGroup _root;
        [SerializeField]
        private RectTransform _content;
        [SerializeField]
        private Button _confirmButton;
        [Tooltip("Optional. Counts down to the return to the lobby; hidden when there is none.")]
        [SerializeField]
        private Text _returnText;

        [Header("Citizens")]
        [SerializeField]
        private GameObject _citizensSymbol;
        [SerializeField]
        private GameObject _citizensTitle;
        [SerializeField]
        private GameObject _citizensSubtitle;

        [Header("Mafia")]
        [SerializeField]
        private GameObject _mafiaSymbol;
        [SerializeField]
        private GameObject _mafiaTitle;
        [SerializeField]
        private GameObject _mafiaSubtitle;

        [Header("Motion")]
        [Tooltip("Waits for the match effects' end fade to darken the screen first.")]
        [SerializeField, Min(0f)]
        private float _delaySeconds = 1.2f;
        [SerializeField, Min(0.01f)]
        private float _fadeSeconds = 0.8f;
        [SerializeField, Min(0f)]
        private float _riseDistance = 40f;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private TowerMatchFlow _flow;
        private int _shownReturnSeconds = -1;
        private bool _hasPhase;
        private TowerPhase _shownPhase;
        private bool _isOpen;
        private float _timer;
        private Vector2 _contentPosition;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        private void Awake()
        {
            if (_useOsFont && _returnText)
            {
                _returnText.font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            }

            _flow = _match ? _match.GetComponent<TowerMatchFlow>() : null;

            if (_content)
            {
                _contentPosition = _content.anchoredPosition;
            }

            if (_confirmButton)
            {
                _confirmButton.onClick.AddListener(Close);
            }

            Hide();
        }

        private void LateUpdate()
        {
            if (!_match)
            {
                return;
            }

            ObservePhase();

            if (!_isOpen)
            {
                return;
            }

            Animate();
            RefreshReturnText();
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Close();
            }
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void ObservePhase()
        {
            TowerPhase phase = _match.Phase;

            if (_hasPhase && phase == _shownPhase)
            {
                return;
            }

            bool isFirst = !_hasPhase;
            _hasPhase = true;
            _shownPhase = phase;

            if (phase == TowerPhase.Ended)
            {
                Open(!isFirst);
            }
            else if (_isOpen)
            {
                // A new round started while the screen was still up.
                Close();
            }
        }

        private void Open(bool isAnimated)
        {
            TowerOutcome outcome = _match.Outcome;

            if (outcome == TowerOutcome.None)
            {
                return;
            }

            bool isCitizens = outcome == TowerOutcome.CitizensWin;
            SetActive(_citizensSymbol, isCitizens);
            SetActive(_citizensTitle, isCitizens);
            SetActive(_citizensSubtitle, isCitizens);
            SetActive(_mafiaSymbol, !isCitizens);
            SetActive(_mafiaTitle, !isCitizens);
            SetActive(_mafiaSubtitle, !isCitizens);

            _isOpen = true;
            // Forces the countdown line to refresh, including back to hidden.
            _shownReturnSeconds = -2;
            _timer = isAnimated ? 0f : _delaySeconds + _fadeSeconds;
            _root.gameObject.SetActive(true);
            _root.blocksRaycasts = true;
            _root.interactable = true;
            Animate();
            HoldCursor();
        }

        private void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            Hide();
            ReleaseCursor();
        }

        private void Hide()
        {
            _isOpen = false;

            if (_root)
            {
                _root.alpha = 0f;
                _root.blocksRaycasts = false;
                _root.interactable = false;
                _root.gameObject.SetActive(false);
            }
        }

        // Fades in after the delay and rises into place, easing out.
        private void Animate()
        {
            _timer += Time.unscaledDeltaTime;
            float shown = Mathf.Clamp01((_timer - _delaySeconds) / _fadeSeconds);
            float eased = 1f - (1f - shown) * (1f - shown);
            _root.alpha = eased;

            if (_content)
            {
                _content.anchoredPosition = _contentPosition - new Vector2(0f, _riseDistance * (1f - eased));
            }
        }

        // The return time is a SyncVar that can land a frame after the phase, so it is read every frame.
        private void RefreshReturnText()
        {
            if (!_returnText)
            {
                return;
            }

            double returnAt = _flow ? _flow.LobbyReturnAt : 0d;
            int seconds = returnAt > 0d ? Mathf.Max(0, Mathf.CeilToInt((float)(returnAt - NetworkTime.time))) : -1;

            if (seconds == _shownReturnSeconds)
            {
                return;
            }

            _shownReturnSeconds = seconds;
            _returnText.enabled = seconds >= 0;
            _returnText.text = seconds >= 0 ? $"{seconds}초 후 선원 대기실로 돌아갑니다" : string.Empty;
        }

        // The confirm button needs the pointer; the player's own lock comes back on close.
        private void HoldCursor()
        {
            if (!_hasSavedCursor)
            {
                _hasSavedCursor = true;
                _savedLockMode = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursor()
        {
            if (!_hasSavedCursor)
            {
                return;
            }

            _hasSavedCursor = false;
            Cursor.lockState = _savedLockMode;
            Cursor.visible = _savedCursorVisible;
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target)
            {
                target.SetActive(isActive);
            }
        }
    }
}
