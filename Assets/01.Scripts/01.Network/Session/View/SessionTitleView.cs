using System;
using Lighthouse.Network.Session.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // First screen (offline scene): open a host or join one by address.
    [DisallowMultipleComponent]
    public sealed class SessionTitleView : MonoBehaviour
    {
        [SerializeField]
        private InputField _address;
        [Tooltip("Optional nickname field. Its character limit is set from LobbyPlayer.MaxNicknameLength.")]
        [SerializeField]
        private InputField _nickname;
        [SerializeField]
        private Text _status;
        [SerializeField]
        private Button _join;
        [SerializeField]
        private Text _joinLabel;
        [SerializeField]
        private Button _host;
        [SerializeField]
        private Button _quit;
        [Tooltip("Enabled only by the main entry prefab; development screens keep their combined host/join layout.")]
        [SerializeField]
        private bool _isEntryScreen;
        [SerializeField]
        private GameObject[] _createOnly;
        [SerializeField]
        private GameObject[] _joinOnly;
        [SerializeField]
        private GameObject _menu;
        [SerializeField]
        private GameObject _entryPanel;
        [SerializeField]
        private Button _createEntry;
        [SerializeField]
        private Button _joinEntry;

        private bool _isConnecting;
        private bool _hasState;
        private bool _isEntryOpen;

        public event Action JoinRequested;
        public event Action HostRequested;
        public event Action CancelRequested;
        public event Action QuitRequested;

        public bool IsEntryScreen => _isEntryScreen;
        public bool IsEntryOpen => _isEntryOpen;

        public string Address
        {
            get => _address.text.Trim();
            set => _address.text = value;
        }

        public string Nickname
        {
            get => _nickname ? _nickname.text.Trim() : string.Empty;
            set
            {
                if (_nickname)
                {
                    _nickname.text = value;
                }
            }
        }

        private void Awake()
        {
            if (_nickname)
            {
                _nickname.characterLimit = LobbyPlayer.MaxNicknameLength;
            }

            _join.onClick.AddListener(HandleJoin);
            _host.onClick.AddListener(HandleHost);
            _quit.onClick.AddListener(HandleQuit);

            if (_isEntryScreen)
            {
                _createEntry.onClick.AddListener(HandleCreateEntry);
                _joinEntry.onClick.AddListener(HandleJoinEntry);
            }
        }

        private void OnEnable()
        {
            if (!_isEntryScreen)
            {
                return;
            }

            ShowMenu();
        }

        public void ShowMenu()
        {
            _isEntryOpen = false;
            _entryPanel.SetActive(false);
            _menu.SetActive(true);
            _status.text = string.Empty;
        }

        public void SetConnecting(bool isConnecting)
        {
            if (_hasState && isConnecting == _isConnecting)
            {
                return;
            }

            _hasState = true;
            _isConnecting = isConnecting;
            _joinLabel.text = isConnecting ? "연결 취소" : "참가하기";
            _host.interactable = !isConnecting;
            _quit.interactable = !isConnecting;
            _address.interactable = !isConnecting;

            if (_nickname)
            {
                _nickname.interactable = !isConnecting;
            }

            _status.text = isConnecting ? "호스트에 연결하는 중…" : string.Empty;
        }

        private void HandleJoin()
        {
            if (_isConnecting)
            {
                CancelRequested?.Invoke();
            }
            else
            {
                JoinRequested?.Invoke();
            }
        }

        private void HandleHost()
        {
            HostRequested?.Invoke();
        }

        private void HandleQuit()
        {
            QuitRequested?.Invoke();
        }

        private void HandleCreateEntry()
        {
            OpenEntry(false);
        }

        private void HandleJoinEntry()
        {
            OpenEntry(true);
        }

        private void OpenEntry(bool isJoin)
        {
            foreach (GameObject item in _createOnly)
            {
                item.SetActive(!isJoin);
            }

            foreach (GameObject item in _joinOnly)
            {
                item.SetActive(isJoin);
            }

            _isEntryOpen = true;
            _menu.SetActive(false);
            _entryPanel.SetActive(true);
        }
    }
}
