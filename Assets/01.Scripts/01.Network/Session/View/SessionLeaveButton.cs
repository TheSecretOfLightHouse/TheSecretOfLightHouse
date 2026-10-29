using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // Opens the session screen's leave-match dialog from a button outside it, such as the in-game HUD's Esc hint.
    // While one is shown, the session screen hides its own fallback button so the game shows a single control.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SessionLeaveButton : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(SessionScreen.RequestLeaveMatch);
        }

        private void OnEnable()
        {
            SessionScreen.SetLeaveControlShown(this, true);
        }

        private void OnDisable()
        {
            SessionScreen.SetLeaveControlShown(this, false);
        }

        private void OnDestroy()
        {
            if (_button)
            {
                _button.onClick.RemoveListener(SessionScreen.RequestLeaveMatch);
            }
        }
    }
}
