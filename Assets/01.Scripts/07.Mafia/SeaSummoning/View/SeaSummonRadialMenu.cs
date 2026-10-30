using Lighthouse.Mafia.SeaSummoning.Net;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [DefaultExecutionOrder(400)]
    public sealed class SeaSummonRadialMenu : MonoBehaviour
    {
        private enum MenuPage
        {
            Ability,
            Weather,
            Monster
        }
        private static readonly string[] SlotIds = { "turtle", "marline", "manta", "angler", "mosawhale", "spinocroc", "shark" };
        private static readonly string[] AbilityLabels = { "몬스터 소환", "날씨 조정", "환영섬 생성" };
        private static readonly string[] WeatherLabels = { "안개", "폭풍우" };
        private static readonly Rect CloseRect = new Rect(430f, 390f, 140f, 140f);
        [SerializeField] private GameObject _menuPrefab;
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        [SerializeField] private SeaSummonSpawnController _spawn;
        [SerializeField] private SeaSummonPointView _pointView;
        [SerializeField] private SeaSummonCameraView _cameraView;
        private readonly int[] _optionIndices = new int[7];
        private readonly bool[] _enabledSlots = new bool[7];
        private readonly UnityEngine.UI.Image[] _icons = new UnityEngine.UI.Image[7];
        private readonly Sprite[] _monsterSprites = new Sprite[7];
        private readonly Sprite[] _abilitySprites = new Sprite[3];
        private readonly Sprite[] _weatherSprites = new Sprite[2];
        private readonly Vector2[] _iconPositions = new Vector2[7];
        private readonly Vector2[] _iconSizes = new Vector2[7];
        private GameObject _canvasObject;
        private RectTransform _imageRect, _wheelRect;
        private SeaSummonWheelGraphic _wheel;
        private UnityEngine.UI.Image _centerIcon;
        private UnityEngine.UI.Text _statusLabel;
        private UnityEngine.UI.Image _titleBanner;
        private UnityEngine.UI.Text _pageTitle;
        private UnityEngine.UI.Text _hoverLabel;
        private Sprite _blankTitleSprite;
        private MenuPage _page;
        private int _slotCount, _hover = -2, _openedFrame;
        private bool _hasFailure;
        private bool _wasPending;
        private Font _font;
        public bool IsOpen { get; private set; }
        public string Status { get; private set; } = "F: 마피아 능력";

        private void Awake()
        {
            if (_menuPrefab == null)
            {
                Debug.LogError($"[{nameof(SeaSummonRadialMenu)}] Menu prefab is not assigned.", this);
                enabled = false;
                return;
            }
            _canvasObject = Instantiate(_menuPrefab, transform);
            _imageRect = (RectTransform)_canvasObject.transform.Find("WheelReference");
            _wheelRect = (RectTransform)_imageRect.Find("Wheel");
            _wheel = _wheelRect.Find("SelectionOverlay").GetComponent<SeaSummonWheelGraphic>();
            _centerIcon = _wheelRect.Find("CenterIcon").GetComponent<UnityEngine.UI.Image>();
            _statusLabel = _imageRect.Find("Status").GetComponent<UnityEngine.UI.Text>();
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            if (_font != null)
            {
                _statusLabel.font = _font;
            }
            _statusLabel.fontStyle = FontStyle.Bold;
            foreach (UnityEngine.UI.Image image in _canvasObject.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.name == "TitleBanner")
                {
                    _titleBanner = image;
                    break;
                }
            }
            var titleObject = new GameObject("PageTitle", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            titleObject.transform.SetParent(_titleBanner.transform, false);
            _pageTitle = titleObject.GetComponent<UnityEngine.UI.Text>();
            _pageTitle.font = _statusLabel.font;
            _pageTitle.fontSize = 44;
            _pageTitle.fontStyle = FontStyle.Bold;
            _pageTitle.alignment = TextAnchor.MiddleCenter;
            _pageTitle.color = new Color(1f, 0.965f, 0.86f);
            _pageTitle.raycastTarget = false;
            _pageTitle.resizeTextForBestFit = true;
            _pageTitle.resizeTextMinSize = 28;
            _pageTitle.resizeTextMaxSize = 44;
            _pageTitle.rectTransform.sizeDelta = new Vector2(260f, 90f);
            _pageTitle.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            _blankTitleSprite = Resources.Load<Sprite>("MafiaAbilityIcons/title_blank");
            var hoverObject = new GameObject("HoverLabel", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            hoverObject.transform.SetParent(_wheelRect, false);
            _hoverLabel = hoverObject.GetComponent<UnityEngine.UI.Text>();
            _hoverLabel.font = _statusLabel.font;
            _hoverLabel.fontStyle = FontStyle.Bold;
            _hoverLabel.alignment = TextAnchor.MiddleCenter;
            _hoverLabel.color = new Color(1f, 0.965f, 0.86f);
            _hoverLabel.raycastTarget = false;
            _hoverLabel.resizeTextForBestFit = true;
            _hoverLabel.resizeTextMinSize = 28;
            _hoverLabel.resizeTextMaxSize = 48;
            _hoverLabel.rectTransform.sizeDelta = new Vector2(300f, 140f);
            for (int i = 0; i < _icons.Length; i++)
            {
                _icons[i] = _wheelRect.Find("Icon " + i).GetComponent<UnityEngine.UI.Image>();
                _iconPositions[i] = _icons[i].rectTransform.anchoredPosition;
                _iconSizes[i] = _icons[i].rectTransform.sizeDelta;
                _monsterSprites[i] = _icons[i].sprite;
            }
            _abilitySprites[0] = _monsterSprites[0];
            _abilitySprites[1] = Resources.Load<Sprite>("MafiaAbilityIcons/storm");
            _abilitySprites[2] = Resources.Load<Sprite>("MafiaAbilityIcons/island");
            _weatherSprites[0] = Resources.Load<Sprite>("MafiaAbilityIcons/fog");
            _weatherSprites[1] = _abilitySprites[1];
            _canvasObject.SetActive(false);
            Status = "F: 마피아 능력";
        }

        private void OnEnable()
        {
            _points.SelectionResponded += HandleSelectionResponded;
            _spawn.SummonResponded += HandleSummonResponded;
            _spawn.QuotaChanged += HandleQuotaChanged;
            _permission.WeatherResponded += HandleWeatherResponded;
        }

        private void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }
            bool validView = _page == MenuPage.Monster ? _cameraView.IsTopViewActive : _cameraView.IsAbilityMenuActive;
            if (!validView || !NetworkClient.ready)
            {
                CloseMenu(true);
                return;
            }
            bool pending = _page == MenuPage.Monster ? _spawn.IsPending : _permission.IsWeatherPending;
            if (Mouse.current == null)
            {
                return;
            }
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_wheelRect, mouse, null, out Vector2 local);
            int hover = GetSlotAt(local);
            if (hover != _hover || pending != _wasPending)
            {
                _hover = hover;
                _wasPending = pending;
                UpdateVisuals();
                if (pending)
                {
                    _statusLabel.text = "서버 응답 대기 중…";
                }
                else
                {
                    string label = hover < 0 ? Status : GetLabel(hover);
                    _statusLabel.text = _page == MenuPage.Monster
                        ? label + "    ·    " + GetQuotaText() + "    ·    F: 닫기"
                        : label + "    ·    우클릭: 뒤로    ·    F: 닫기";
                }
            }
            if (Time.frameCount <= _openedFrame || pending)
            {
                return;
            }
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                GoBack();
                return;
            }
            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_imageRect, mouse, null, out Vector2 imagePoint);
            if (CloseRect.Contains(imagePoint))
            {
                GoBack();
                return;
            }
            if (hover >= 0 && _enabledSlots[hover])
            {
                SelectSlot(hover);
            }
        }

        private void OnDisable()
        {
            _points.SelectionResponded -= HandleSelectionResponded;
            _spawn.SummonResponded -= HandleSummonResponded;
            _spawn.QuotaChanged -= HandleQuotaChanged;
            _permission.WeatherResponded -= HandleWeatherResponded;
            CloseMenu(false);
            if (_cameraView != null && _cameraView.IsAbilityMenuActive)
            {
                _cameraView.RequestToggle();
            }
        }

        private void OnDestroy()
        {
            if (_font != null)
            {
                Destroy(_font);
            }
        }

        private void OnGUI()
        {
            if (_hasFailure && !IsOpen && _cameraView != null && _cameraView.IsTopViewActive)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 300f, Screen.height - 100f, 600f, 60f), "소환 실패: " + Status + "\n지점을 다시 선택해 주세요.");
            }
        }

        public void OpenAbilities()
        {
            if (enabled && _canvasObject != null)
            {
                OpenPage(MenuPage.Ability, "마피아 능력을 선택해 주세요.");
            }
        }

        private int GetSlotAt(Vector2 point)
        {
            float radius = Mathf.Min(_wheelRect.rect.width, _wheelRect.rect.height) * 0.5f;
            if (point.magnitude < radius * 0.438f || point.magnitude > radius * 0.908f)
            {
                return -1;
            }
            float angle = Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg + 180f / _slotCount, 360f);
            return Mathf.FloorToInt(angle / (360f / _slotCount));
        }

        private void OpenPage(MenuPage page, string status)
        {
            _page = page;
            _slotCount = page == MenuPage.Ability ? 3 : page == MenuPage.Weather ? 2 : 7;
            _titleBanner.enabled = true;
            _titleBanner.sprite = _blankTitleSprite;
            _pageTitle.gameObject.SetActive(true);
            _pageTitle.text = page == MenuPage.Ability ? "마피아 능력"
                : page == MenuPage.Weather ? "다음 날 날씨" : "몬스터 선택";
            Status = status;
            RefreshSlots();
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _hover = -2;
            _canvasObject.SetActive(true);
            _pointView.IsSelectionBlocked = true;
            _statusLabel.text = status;
        }

        private void RefreshSlots()
        {
            for (int slot = 0; slot < _icons.Length; slot++)
            {
                _optionIndices[slot] = -1;
                bool monster = _page == MenuPage.Monster;
                if (monster)
                {
                    for (int i = 0; i < _permission.Settings.Options.Count; i++)
                    {
                        if (_permission.Settings.Options[i].MonsterId == SlotIds[slot])
                        {
                            _optionIndices[slot] = i;
                            break;
                        }
                    }
                }
                int index = _optionIndices[slot];
                _enabledSlots[slot] = slot < _slotCount && (!monster || (index >= 0
                    && _permission.Settings.Options[index].IsEnabled && (!_spawn.HasOwnQuota || _spawn.OwnRemainingCount > 0)));
                _icons[slot].gameObject.SetActive(slot < _slotCount);
                _icons[slot].rectTransform.anchoredPosition = _iconPositions[slot];
                _icons[slot].rectTransform.sizeDelta = monster ? _iconSizes[slot] : new Vector2(250f, 220f);
                _icons[slot].sprite = monster ? _monsterSprites[slot]
                    : slot < _slotCount ? (_page == MenuPage.Ability ? _abilitySprites[slot] : _weatherSprites[slot]) : null;
                _icons[slot].preserveAspect = true;
                _icons[slot].rectTransform.localScale = Vector3.one;
                if (!monster && slot < _slotCount)
                {
                    float angle = slot * Mathf.PI * 2f / _slotCount;
                    float radius = Mathf.Min(_wheelRect.rect.width, _wheelRect.rect.height) * 0.34f;
                    _icons[slot].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                }
            }
            _hover = -2;
            UpdateVisuals();
        }

        private string GetLabel(int slot)
        {
            if (_page == MenuPage.Ability)
            {
                return AbilityLabels[slot];
            }
            if (_page == MenuPage.Weather)
            {
                return WeatherLabels[slot];
            }
            int index = _optionIndices[slot];
            return index >= 0 ? _permission.Settings.Options[index].DisplayName : "소환 불가";
        }

        private void UpdateVisuals()
        {
            bool pending = _page == MenuPage.Monster ? _spawn.IsPending : _permission.IsWeatherPending;
            _wheel.SetState(_hover, _enabledSlots, pending, _slotCount);
            for (int i = 0; i < _icons.Length; i++)
            {
                _icons[i].color = _enabledSlots[i] && !pending ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
                _icons[i].rectTransform.localScale = Vector3.one * (i == _hover && _enabledSlots[i] && !pending ? 1.07f : 1f);
            }
            _centerIcon.gameObject.SetActive(false);
            _hoverLabel.text = pending ? "요청 중…" : _hover >= 0 ? GetLabel(_hover) : "선택해 주세요";
        }

        private void SelectSlot(int slot)
        {
            if (_page == MenuPage.Monster)
            {
                _spawn.RequestSummon(_permission.Settings.Options[_optionIndices[slot]].MonsterId);
            }
            else if (_page == MenuPage.Weather)
            {
                _permission.RequestNextDayWeather(slot == 1);
            }
            else if (slot == 0)
            {
                CloseMenu(true);
                _hasFailure = false;
                _cameraView.BeginPointSelection();
            }
            else if (slot == 1)
            {
                OpenPage(MenuPage.Weather, "다음 날 날씨 선택: 안개 / 폭풍우");
            }
            else
            {
                SetStatus("환영섬 생성은 민서의 기능 연결 준비 중입니다.");
            }
        }

        private void GoBack()
        {
            if (_page == MenuPage.Weather)
            {
                OpenAbilities();
            }
            else if (_page == MenuPage.Monster)
            {
                CloseMenu(true);
                Status = "소환할 지점을 다시 선택해 주세요.";
            }
            else
            {
                CloseMenu(true);
                _cameraView.RequestToggle();
            }
        }

        private void SetStatus(string status)
        {
            Status = status;
            _statusLabel.text = status;
        }

        private void HandleSelectionResponded()
        {
            if (_points.HasOwnSelection && _cameraView.IsTopViewActive && !_spawn.IsPending)
            {
                OpenPage(MenuPage.Monster, "몬스터 종류를 클릭하면 즉시 소환합니다.");
            }
        }

        private void HandleQuotaChanged()
        {
            if (IsOpen && _page == MenuPage.Monster)
            {
                RefreshSlots();
            }
        }

        private string GetQuotaText()
        {
            return _spawn.HasOwnQuota ? $"오늘 남은 소환: {_spawn.OwnRemainingCount}/{_spawn.OwnDailyLimit}" : "소환 횟수 확인 중";
        }

        private void HandleWeatherResponded(bool success, string result)
        {
            if (IsOpen && _page == MenuPage.Weather)
            {
                _wasPending = false;
                UpdateVisuals();
                SetStatus(result);
            }
        }

        private void HandleSummonResponded(bool success)
        {
            // A delayed response must not close a newly opened ability menu.
            if (!IsOpen || _page != MenuPage.Monster || !_cameraView.IsTopViewActive)
            {
                return;
            }
            Status = _spawn.OwnResult;
            _hasFailure = !success;
            CloseMenu(true);
            if (success && _cameraView.IsTopViewActive)
            {
                _cameraView.RequestToggle();
            }
        }

        private void CloseMenu(bool cancelSelection)
        {
            bool wasOpen = IsOpen;
            IsOpen = false;
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(false);
            }
            if (_pointView != null)
            {
                _pointView.IsSelectionBlocked = false;
            }
            if (wasOpen && cancelSelection && NetworkClient.active)
            {
                _points.CancelSelection();
            }
        }
    }
}
