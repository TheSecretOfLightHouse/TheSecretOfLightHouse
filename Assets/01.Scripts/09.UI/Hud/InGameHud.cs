using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // Always-on HUD from the 2026-10-29 in-game HUD mockup: carried item card and role/boat panel bottom left, key
    // hints top right. Only the boat's durability is live; it follows the local player's own boat, aboard or not. The
    // role and card are the mockup's fixed pictures until their data exists (no role data yet). The mockup's "E" hint
    // is left out: each station shows its own prompt when in range. Shown while a local player exists.
    [DisallowMultipleComponent]
    public sealed class InGameHud : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [Header("Boat")]
        [SerializeField]
        private Image _boatFill;
        [SerializeField]
        private Text _boatText;
        [Tooltip("The current durability in \"current / max\", as in the mockup. The max keeps the text colour.")]
        [SerializeField]
        private Color _boatNumberColor = new Color(0.98f, 0.73f, 0.15f);
        [Tooltip("Bar fill change per second while easing toward the synced value.")]
        [SerializeField, Min(0.01f)]
        private float _fillSpeed = 1.2f;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private string _boatNumberHex;
        private bool _isShown;
        private NetworkIdentity _boundPlayer;
        private PlayerController _player;
        private BoatController _boundBoat;
        private BoatDurability _durability;
        private int _shownDurability = -1;
        private int _shownMaxDurability = -1;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _boatNumberHex = ColorUtility.ToHtmlStringRGB(_boatNumberColor);
            _root.SetActive(false);
        }

        private void LateUpdate()
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            bool hasPlayer = localPlayer;

            if (hasPlayer != _isShown)
            {
                _isShown = hasPlayer;
                _root.SetActive(hasPlayer);
            }

            if (!_isShown)
            {
                return;
            }

            if (localPlayer != _boundPlayer)
            {
                _boundPlayer = localPlayer;
                _player = localPlayer.GetComponent<PlayerController>();
            }

            // A wrecked boat is replaced by a recovery boat, so the owned boat is checked every frame.
            BoatController boat = _player ? _player.OwnedBoat : null;

            if (boat != _boundBoat)
            {
                _boundBoat = boat;
                _durability = boat ? boat.GetComponent<BoatDurability>() : null;
                _shownDurability = -1;
                _shownMaxDurability = -1;

                if (_boatFill)
                {
                    _boatFill.fillAmount = DurabilityRatio();
                }
            }

            if (_boatFill)
            {
                _boatFill.fillAmount = Mathf.MoveTowards(_boatFill.fillAmount, DurabilityRatio(), _fillSpeed * Time.unscaledDeltaTime);
            }

            RefreshBoatText();
        }

        private float DurabilityRatio()
        {
            return _durability && _durability.MaxDurability > 0f
                ? Mathf.Clamp01(_durability.CurrentDurability / _durability.MaxDurability)
                : 0f;
        }

        private void RefreshBoatText()
        {
            int durability = _durability ? Mathf.CeilToInt(_durability.CurrentDurability) : 0;
            int maxDurability = _durability ? Mathf.CeilToInt(_durability.MaxDurability) : 0;

            if (!_boatText || (durability == _shownDurability && maxDurability == _shownMaxDurability))
            {
                return;
            }

            _shownDurability = durability;
            _shownMaxDurability = maxDurability;
            _boatText.text = _durability
                ? $"<color=#{_boatNumberHex}>{durability}</color> / {maxDurability}"
                : "- / -";
        }

        private void ApplyOsFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            Text[] texts = GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }
    }
}
