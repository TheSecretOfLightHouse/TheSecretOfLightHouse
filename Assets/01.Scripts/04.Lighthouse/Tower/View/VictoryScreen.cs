using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.View;
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
    // With the session flow the confirm asks the server to go back to the lobby (2026-10-29). The first confirm moves
    // everyone, since the scene is shared, so the open screen is kept across the scene change and stays over the lobby
    // until this player confirms too. That copy goes away on its own if the connection closes first.
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
        [Tooltip("Optional. Says the confirm goes back to the lobby; hidden without the session flow.")]
        [SerializeField]
        private Text _returnText;
        [Tooltip("Sort order once carried over the lobby, above the session screen (500).")]
        [SerializeField]
        private int _lobbySortingOrder = 600;

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

        private Canvas _canvas;
        // Kept across scene changes for the session flow; destroyed instead of hidden once closed.
        private bool _isCarried;
        private bool _isOverLobby;
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

            _canvas = GetComponent<Canvas>();

            if (_content)
            {
                _contentPosition = _content.anchoredPosition;
            }

            if (_confirmButton)
            {
                _confirmButton.onClick.AddListener(Confirm);
            }

            Hide();
        }

        private void LateUpdate()
        {
            if (_match)
            {
                ObservePhase();
            }

            if (_isCarried && !KeepCarried())
            {
                return;
            }

            if (!_isOpen)
            {
                return;
            }

            Animate();
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Confirm();
            }
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            SessionScreen.SetGameplayOverlayOpen(this, false);
        }

        // False once the carried copy has removed itself: the connection closed (the first screen's end notice must
        // not be covered), a new match is loading, or the game scene is gone without a lobby to sit over.
        // The lobby phase arrives before the scene change, so the screen is raised over the session screen (500)
        // from then on, while the game scene is still there.
        private bool KeepCarried()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            bool isConnected = NetworkServer.active || NetworkClient.isConnected;
            MatchPhase phase = manager ? manager.ClientState.Phase : MatchPhase.Lobby;

            if (!_isOpen || !manager || !isConnected || phase == MatchPhase.Loading || (!_match && phase != MatchPhase.Lobby))
            {
                // Every case here leaves the game, where the session screen owns the cursor; restoring the game's lock
                // would trap it on the first screen or the lobby.
                _hasSavedCursor = false;
                Destroy(gameObject);
                return false;
            }

            if (phase == MatchPhase.Lobby && !_isOverLobby)
            {
                _isOverLobby = true;

                if (_canvas)
                {
                    _canvas.sortingOrder = _lobbySortingOrder;
                }

                // Keeps Escape from opening the lobby's leave dialog underneath.
                SessionScreen.SetGameplayOverlayOpen(this, true);
            }

            return true;
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
            bool isSessionFlow = IsSessionFlow();

            if (_returnText)
            {
                _returnText.enabled = isSessionFlow;
                _returnText.text = isSessionFlow ? "확인을 누르면 선원 대기실로 돌아갑니다" : string.Empty;
            }

            // DontDestroyOnLoad needs a root object; the setup places the screen at the scene root.
            if (isSessionFlow && !_isCarried && !transform.parent)
            {
                _isCarried = true;
                DontDestroyOnLoad(gameObject);
            }

            _timer = isAnimated ? 0f : _delaySeconds + _fadeSeconds;
            _root.gameObject.SetActive(true);
            _root.blocksRaycasts = true;
            _root.interactable = true;
            Animate();
            HoldCursor();
        }

        private void Confirm()
        {
            if (!_isOpen)
            {
                return;
            }

            if (IsSessionFlow())
            {
                LighthouseNetworkManager.Instance.RequestReturnToLobby();
            }

            Close();
        }

        private void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            Hide();

            if (_isOverLobby)
            {
                // The lobby keeps the pointer free; the cursor saved in the game scene no longer applies.
                _hasSavedCursor = false;
            }
            else
            {
                ReleaseCursor();
            }

            if (_isCarried)
            {
                // The next match's scene brings its own screen.
                Destroy(gameObject);
            }
        }

        private static bool IsSessionFlow()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            return manager && manager.IsSessionFlowEnabled;
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
