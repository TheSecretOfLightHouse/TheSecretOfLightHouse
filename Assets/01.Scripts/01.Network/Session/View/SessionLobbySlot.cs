using Lighthouse.Network.Session.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // One crew row in the lobby list. Without dot sprites the dot is tinted instead, so an art-less layout still reads.
    [DisallowMultipleComponent]
    public sealed class SessionLobbySlot : MonoBehaviour
    {
        [SerializeField]
        private Image _highlight;
        [SerializeField]
        private Image _avatar;
        [SerializeField]
        private Text _name;
        [SerializeField]
        private Text _state;
        [SerializeField]
        private Image _dot;
        [Tooltip("Optional crown shown on the listen host's row.")]
        [SerializeField]
        private Image _hostBadge;
        [SerializeField]
        private Sprite _readyDot;
        [SerializeField]
        private Sprite _idleDot;
        [SerializeField]
        private Color _readyColor = new Color32(62, 112, 58, 255);
        [SerializeField]
        private Color _idleColor = new Color32(122, 94, 72, 255);
        [SerializeField]
        private Color _nameColor = new Color32(74, 50, 34, 255);
        [SerializeField]
        private Color _emptyColor = new Color32(122, 94, 72, 140);
        [Tooltip("Optional card frame swapped between a taken and an empty seat.")]
        [SerializeField]
        private Image _frame;
        [SerializeField]
        private Sprite _frameSprite;
        [SerializeField]
        private Sprite _emptyFrameSprite;
        [SerializeField]
        private string _emptyState = string.Empty;

        private Color _avatarColor;

        private void Awake()
        {
            _avatarColor = _avatar.color;
        }

        public void ShowPlayer(LobbyPlayer player)
        {
            bool isReady = player.IsReady;
            _highlight.enabled = player.isLocalPlayer;
            _avatar.color = _avatarColor;
            _name.color = _nameColor;
            _name.text = player.isLocalPlayer ? $"{player.DisplayName} (나)" : player.DisplayName;
            _state.text = isReady ? "준비 완료" : "대기 중";
            _state.color = isReady ? _readyColor : _idleColor;
            _dot.enabled = true;
            SetFrame(_frameSprite);

            if (_hostBadge)
            {
                _hostBadge.enabled = player.IsHost;
            }

            Sprite dot = isReady ? _readyDot : _idleDot;

            if (dot)
            {
                _dot.sprite = dot;
                _dot.color = Color.white;
            }
            else
            {
                _dot.color = isReady ? _readyColor : _idleColor;
            }
        }

        public void ShowEmpty()
        {
            _highlight.enabled = false;
            Color faded = _avatarColor;
            faded.a *= 0.3f;
            _avatar.color = faded;
            _name.color = _emptyColor;
            _name.text = "빈 자리";
            _state.text = _emptyState;
            _state.color = _emptyColor;
            _dot.enabled = false;
            SetFrame(_emptyFrameSprite);

            if (_hostBadge)
            {
                _hostBadge.enabled = false;
            }
        }

        private void SetFrame(Sprite sprite)
        {
            if (_frame && sprite)
            {
                _frame.sprite = sprite;
            }
        }
    }
}
