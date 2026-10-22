using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.View
{
    // One player card on the secret vote screen. The screen decides what a click means; the card only draws.
    [DisallowMultipleComponent]
    public sealed class VoteCandidateCard : MonoBehaviour
    {
        [SerializeField]
        private Image _frame;
        [SerializeField]
        private Text _name;
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

        public uint ActorId { get; private set; }

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
        }

        public void Bind(int number, Action<VoteCandidateCard> clicked)
        {
            _badge.text = number.ToString();
            _clicked = clicked;
        }

        public void Show(uint actorId, string displayName, bool isCurrent, bool canPick)
        {
            ActorId = actorId;
            _name.text = displayName;
            _frame.sprite = isCurrent ? _selectedFrame : _normalFrame;
            _buttonImage.sprite = isCurrent ? _currentButton : _voteButton;
            _check.SetActive(isCurrent);
            _button.interactable = canPick;
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
    }
}
