using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Systems.Audio;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
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
    // everyone, since the scene is shared, so the screen closes for everyone once the lobby phase arrives instead of
    // staying over the lobby until each player confirms (2026-10-30).
    [DisallowMultipleComponent]
    public sealed class VictoryScreen : MonoBehaviour
    {
        private const float MatchLookupInterval = 1f;

        [Tooltip("Optional. Left empty, the match is looked up once a second, for scenes that spawn the tower at match start.")]
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

        [Header("Citizens")]
        [SerializeField]
        private GameObject _citizensSymbol;
        [SerializeField]
        private GameObject _citizensTitle;
        [SerializeField]
        private GameObject _citizensSubtitle;

        [Header("Citizens Victory Audio")]
        [SerializeField]
        private SoundManager _soundManagerPrefab;
        [SerializeField]
        private AudioClip _citizensVictoryClip;
        [SerializeField, Range(0f, 1f)]
        private float _citizensVictoryVolume = 0.9f;

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

        private bool _hasPhase;
        private TowerPhase _shownPhase;
        private bool _isOpen;
        private float _timer;
        private Vector2 _contentPosition;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private float _nextMatchLookup;

        private void Awake()
        {
            if (_useOsFont && _returnText)
            {
                _returnText.font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            }

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
            // The main island spawns its tower at match start, so there is nothing to wire in the scene (2026-10-30).
            if (!_match && Time.unscaledTime >= _nextMatchLookup)
            {
                _nextMatchLookup = Time.unscaledTime + MatchLookupInterval;
                _match = FindAnyObjectByType<TowerMatchNet>();
            }

            if (_match)
            {
                ObservePhase();
            }

            if (!_isOpen)
            {
                return;
            }

            if (IsBackInLobby())
            {
                // The lobby keeps the pointer free; the cursor saved in the game scene no longer applies.
                _hasSavedCursor = false;
                Close();
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

        // Another player's confirm moved the match back to the lobby. The phase arrives before the scene change, so
        // the screen closes while the game scene is still loaded.
        private static bool IsBackInLobby()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            return manager && manager.IsSessionFlowEnabled && manager.ClientState.Phase == MatchPhase.Lobby;
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

            _timer = isAnimated ? 0f : _delaySeconds + _fadeSeconds;
            _root.gameObject.SetActive(true);
            _root.blocksRaycasts = true;
            _root.interactable = true;
            Animate();
            HoldCursor();

            if (isCitizens)
            {
                PlayCitizensVictorySound();
            }
        }

        private void PlayCitizensVictorySound()
        {
            if (!_citizensVictoryClip)
            {
                return;
            }

            if (!SoundManager.Instance && _soundManagerPrefab)
            {
                Instantiate(_soundManagerPrefab);
            }

            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlayEffect(_citizensVictoryClip, _citizensVictoryVolume);
            }
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
            ReleaseCursor();
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
