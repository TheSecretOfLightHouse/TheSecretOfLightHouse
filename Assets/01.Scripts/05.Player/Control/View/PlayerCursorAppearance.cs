using UnityEngine;

namespace Lighthouse.Player.Control.View
{
    internal sealed class PlayerCursorAppearance
    {
        private const string GameplayResource = "PlayerCursor/cream_arrow_gameplay";
        private const string MenuResource = "PlayerCursor/cream_arrow_menu";
        private static readonly Vector2 Hotspot = new Vector2(2f, 2f);

        private Texture2D _gameplayTexture;
        private Texture2D _menuTexture;
        private bool _hasLoaded;
        private bool _hasApplied;
        private bool _isMenu;

        public void Refresh()
        {
            if (!_hasLoaded)
            {
                _gameplayTexture = Resources.Load<Texture2D>(GameplayResource);
                _menuTexture = Resources.Load<Texture2D>(MenuResource);
                _hasLoaded = true;
            }

            // Existing panels release confinement while borrowing the pointer.
            bool isMenu = Cursor.lockState == CursorLockMode.None;
            if (_hasApplied && _isMenu == isMenu)
            {
                return;
            }

            Texture2D texture = isMenu ? _menuTexture : _gameplayTexture;
            Cursor.SetCursor(texture, texture != null ? Hotspot : Vector2.zero, CursorMode.Auto);
            _isMenu = isMenu;
            _hasApplied = true;
        }

        public void Release()
        {
            if (!_hasApplied)
            {
                return;
            }

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _hasApplied = false;
        }
    }
}
