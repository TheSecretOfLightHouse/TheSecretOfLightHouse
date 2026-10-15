using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // Modal notice with one or two choices: end reasons, failed joins, and leave confirmations.
    // Put the buttons under a layout group so hiding the secondary one recentres the primary.
    [DisallowMultipleComponent]
    public sealed class SessionDialogView : MonoBehaviour
    {
        [SerializeField]
        private Text _title;
        [SerializeField]
        private Text _message;
        [SerializeField]
        private Button _primary;
        [SerializeField]
        private Text _primaryLabel;
        [SerializeField]
        private Button _secondary;
        [SerializeField]
        private Text _secondaryLabel;

        private Action _onPrimary;
        private Action _onSecondary;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            _primary.onClick.AddListener(HandlePrimary);
            _secondary.onClick.AddListener(HandleSecondary);
        }

        public void Show(string title, string message, string primaryLabel, Action onPrimary, string secondaryLabel = null, Action onSecondary = null)
        {
            _title.text = title;
            _message.text = message;
            _primaryLabel.text = primaryLabel;
            _onPrimary = onPrimary;
            _onSecondary = onSecondary;

            bool hasSecondary = !string.IsNullOrEmpty(secondaryLabel);
            _secondary.gameObject.SetActive(hasSecondary);

            if (hasSecondary)
            {
                _secondaryLabel.text = secondaryLabel;
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _onPrimary = null;
            _onSecondary = null;
        }

        private void HandlePrimary()
        {
            Action action = _onPrimary;
            Hide();
            action?.Invoke();
        }

        private void HandleSecondary()
        {
            Action action = _onSecondary;
            Hide();
            action?.Invoke();
        }
    }
}
