using System;
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

        private bool _isConnecting;
        private bool _hasState;

        public event Action JoinRequested;
        public event Action HostRequested;
        public event Action CancelRequested;
        public event Action QuitRequested;

        public string Address
        {
            get => _address.text.Trim();
            set => _address.text = value;
        }

        private void Awake()
        {
            _join.onClick.AddListener(HandleJoin);
            _host.onClick.AddListener(HandleHost);
            _quit.onClick.AddListener(HandleQuit);
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
            _address.interactable = !isConnecting;
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
    }
}
