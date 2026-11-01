using System.Collections.Generic;
using Lighthouse.Common.UI;
using Lighthouse.Network.Session.View;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.UI.PlayGuide
{
    // This local overlay belongs to the persistent session UI, never to the networked player.
    [DisallowMultipleComponent]
    public sealed class PlayGuideScreen : MonoBehaviour
    {
        private const string AnsweredKey = "Lighthouse.PlayGuide.Answered.v1";
        private static readonly Color Cream = new Color32(255, 240, 207, 255);
        private static readonly Color Mint = new Color32(157, 233, 204, 255);
        private static readonly Color Yellow = new Color32(255, 209, 91, 255);
        private static readonly Color Muted = new Color32(195, 183, 160, 255);
        private static readonly Color Ink = new Color32(51, 39, 27, 255);

        private readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        private readonly UnityEngine.UI.Image[] _cardIcons = new UnityEngine.UI.Image[3];
        private readonly UnityEngine.UI.Text[] _cardTitles = new UnityEngine.UI.Text[3];
        private readonly UnityEngine.UI.Text[] _cardDescriptions = new UnityEngine.UI.Text[3];
        private readonly UnityEngine.UI.Text[] _lines = new UnityEngine.UI.Text[4];
        private UnityEngine.UI.Button[] _tabs;
        private UnityEngine.UI.Text[] _tabLabels;
        private UnityEngine.UI.Text _title;
        private UnityEngine.UI.Text _intro;
        private UnityEngine.UI.Text _heading;
        private UnityEngine.UI.Text _tip;
        private UnityEngine.UI.Text _nextLabel;
        private UnityEngine.UI.Button _previous;
        private GameObject _launcher;
        private GameObject _modal;
        private GameObject _window;
        private GameObject _question;
        private RectTransform _layoutFrame;
        private Sprite _windowSprite;
        private Sprite _primarySprite;
        private Sprite _secondarySprite;
        private Sprite _dividerSprite;
        private PlayerInput _input;
        private PlayerInputReader _reader;
        private bool _wasInputEnabled;
        private bool _wasReaderEnabled;
        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLock;
        private bool _isOpen;
        private bool _isInGame;
        private bool _isLobby;
        private bool _isDialogOpen;
        private bool _askedInLobby;
        private bool _needsQuestion;
        private int _page;
        private int _closedFrame = -1;

        public bool BlocksSessionEscape => _isOpen || _closedFrame == Time.frameCount;

        private void OnDisable()
        {
            Close();
        }

        private void OnRectTransformDimensionsChange()
        {
            FitLayout();
        }

        public static PlayGuideScreen Create(Transform parent)
        {
            var root = new GameObject("PlayGuide", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            Canvas parentCanvas = parent.GetComponent<Canvas>();
            if (!parentCanvas)
            {
                parentCanvas = parent.GetComponentInParent<Canvas>();
            }
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 510;
            UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            // Expand fits the whole approved layout on both 16:9 and narrower screens.
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (parentCanvas)
            {
                // SessionScreen already owns a canvas. A nested scaler cannot drive its size.
                scaler.enabled = false;
                RectTransform rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            PlayGuideScreen screen = root.AddComponent<PlayGuideScreen>();
            screen.Build();
            screen.FitLayout();
            return screen;
        }

        public void Tick(bool isLobby, bool isInGame, bool isDialogOpen)
        {
            if ((_isLobby != isLobby || _isInGame != isInGame) && _isOpen)
            {
                Close();
            }
            if (!isLobby)
            {
                _askedInLobby = false;
            }
            _isLobby = isLobby;
            _isInGame = isInGame;
            _isDialogOpen = isDialogOpen;
            bool canOpen = (isLobby || isInGame) && !isDialogOpen;
            _launcher.SetActive(canOpen && !_isOpen && !SessionScreen.IsGameplayOverlayOpen);
            if (isLobby && !_askedInLobby && _needsQuestion && !isDialogOpen
                && !SessionScreen.IsGameplayOverlayOpen)
            {
                _askedInLobby = true;
                BorrowInput();
                _question.SetActive(true);
                _window.SetActive(false);
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame))
            {
                Close();
            }
            else if (canOpen && keyboard.f1Key.wasPressedThisFrame && !SessionScreen.IsGameplayOverlayOpen)
            {
                OpenGuide();
            }
        }

        private void OpenGuide()
        {
            if ((!_isLobby && !_isInGame) || _isDialogOpen || (!_isOpen && SessionScreen.IsGameplayOverlayOpen))
            {
                return;
            }
            BorrowInput();
            _question.SetActive(false);
            _window.SetActive(true);
            ShowPage(0);
        }

        private void Answer(bool isFirstTime)
        {
            PlayerPrefs.SetInt(AnsweredKey, 1);
            PlayerPrefs.Save();
            _needsQuestion = false;
            if (isFirstTime)
            {
                OpenGuide();
            }
            else
            {
                Close();
            }
        }

        private void BorrowInput()
        {
            if (_isOpen)
            {
                return;
            }
            _isOpen = true;
            _modal.SetActive(true);
            _launcher.SetActive(false);
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _input = null;
            _reader = null;
            if (_isInGame && NetworkClient.localPlayer)
            {
                _input = NetworkClient.localPlayer.GetComponent<PlayerInput>();
                _reader = NetworkClient.localPlayer.GetComponent<PlayerInputReader>();
            }
            _wasInputEnabled = _input && _input.enabled;
            _wasReaderEnabled = _reader && _reader.enabled;
            if (_input)
            {
                _input.enabled = false;
            }
            if (_reader)
            {
                _reader.enabled = false;
            }
            SessionScreen.SetGameplayOverlayOpen(this, true);
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }
            _isOpen = false;
            _closedFrame = Time.frameCount;
            _modal.SetActive(false);
            SessionScreen.SetGameplayOverlayOpen(this, false);
            if (_input && _wasInputEnabled)
            {
                _input.enabled = true;
            }
            if (_reader && _wasReaderEnabled)
            {
                _reader.enabled = true;
            }
            _input = null;
            _reader = null;
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
        }

        private void ShowPage(int index)
        {
            _page = Mathf.Clamp(index, 0, PlayGuideContent.Pages.Length - 1);
            PlayGuideContent.Page page = PlayGuideContent.Pages[_page];
            _title.text = page.Title;
            _intro.text = page.Intro;
            _heading.text = page.Heading;
            _tip.text = page.Tip;
            for (int i = 0; i < 3; i++)
            {
                _cardIcons[i].sprite = _icons[page.Icons[i]];
                _cardTitles[i].text = page.CardTitles[i];
                _cardDescriptions[i].text = page.CardDescriptions[i];
            }
            for (int i = 0; i < _lines.Length; i++)
            {
                _lines[i].text = page.Lines[i];
            }
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool selected = i == _page;
                _tabs[i].GetComponent<UnityEngine.UI.Image>().sprite = selected ? _primarySprite : null;
                _tabs[i].GetComponent<UnityEngine.UI.Image>().color = selected ? Color.white : Color.clear;
                _tabLabels[i].color = selected ? Ink : Cream;
            }
            _previous.gameObject.SetActive(_page > 0);
            _nextLabel.text = _page < PlayGuideContent.Pages.Length - 1
                ? "다음 · " + PlayGuideContent.Pages[_page + 1].Label : "읽기 완료 · 닫기";
        }

        private void Previous()
        {
            ShowPage(_page - 1);
        }

        private void Next()
        {
            if (_page == PlayGuideContent.Pages.Length - 1)
            {
                Close();
            }
            else
            {
                ShowPage(_page + 1);
            }
        }

        private void Build()
        {
            _needsQuestion = PlayerPrefs.GetInt(AnsweredKey, 0) == 0;
            _windowSprite = LoadSprite("window");
            _primarySprite = LoadSprite("button-primary");
            _secondarySprite = LoadSprite("button-secondary");
            _dividerSprite = LoadSprite("divider-horizontal");
            string[] keys = { "crew", "tower", "wood", "tools", "gear", "mask", "shield", "boat", "axe",
                "keyboard", "cannon", "flare", "bag", "vote", "monster" };
            for (int i = 0; i < keys.Length; i++)
            {
                _icons.Add(keys[i], LoadSprite("icon-" + keys[i]));
            }

            RectTransform launcherRect = Rect(transform, "GuideButton", 0f, 0f, 228f, 49f);
            UnityEngine.UI.Image launcherHitArea = launcherRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            launcherHitArea.color = Color.clear;
            UnityEngine.UI.Button launcher = launcherRect.gameObject.AddComponent<UnityEngine.UI.Button>();
            launcher.targetGraphic = launcherHitArea;
            launcher.onClick.AddListener(OpenGuide);
            Picture(launcherRect, "Keycap", 0f, 3f, 42f, 42f, LoadSprite("keycap"));
            UnityEngine.UI.Text key = Label(launcherRect, "Key", 0f, 3f, 42f, 42f, "F1", 18, Cream, true);
            key.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Text description = Label(launcherRect, "Description", 53f, 0f, 175f, 49f,
                "항해 안내서", 22, Cream, true);
            description.alignment = TextAnchor.MiddleLeft;
            launcherRect.anchorMin = new Vector2(1f, 1f);
            launcherRect.anchorMax = new Vector2(1f, 1f);
            launcherRect.anchoredPosition = new Vector2(-248f, -22f);
            _launcher = launcher.gameObject;
            _launcher.SetActive(false);

            var modal = new GameObject("GuideModal", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            modal.transform.SetParent(transform, false);
            RectTransform modalRect = (RectTransform)modal.transform;
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            modal.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.72f);
            _modal = modal;
            RectTransform frame = Rect(modal.transform, "GuideLayout", 0f, 0f, 1600f, 1000f);
            _layoutFrame = frame;
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = frame.anchorMin;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            BuildWindow(frame);
            BuildQuestion(frame);
            _modal.SetActive(false);
            _window.SetActive(false);
            _question.SetActive(false);
        }

        private void FitLayout()
        {
            if (!_layoutFrame)
            {
                return;
            }
            RectTransform root = (RectTransform)transform;
            float scale = Mathf.Min(root.rect.width / 1600f, root.rect.height / 1000f);
            if (scale > 0f)
            {
                _layoutFrame.localScale = Vector3.one * scale;
            }
        }

        private void BuildWindow(Transform frame)
        {
            _window = Rect(frame, "GuideWindow", 0f, 0f, 1600f, 1000f).gameObject;
            Transform window = _window.transform;
            Picture(window, "WindowBackground", 60f, 88f, 1480f, 853f, _windowSprite, true);
            Picture(window, "Lighthouse", 110f, 120f, 98f, 66f, _icons["tower"]);
            Label(window, "WindowTitle", 235f, 122f, 750f, 55f, "항해 안내서", 37, Cream, true);
            Label(window, "Subtitle", 238f, 178f, 700f, 32f, "첫 출항부터 등대 점화까지", 20, Muted);
            Button(window, "Close", 1360f, 127f, 124f, 56f, "닫기", false, Close);
            Divider(window, 115f, 230f, 1370f);
            _tabs = new UnityEngine.UI.Button[PlayGuideContent.Pages.Length];
            _tabLabels = new UnityEngine.UI.Text[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                float y = 255f + i * 61f;
                _tabs[i] = Button(window, "Topic" + i, 110f, y, 330f, 52f,
                    (i + 1).ToString("00") + "   " + PlayGuideContent.Pages[i].Label, false, () => ShowPage(index));
                _tabLabels[i] = _tabs[i].GetComponentInChildren<UnityEngine.UI.Text>();
                _tabLabels[i].alignment = TextAnchor.MiddleLeft;
                _tabLabels[i].rectTransform.anchoredPosition = new Vector2(19f, 0f);
                _tabLabels[i].rectTransform.sizeDelta = new Vector2(292f, 52f);
            }
            Label(window, "NavigationHint", 119f, 846f, 330f, 40f, "항목을 눌러 바로 이동할 수 있어요.", 17, Muted);
            UnityEngine.UI.Image vertical = Picture(window, "VerticalDivider", 468f, 260f, 2f, 616f, null);
            vertical.color = new Color32(102, 92, 73, 255);
            _title = Label(window, "TopicTitle", 511f, 262f, 965f, 49f, "", 27, Cream, true);
            _intro = Label(window, "TopicIntro", 513f, 315f, 965f, 40f, "", 20, Muted);
            for (int i = 0; i < 3; i++)
            {
                float x = 515f + i * 327f;
                _cardIcons[i] = Picture(window, "TopicIcon" + i, x + 70f, 378f, 110f, 86f, null);
                _cardIcons[i].preserveAspect = true;
                _cardTitles[i] = Label(window, "CardTitle" + i, x - 25f, 471f, 300f, 43f, "", 23, Yellow, true);
                _cardTitles[i].alignment = TextAnchor.MiddleCenter;
                _cardDescriptions[i] = Label(window, "CardDescription" + i, x - 25f, 516f, 300f, 44f, "", 18, Cream);
                _cardDescriptions[i].alignment = TextAnchor.MiddleCenter;
                if (i < 2)
                {
                    Label(window, "Arrow" + i, x + 260f, 394f, 48f, 50f, "→", 32, Mint);
                }
            }
            Divider(window, 511f, 577f, 959f);
            _heading = Label(window, "ExplanationTitle", 514f, 606f, 955f, 43f, "", 24, Mint, true);
            for (int i = 0; i < 4; i++)
            {
                Label(window, "LineNumber" + i, 515f, 660f + i * 37f, 30f, 35f, (i + 1) + ".", 20, Yellow, true);
                _lines[i] = Label(window, "Explanation" + i, 551f, 660f + i * 37f, 925f, 35f, "", 19, Cream);
            }
            _tip = Label(window, "Tip", 515f, 812f, 960f, 34f, "", 17, Muted);
            _previous = Button(window, "Previous", 513f, 850f, 165f, 52f, "이전", false, Previous);
            UnityEngine.UI.Button next = Button(window, "Next", 1197f, 846f, 286f, 59f, "다음", true, Next);
            _nextLabel = next.GetComponentInChildren<UnityEngine.UI.Text>();
        }

        private void BuildQuestion(Transform frame)
        {
            _question = Rect(frame, "FirstVisitQuestion", 0f, 0f, 1600f, 1000f).gameObject;
            Transform question = _question.transform;
            Picture(question, "QuestionBackground", 375f, 167f, 850f, 684f, _windowSprite, true);
            Picture(question, "Sailor", 752f, 208f, 96f, 113f, _icons["crew"]);
            UnityEngine.UI.Text title = Label(question, "QuestionTitle", 450f, 338f, 700f, 70f, "첫 항해인가요?", 42, Cream, true);
            title.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Text intro = Label(question, "QuestionIntro", 420f, 414f, 760f, 48f,
                "처음이라면 출항 전에 안내서를 읽어보세요.", 24, Cream);
            intro.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Text detail = Label(question, "QuestionDetail", 420f, 468f, 760f, 44f,
                "조작 · 제작 · 추리 · 승리 조건을 차근차근 알려드려요.", 20, Muted);
            detail.alignment = TextAnchor.MiddleCenter;
            Button(question, "FirstTime", 488f, 535f, 624f, 79f, "네, 처음이에요 · 안내서 보기", true, () => Answer(true));
            Button(question, "Experienced", 488f, 637f, 624f, 78f, "아니요, 플레이해 봤어요", false, () => Answer(false));
            UnityEngine.UI.Text saved = Label(question, "SavedLocally", 450f, 745f, 700f, 40f,
                "답변은 이 PC에 저장됩니다.", 18, Muted);
            saved.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Text hint = Label(question, "ReopenHint", 420f, 785f, 760f, 40f,
                "도움말은 로비와 게임 중에도 F1로 다시 열 수 있어요.", 18, Mint);
            hint.alignment = TextAnchor.MiddleCenter;
        }

        private static Sprite LoadSprite(string name)
        {
            return Resources.Load<Sprite>("PlayGuide/" + name);
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static UnityEngine.UI.Image Picture(Transform parent, string name, float x, float y,
            float width, float height, Sprite sprite, bool sliced = false)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = !sliced;
            return image;
        }

        private void Divider(Transform parent, float x, float y, float width)
        {
            UnityEngine.UI.Image image = Picture(parent, "Divider", x, y, width, 3f, _dividerSprite);
            image.preserveAspect = false;
        }

        private static UnityEngine.UI.Text Label(Transform parent, string name, float x, float y,
            float width, float height, string value, int size, Color color, bool bold = false)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            UnityEngine.UI.Text label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = KoreanUiFont.Font;
            label.text = value;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Button Button(Transform parent, string name, float x, float y,
            float width, float height, string title, bool primary, UnityEngine.Events.UnityAction clicked)
        {
            UnityEngine.UI.Image image = Picture(parent, name, x, y, width, height,
                primary ? _primarySprite : _secondarySprite, true);
            image.raycastTarget = true;
            UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.86f, 1f, 0.93f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 0.77f, 1f);
            button.colors = colors;
            button.onClick.AddListener(clicked);
            UnityEngine.UI.Text label = Label(image.transform, "Label", 8f, 0f, width - 16f, height,
                title, 22, primary ? Ink : Cream, true);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
