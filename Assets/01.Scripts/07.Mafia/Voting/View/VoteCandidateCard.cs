using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.View
{
    // One player card on the secret vote screen. The screen decides what a click means; the card only draws.
    [DisallowMultipleComponent]
    public sealed class VoteCandidateCard : MonoBehaviour
    {
        private const float PopSeconds = 0.2f;
        private const float PopScale = 1.06f;
        // Shared with the screen so a refused vote shakes the notice and the card the same way.
        internal const float ShakeSeconds = 0.2f;
        private const float ShakeDistance = 8f;
        private const float ShakeCycles = 3f;
        private static readonly Color LockedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

        [SerializeField]
        private Color _currentBadgeColor = new Color(0.965f, 0.776f, 0.29f);
        [SerializeField]
        private Image _frame;
        [SerializeField]
        private Text _name;
        [Tooltip("The job picture. Hidden when the player's job is not known.")]
        [SerializeField]
        private Image _portrait;
        [SerializeField]
        private Button _button;
        [SerializeField]
        private Image _buttonImage;
        [SerializeField]
        private Text _badge;
        [SerializeField]
        private GameObject _check;
        [SerializeField]
        private Sprite _normalFrame;
        [SerializeField]
        private Sprite _selectedFrame;
        [SerializeField]
        private Sprite _voteButton;
        [SerializeField]
        private Sprite _currentButton;

        private Action<VoteCandidateCard> _clicked;
        private Color _badgeColor;
        private Color _frameColor;
        private Color _nameColor;
        private Color _portraitColor;
        private Color _buttonColor;
        private bool _isCurrent;
        private bool _isLocked;
        private float _popEndsAt;
        private float _shakeEndsAt;
        private Vector2 _restPosition;

        public uint ActorId { get; private set; }

        private bool IsMoving => _popEndsAt > 0f || _shakeEndsAt > 0f;

        private void Awake()
        {
            _badgeColor = _badge.color;
            _frameColor = _frame.color;
            _nameColor = _name.color;
            _portraitColor = _portrait ? _portrait.color : Color.white;
            _buttonColor = _buttonImage.color;
            _button.onClick.AddListener(HandleClicked);
        }

        private void Update()
        {
            if (!IsMoving)
            {
                return;
            }

            float now = Time.unscaledTime;
            float scale = 1f;
            float shake = 0f;

            if (_popEndsAt > 0f)
            {
                float left = _popEndsAt - now;

                if (left <= 0f)
                {
                    _popEndsAt = 0f;
                }
                else
                {
                    scale = 1f + (PopScale - 1f) * (left / PopSeconds);
                }
            }

            if (_shakeEndsAt > 0f)
            {
                float left = _shakeEndsAt - now;

                if (left <= 0f)
                {
                    _shakeEndsAt = 0f;
                }
                else
                {
                    shake = ShakeOffset(left);
                }
            }

            RectTransform rect = (RectTransform)transform;
            // The sketch pins cards at their top-left corner; shift so the card grows from its middle.
            rect.anchoredPosition = _restPosition + (rect.pivot - new Vector2(0.5f, 0.5f)) * rect.rect.size * (scale - 1f)
                + new Vector2(shake, 0f);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnDisable()
        {
            StopMotion();
        }

        // A side-to-side wobble that dies out over ShakeSeconds; left is the time still to go.
        internal static float ShakeOffset(float left)
        {
            float progress = 1f - left / ShakeSeconds;
            return Mathf.Sin(progress * ShakeCycles * 2f * Mathf.PI) * ShakeDistance * (left / ShakeSeconds);
        }

        public void Bind(int number, Action<VoteCandidateCard> clicked)
        {
            _badge.text = number.ToString();
            _clicked = clicked;
        }

        public void Show(uint actorId, string displayName, Sprite portrait, bool isCurrent, bool canPick, bool isLocked)
        {
            // Pops only when this same player becomes the pick, not when the card first fills or changes player.
            if (isCurrent && !_isCurrent && actorId == ActorId)
            {
                SaveRestPosition();
                _popEndsAt = Time.unscaledTime + PopSeconds;
            }

            ActorId = actorId;
            _isCurrent = isCurrent;
            _name.text = displayName;
            _frame.sprite = isCurrent ? _selectedFrame : _normalFrame;
            _buttonImage.sprite = isCurrent ? _currentButton : _voteButton;
            _check.SetActive(isCurrent);
            _badge.color = isCurrent ? _currentBadgeColor : _badgeColor;
            _button.interactable = canPick;

            if (_portrait)
            {
                ShowPortrait(portrait);
            }

            if (_isLocked != isLocked)
            {
                _isLocked = isLocked;
                ApplyTint(isLocked ? LockedTint : Color.white);
            }
        }

        public void Shake()
        {
            SaveRestPosition();
            _shakeEndsAt = Time.unscaledTime + ShakeSeconds;
        }

        public void Press()
        {
            if (_button.interactable)
            {
                HandleClicked();
            }
        }

        private void HandleClicked()
        {
            _clicked?.Invoke(this);
        }

        private void ShowPortrait(Sprite portrait)
        {
            bool hasPortrait = portrait;

            if (_portrait.gameObject.activeSelf != hasPortrait)
            {
                _portrait.gameObject.SetActive(hasPortrait);
            }

            if (!hasPortrait)
            {
                return;
            }

            if (_portrait.sprite != portrait)
            {
                _portrait.sprite = portrait;
            }
        }

        private void SaveRestPosition()
        {
            if (!IsMoving)
            {
                _restPosition = ((RectTransform)transform).anchoredPosition;
            }
        }

        // Greys the card once the ritual locks the votes; the gold badge keeps its own colour on top.
        private void ApplyTint(Color tint)
        {
            _frame.color = _frameColor * tint;
            _name.color = _nameColor * tint;
            _buttonImage.color = _buttonColor * tint;

            if (_portrait)
            {
                _portrait.color = _portraitColor * tint;
            }
        }

        private void StopMotion()
        {
            if (!IsMoving)
            {
                return;
            }

            _popEndsAt = 0f;
            _shakeEndsAt = 0f;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = _restPosition;
            rect.localScale = Vector3.one;
        }
    }
}
