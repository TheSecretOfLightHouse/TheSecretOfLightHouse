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
        private bool _isCurrent;
        private float _popEndsAt;
        private Vector2 _restPosition;

        public uint ActorId { get; private set; }

        private void Awake()
        {
            _badgeColor = _badge.color;
            _button.onClick.AddListener(HandleClicked);
        }

        private void Update()
        {
            if (_popEndsAt <= 0f)
            {
                return;
            }

            float left = _popEndsAt - Time.unscaledTime;

            if (left <= 0f)
            {
                StopPop();
                return;
            }

            float scale = 1f + (PopScale - 1f) * (left / PopSeconds);
            RectTransform rect = (RectTransform)transform;
            // The sketch pins cards at their top-left corner; shift so the card grows from its middle.
            rect.anchoredPosition = _restPosition + (rect.pivot - new Vector2(0.5f, 0.5f)) * rect.rect.size * (scale - 1f);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnDisable()
        {
            StopPop();
        }

        public void Bind(int number, Action<VoteCandidateCard> clicked)
        {
            _badge.text = number.ToString();
            _clicked = clicked;
        }

        public void Show(uint actorId, string displayName, Sprite portrait, bool isCurrent, bool canPick)
        {
            // Pops only when this same player becomes the pick, not when the card first fills or changes player.
            if (isCurrent && !_isCurrent && actorId == ActorId)
            {
                if (_popEndsAt <= 0f)
                {
                    _restPosition = ((RectTransform)transform).anchoredPosition;
                }

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

        private void StopPop()
        {
            if (_popEndsAt <= 0f)
            {
                return;
            }

            _popEndsAt = 0f;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = _restPosition;
            rect.localScale = Vector3.one;
        }
    }
}
