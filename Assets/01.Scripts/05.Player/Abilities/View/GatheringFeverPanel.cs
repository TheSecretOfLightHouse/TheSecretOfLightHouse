using Lighthouse.Network.Session.View;
using Lighthouse.UI.SharedMap.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Player.Abilities.View
{
    [DisallowMultipleComponent]
    public sealed class GatheringFeverPanel : MonoBehaviour
    {
        private GatheringFeverRequest _request;
        private GatheringFeverAbility _ability;
        private GatheringFever _fever;
        private PlayerPassiveController _passives;
        private PlayerInput _input;
        private GameObject _canvas;
        private UnityEngine.UI.Button _useButton;
        private UnityEngine.UI.Text _message;
        private Font _font;
        private SharedMapSettings _skin;
        private bool _isOpen;
        private int _openedFrame;
        private bool _hasRejectedResponse;
        private bool _wasInputEnabled;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;

        private void Awake()
        {
            _request = GetComponent<GatheringFeverRequest>();
            _ability = GetComponent<GatheringFeverAbility>();
            _fever = GetComponent<GatheringFever>();
            _passives = GetComponent<PlayerPassiveController>();
            _input = GetComponent<PlayerInput>();
            _request.Responded += HandleResponded;
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            if (!_request || !_request.isActiveAndEnabled || !_request.isOwned
                || NetworkClient.localPlayer != _request.netIdentity
                || !_ability || (_ability.IsFinished && !_hasRejectedResponse) || !_ability.IsOnLand
                || !_passives || !_passives.IsJobAbilityEnabled)
            {
                Hide();
                return;
            }

            if (Time.frameCount > _openedFrame && Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame))
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
            if (_request)
            {
                _request.Responded -= HandleResponded;
            }
            if (_canvas)
            {
                Destroy(_canvas);
            }
            if (_font)
            {
                Destroy(_font);
            }
        }

        public void Show()
        {
            if (_isOpen || !isActiveAndEnabled)
            {
                return;
            }
            if (SessionScreen.IsGameplayOverlayOpen || !_ability.IsOnLand)
            {
                _ability.CompleteRequest();
                return;
            }
            if (!_canvas)
            {
                Build();
            }

            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _wasInputEnabled = _input && _input.enabled;
            _hasRejectedResponse = false;
            _isOpen = true;
            _openedFrame = Time.frameCount;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            if (_input)
            {
                _input.enabled = false;
            }
            _message.text = "준비가 되면 사용하기를 눌러주세요.";
            _useButton.interactable = true;
            _canvas.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (!_isOpen)
            {
                return;
            }
            _isOpen = false;
            if (_canvas)
            {
                _canvas.SetActive(false);
            }
            SessionScreen.SetGameplayOverlayOpen(this, false);
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            if (_input && _wasInputEnabled)
            {
                _input.enabled = true;
            }
            _wasInputEnabled = false;
            if (_ability && (!_request || !_request.IsAwaitingResult))
            {
                _ability.CompleteRequest();
            }
        }

        private void HandleUseClicked()
        {
            if (!_isOpen || _request.IsAwaitingResult)
            {
                return;
            }
            _useButton.interactable = false;
            _message.text = "피버타임을 요청하는 중입니다…";
            if (!_request.TryConfirm())
            {
                _useButton.interactable = true;
                _message.text = "육지에서 사용할 수 있습니다. 현재 상태를 확인해주세요.";
            }
        }

        private void HandleResponded(bool succeeded)
        {
            if (succeeded)
            {
                Hide();
                return;
            }
            if (_isOpen)
            {
                // The request completes the runner before publishing its result.
                _hasRejectedResponse = true;
                _message.text = "오늘 이미 사용했거나 현재 사용할 수 없는 상태입니다.";
                _useButton.interactable = false;
            }
        }

        private void Build()
        {
            // Reuse the shipped brown/cream skins without changing a shared UI asset.
            _skin = Resources.Load<SharedMapSettings>("SharedMapSettings");
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" }, 32);
            _canvas = new GameObject("GatheringFeverCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            _canvas.SetActive(false);
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.GetComponent<Canvas>().sortingOrder = 170;
            UnityEngine.UI.CanvasScaler scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            UnityEngine.UI.Image shade = Box("Shade", _canvas.transform, Vector2.zero,
                Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            UnityEngine.UI.Image panel = Box("FeverPanel", shade.transform, Vector2.zero,
                new Vector2(700f, 460f), new Color(0.20f, 0.11f, 0.055f));
            if (_skin && _skin.PanelSprite)
            {
                panel.sprite = _skin.PanelSprite;
                panel.type = UnityEngine.UI.Image.Type.Sliced;
                panel.color = Color.white;
            }
            Label("Title", panel.transform, new Vector2(0f, 158f), new Vector2(620f, 55f), 34).text = "채집가 · 피버타임";
            Label("Effect", panel.transform, new Vector2(0f, 60f), new Vector2(610f, 100f), 28).text =
                $"{_fever.Duration:0.#}초 동안\n채집한 자원 획득량이 2배가 됩니다.";
            Label("Details", panel.transform, new Vector2(0f, -35f), new Vector2(610f, 80f), 21).text =
                "하루 1회 · 사용하기를 누르면 시작\n보너스 자원도 바닥에 떨어지며, 동료도 주울 수 있어요.";
            _message = Label("Message", panel.transform, new Vector2(0f, -100f), new Vector2(620f, 45f), 20);
            Button("닫기", panel.transform, new Vector2(-150f, -168f), Hide);
            _useButton = Button("사용하기", panel.transform, new Vector2(150f, -168f), HandleUseClicked);
        }

        private UnityEngine.UI.Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            item.transform.SetParent(parent, false);
            UnityEngine.UI.Image image = item.GetComponent<UnityEngine.UI.Image>();
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = position;
            image.color = color;
            return image;
        }

        private UnityEngine.UI.Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            item.transform.SetParent(parent, false);
            UnityEngine.UI.Text label = item.GetComponent<UnityEngine.UI.Text>();
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            label.font = _font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.96f, 0.92f, 0.79f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Button Button(string text, Transform parent, Vector2 position, UnityEngine.Events.UnityAction clicked)
        {
            UnityEngine.UI.Image image = Box(text, parent, position, new Vector2(240f, 58f), new Color(0.30f, 0.19f, 0.10f));
            if (_skin && _skin.ButtonSprite)
            {
                image.sprite = _skin.ButtonSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.white;
            }
            UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(clicked);
            Label("Label", image.transform, Vector2.zero, new Vector2(220f, 50f), 24).text = text;
            return button;
        }
    }
}
