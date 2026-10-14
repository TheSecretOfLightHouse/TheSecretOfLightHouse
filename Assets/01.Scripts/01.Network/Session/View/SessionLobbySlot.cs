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
            _name.text = player.isLocalPlayer ? $"플레이어 {player.ActorId} (나)" : $"플레이어 {player.ActorId}";
            _state.text = isReady ? "준비 완료" : "대기 중";
            _state.color = isReady ? _readyColor : _idleColor;
            _dot.enabled = true;

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
            _state.text = string.Empty;
            _dot.enabled = false;
        }
    }
}
