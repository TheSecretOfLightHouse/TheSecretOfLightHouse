using Lighthouse.Network.Session.View;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.UI.SharedMap.View
{
    [DefaultExecutionOrder(500)]
    public sealed class SharedMapScreen : MonoBehaviour
    {
        private SharedMapRuntime _runtime;
        private GameObject _canvas, _panel, _tools;
        private UnityEngine.UI.Text _title, _prompt;
        private UnityEngine.UI.Text _toolLabel;
        private RectTransform _landmark;
        private CanvasGroup _toolsGroup;
        private SharedMapSettings _settings;
        private readonly UnityEngine.UI.Image[] _colorButtons = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Image[] _sizeButtons = new UnityEngine.UI.Image[3];
        private UnityEngine.UI.Image _eraseButton;
        private SharedMapGraphic _graphic;
        private Font _font;
        private bool _open, _erasing, _drawing, _wasEditing;
        private bool _savedInputEnabled, _savedReaderEnabled, _savedCursorVisible;
        private CursorLockMode _savedCursorLock;
        private PlayerInput _playerInput;
        private PlayerInputReader _reader;
        private GameObject _eventSystem;
        private uint _editSession, _epoch, _stroke;
        private byte _color;
        private byte _brushSize = 1;
        private Vector2 _lastPoint;
        private double _nextPoint;

        public void Bind(SharedMapRuntime runtime)
        {
            _runtime = runtime;
            _runtime.Changed += HandleChanged;
            Build();
        }

        private void Update()
        {
            if (!_runtime || !_runtime.IsAvailable)
            {
                SetOpen(false);
                if (_prompt)
                {
                    _prompt.text = string.Empty;
                }
                return;
            }
            SharedMapUpdate state = _runtime.State;
            bool editing = state.IsEditing && NetworkTime.time < state.Deadline;
            if (_wasEditing && !editing)
            {
                SetOpen(false);
            }
            _wasEditing = editing;
            PlayerController player = NetworkClient.localPlayer.GetComponent<PlayerController>();
            if (_open && player.IsDowned)
            {
                SetOpen(false);
            }
            bool near = _runtime.IsNearTable(player);
            _prompt.text = !_open && near
                ? state.IsEvening ? $"공유지도  ·  준비 {state.ReadyCount}/4  ·  E 준비  /  M 열람" : "공유지도  ·  저녁에 함께 작성  /  M 열람"
                : string.Empty;
            if (Keyboard.current != null)
            {
                if (!editing && Keyboard.current.mKey.wasPressedThisFrame)
                {
                    if (_open || !SessionScreen.IsGameplayOverlayOpen)
                    {
                        SetOpen(!_open);
                    }
                }
                if (_open && !editing && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    SetOpen(false);
                }
            }
            if (!_open)
            {
                return;
            }
            _toolsGroup.interactable = editing;
            _toolsGroup.blocksRaycasts = editing;
            _toolsGroup.alpha = editing ? 1f : 0.45f;
            string remaining = editing ? $"공동 작성  ·  {Mathf.CeilToInt((float)(state.Deadline - NetworkTime.time))}초" : "공유지도  ·  M / Esc 닫기";
            _title.text = remaining + (_runtime.IsCaughtUp ? string.Empty : "  ·  동기화 중")
                + (_runtime.Operations.Count >= SharedMapRules.MaxOperations ? "  ·  지도 기록 한도 도달" : string.Empty);
            if (editing && _runtime.IsCaughtUp && !player.IsDowned)
            {
                ReadDrawing();
            }
        }

        private void OnDisable()
        {
            SetOpen(false);
        }

        private void OnDestroy()
        {
            SetOpen(false);
            if (_runtime)
            {
                _runtime.Changed -= HandleChanged;
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

        private void HandleChanged()
        {
            SharedMapUpdate state = _runtime.State;
            bool editing = state.IsEditing && NetworkTime.time < state.Deadline;
            if (_epoch != state.Epoch)
            {
                SetOpen(false);
                _epoch = state.Epoch;
                _editSession = state.EditSession;
                _stroke = 0;
            }
            if (editing && (_editSession != state.EditSession || !_wasEditing))
            {
                _editSession = state.EditSession;
                SetOpen(true);
            }
            if (!editing && _wasEditing)
            {
                SetOpen(false);
            }
            _wasEditing = editing;
            _landmark.anchorMin = state.Lighthouse;
            _landmark.anchorMax = state.Lighthouse;
            _landmark.anchoredPosition = Vector2.zero;
            _graphic.SetVerticesDirty();
        }

        private void SetOpen(bool open)
        {
            if (_open == open)
            {
                return;
            }
            _open = open;
            _drawing = false;
            _panel.SetActive(open);
            SessionScreen.SetGameplayOverlayOpen(this, open);
            if (open)
            {
                if (!UnityEngine.EventSystems.EventSystem.current && !_eventSystem)
                {
                    _eventSystem = new GameObject("SharedMapEventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                    _eventSystem.transform.SetParent(transform, false);
                }
                _savedCursorLock = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (NetworkClient.localPlayer)
                {
                    _playerInput = NetworkClient.localPlayer.GetComponent<PlayerInput>();
                    _reader = NetworkClient.localPlayer.GetComponent<PlayerInputReader>();
                    _savedInputEnabled = _playerInput && _playerInput.enabled;
                    _savedReaderEnabled = _reader && _reader.enabled;
                    if (_playerInput)
                    {
                        _playerInput.enabled = false;
                    }
                    if (_reader)
                    {
                        _reader.enabled = false;
                    }
                }
            }
            else
            {
                if (_reader && _savedReaderEnabled)
                {
                    _reader.enabled = true;
                }
                if (_playerInput && _savedInputEnabled)
                {
                    _playerInput.enabled = true;
                }
                Cursor.lockState = _savedCursorLock;
                Cursor.visible = _savedCursorVisible;
            }
        }

        private void ReadDrawing()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || (!mouse.leftButton.isPressed && !(_drawing && mouse.leftButton.wasReleasedThisFrame)))
            {
                _drawing = false;
                return;
            }
            RectTransform board = _graphic.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, mouse.position.ReadValue(), null, out Vector2 local);
            Vector2 point = new Vector2((local.x - board.rect.xMin) / board.rect.width, (local.y - board.rect.yMin) / board.rect.height);
            if (!SharedMapRules.IsValidPoint(point))
            {
                _drawing = false;
                return;
            }
            if (_erasing)
            {
                if (Time.unscaledTimeAsDouble < _nextPoint)
                {
                    return;
                }
                _nextPoint = Time.unscaledTimeAsDouble + 0.05d;
                if (_graphic.TryFindStroke(point, out SharedMapOperation target))
                {
                    target.IsErase = true;
                    _runtime.Submit(target);
                }
                return;
            }
            if (!_drawing)
            {
                _drawing = true;
                _stroke++;
                _lastPoint = point;
            }
            else if ((point - _lastPoint).sqrMagnitude < 0.000001f)
            {
                return;
            }
            _runtime.Submit(new SharedMapOperation { Stroke = _stroke, From = _lastPoint, To = point, Color = _color, BrushSize = _brushSize });
            _lastPoint = point;
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _drawing = false;
            }
        }

        private void Build()
        {
            // Match the existing HUD's OS-font fallback so Korean does not require a new font package.
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 28);
            _settings = Resources.Load<SharedMapSettings>("SharedMapSettings");
            _canvas = new GameObject("SharedMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            UnityEngine.UI.CanvasScaler scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _prompt = Label("MapPrompt", _canvas.transform, new Vector2(0f, -400f), new Vector2(1000f, 60f), 25);
            _panel = Box("SharedMapPanel", _canvas.transform, Vector2.zero, new Vector2(1450f, 1010f), new Color(0.20f, 0.11f, 0.055f)).gameObject;
            UnityEngine.UI.Image frame = _panel.GetComponent<UnityEngine.UI.Image>();
            if (_settings && _settings.PanelSprite)
            {
                frame.sprite = _settings.PanelSprite;
                frame.color = Color.white;
            }
            UnityEngine.UI.Text heading = Label("MapHeading", _panel.transform, new Vector2(-400f, 450f), new Vector2(500f, 60f), 45);
            heading.text = "공 동 항 해 지 도";
            _title = Label("Title", _panel.transform, new Vector2(350f, 450f), new Vector2(560f, 60f), 25);
            UnityEngine.UI.Image boardFrame = Box("PaperFrame", _panel.transform, new Vector2(-205f, -15f), new Vector2(852f, 852f), new Color(0.96f, 0.87f, 0.68f));
            UnityEngine.UI.Image paper = Box("Paper", boardFrame.transform, Vector2.zero, new Vector2(826f, 826f), new Color(0.91f, 0.84f, 0.66f));
            var drawing = new GameObject("Drawing", typeof(RectTransform), typeof(CanvasRenderer), typeof(SharedMapGraphic));
            drawing.transform.SetParent(paper.transform, false);
            _graphic = drawing.GetComponent<SharedMapGraphic>();
            _graphic.rectTransform.sizeDelta = new Vector2(826f, 826f);
            _graphic.raycastTarget = true;
            _graphic.Bind(_runtime);
            UnityEngine.UI.Text north = Label("North", paper.transform, new Vector2(360f, 360f), new Vector2(70f, 70f), 26);
            north.text = "N ↑";
            north.color = SharedMapGraphic.InkColors[0];
            north.transform.SetAsLastSibling();
            UnityEngine.UI.Image landmark = Box("LighthouseLandmark", paper.transform, Vector2.zero, new Vector2(58f, 65f), Color.white);
            landmark.sprite = _settings ? _settings.LighthouseSprite : null;
            landmark.raycastTarget = false;
            landmark.preserveAspect = true;
            _landmark = landmark.rectTransform;
            UnityEngine.UI.Text landmarkName = Label("LighthouseName", landmark.transform, new Vector2(0f, -45f), new Vector2(120f, 30f), 19);
            landmarkName.text = "등대섬";
            landmarkName.color = SharedMapGraphic.InkColors[0];
            _tools = new GameObject("Tools", typeof(RectTransform), typeof(CanvasGroup));
            _tools.transform.SetParent(_panel.transform, false);
            _toolsGroup = _tools.GetComponent<CanvasGroup>();
            Label("ToolsHeading", _tools.transform, new Vector2(445f, 320f), new Vector2(330f, 55f), 29).text = "나의 기록 도구";
            Label("ColorHeading", _tools.transform, new Vector2(445f, 250f), new Vector2(330f, 45f), 22).text = "잉크 색상";
            string[] colorNames = { "먹색", "붉은색", "푸른색", "초록색" };
            for (int i = 0; i < 4; i++)
            {
                byte index = (byte)i;
                Vector2 position = new Vector2(355f + (i % 2) * 180f, 185f - (i / 2) * 70f);
                _colorButtons[i] = Button(colorNames[i], _tools.transform, position, new Vector2(166f, 56f),
                    () => { _color = index; _erasing = false; _drawing = false; RefreshTools(); });
            }
            Label("WidthHeading", _tools.transform, new Vector2(445f, 20f), new Vector2(330f, 45f), 22).text = "붓 굵기";
            string[] sizeNames = { "가늘게", "보통", "굵게" };
            for (int i = 0; i < 3; i++)
            {
                byte index = (byte)i;
                _sizeButtons[i] = Button(sizeNames[i], _tools.transform, new Vector2(325f + i * 120f, -42f), new Vector2(110f, 56f),
                    () => { _brushSize = index; _erasing = false; _drawing = false; RefreshTools(); });
            }
            _eraseButton = Button("획 지우개", _tools.transform, new Vector2(445f, -145f), new Vector2(346f, 60f),
                () => { _erasing = true; _drawing = false; RefreshTools(); });
            _toolLabel = Label("SelectedTool", _tools.transform, new Vector2(445f, -245f), new Vector2(360f, 50f), 23);
            Label("DrawingHint", _panel.transform, new Vector2(445f, -355f), new Vector2(350f, 125f), 22).text = "마우스를 누른 채 그려보세요\n섬과 이름을 자유롭게 기록하세요";
            Label("Footer", _panel.transform, new Vector2(-190f, -470f), new Vector2(870f, 40f), 20).text = "함께 남긴 기록을 믿고 항해하세요";
            RefreshTools();
            _panel.SetActive(false);
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
            UnityEngine.UI.Text text = item.GetComponent<UnityEngine.UI.Text>();
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.96f, 0.92f, 0.79f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private UnityEngine.UI.Image Button(string name, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction clicked)
        {
            UnityEngine.UI.Image image = Box(name, parent, position, size, new Color(0.30f, 0.19f, 0.10f));
            UnityEngine.UI.Image outline = Box("Outline", image.transform, Vector2.zero, size, Color.white);
            outline.raycastTarget = false;
            if (_settings)
            {
                outline.sprite = _settings.ButtonSprite;
            }
            UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(clicked);
            Label("Label", image.transform, Vector2.zero, size - new Vector2(12f, 8f), 22).text = name;
            return image;
        }

        private void RefreshTools()
        {
            for (int i = 0; i < _colorButtons.Length; i++)
            {
                SetSelected(_colorButtons[i], !_erasing && _color == i);
            }
            for (int i = 0; i < _sizeButtons.Length; i++)
            {
                SetSelected(_sizeButtons[i], !_erasing && _brushSize == i);
            }
            SetSelected(_eraseButton, _erasing);
            if (_toolLabel)
            {
                string[] colors = { "먹색", "붉은색", "푸른색", "초록색" };
                string[] sizes = { "가는 붓", "기본 붓", "굵은 붓" };
                _toolLabel.text = _erasing ? "획을 눌러 지우기" : colors[_color] + "  ·  " + sizes[_brushSize];
            }
        }

        private void SetSelected(UnityEngine.UI.Image image, bool selected)
        {
            if (!image)
            {
                return;
            }
            image.color = selected ? new Color(0.48f, 0.31f, 0.13f) : new Color(0.27f, 0.17f, 0.09f);
            UnityEngine.UI.Image outline = image.transform.Find("Outline").GetComponent<UnityEngine.UI.Image>();
            if (_settings)
            {
                outline.sprite = selected ? _settings.SelectedButtonSprite : _settings.ButtonSprite;
            }
        }
    }
}
