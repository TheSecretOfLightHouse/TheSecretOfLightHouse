using Lighthouse.Mafia.SeaSummoning.Net;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [DefaultExecutionOrder(400)]
    public sealed class SeaSummonRadialMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _menuPrefab;
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        [SerializeField] private SeaSummonSpawnController _spawn;
        [SerializeField] private SeaSummonPointView _pointView;
        [SerializeField] private SeaSummonCameraView _cameraView;
        private static readonly string[] SlotIds = { "turtle", "marline", "manta", "angler", "mosawhale", "spinocroc", "shark" };
        private readonly int[] _optionIndices = new int[7];
        private readonly bool[] _enabledSlots = new bool[7];
        private GameObject _canvasObject;
        private RectTransform _imageRect, _wheelRect;
        private readonly Image[] _icons = new Image[7];
        private SeaSummonWheelGraphic _wheel;
        private Image _centerIcon;
        private Text _statusLabel;
        private int _hover = -2, _openedFrame;
        private bool _hasFailure;
        public bool IsOpen { get; private set; }
        public string Status { get; private set; } = "지점을 선택하면 몬스터 메뉴가 열립니다.";

        private void Awake()
        {
            if (_menuPrefab == null)
            {
                Debug.LogError("Sea summon menu prefab is not assigned.", this);
                enabled = false;
                return;
            }
            _canvasObject = Instantiate(_menuPrefab, transform);
            _imageRect = (RectTransform)_canvasObject.transform.Find("WheelReference");
            _wheelRect = (RectTransform)_imageRect.Find("Wheel");
            _wheel = _wheelRect.Find("SelectionOverlay").GetComponent<SeaSummonWheelGraphic>();
            for (int i = 0; i < 7; i++)
            {
                _icons[i] = _wheelRect.Find("Icon " + i).GetComponent<Image>();
            }
            _centerIcon = _wheelRect.Find("CenterIcon").GetComponent<Image>();
            _statusLabel = _imageRect.Find("Status").GetComponent<Text>();
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            if (font != null) _statusLabel.font = font;
            _canvasObject.SetActive(false);
        }
        private void OnEnable()
        {
            _points.SelectionResponded += HandleSelectionResponded;
            _spawn.SummonResponded += HandleSummonResponded;
            _spawn.QuotaChanged += HandleQuotaChanged;
        }
        private void LateUpdate()
        {
            if (_cameraView != null && !_cameraView.IsTopViewActive) _hasFailure = false;
            if (!IsOpen) return;
            if (_cameraView == null || !_cameraView.IsTopViewActive || !NetworkClient.ready)
            {
                CloseMenu(true);
                return;
            }
            for (int i = 0; i < _icons.Length; i++)
            {
                float scale = i == _hover && _enabledSlots[i] && !_spawn.IsPending ? 1.07f : 1f;
                _icons[i].rectTransform.localScale = Vector3.Lerp(_icons[i].rectTransform.localScale,
                    Vector3.one * scale, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            }
            if (Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_wheelRect, mouse, null, out Vector2 local);
            int hover = GetSlotAt(local);
            bool pending = _spawn.IsPending;
            if (hover != _hover || pending)
            {
                _hover = hover;
                UpdateVisuals(hover, pending);
                string info = hover >= 0 && _optionIndices[hover] >= 0
                    ? _permission.Settings.Options[_optionIndices[hover]].DisplayName + (_enabledSlots[hover] ? "" : " — 소환 불가")
                    : "몬스터를 선택해 주세요";
                _statusLabel.text = pending ? "소환 중…" : info + "    ·    " + GetQuotaText() + "    ·    F: 탑뷰 종료";
            }
            if (Time.frameCount <= _openedFrame || pending || !Mouse.current.leftButton.wasPressedThisFrame) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_imageRect, mouse, null, out Vector2 imagePoint);
            if (new Rect(430f, 390f, 140f, 140f).Contains(imagePoint))
            {
                CloseMenu(true);
                Status = "몬스터 선택 취소 — 지점을 다시 선택해 주세요.";
                return;
            }
            if (hover >= 0 && _enabledSlots[hover]) _spawn.RequestSummon(_permission.Settings.Options[_optionIndices[hover]].MonsterId);
        }
        private void OnDisable()
        {
            _points.SelectionResponded -= HandleSelectionResponded;
            _spawn.SummonResponded -= HandleSummonResponded;
            _spawn.QuotaChanged -= HandleQuotaChanged;
            CloseMenu(false);
        }
        private void OnGUI()
        {
            if (_hasFailure && !IsOpen && _cameraView != null && _cameraView.IsTopViewActive)
                GUI.Box(new Rect(Screen.width * 0.5f - 300f, Screen.height - 100f, 600f, 60f), "소환 실패: " + Status + "\n지점을 다시 선택해 주세요.");
        }
        private static int GetSlotAt(Vector2 point)
        {
            float radius = point.magnitude;
            if (radius < 500f * 0.438f || radius > 500f * 0.908f) return -1;
            float angle = Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg + 180f / 7f, 360f);
            return Mathf.FloorToInt(angle / (360f / 7f));
        }
        private void RefreshSlots()
        {
            for (int slot = 0; slot < SlotIds.Length; slot++)
            {
                _optionIndices[slot] = -1;
                for (int i = 0; i < _permission.Settings.Options.Count; i++)
                    if (_permission.Settings.Options[i].MonsterId == SlotIds[slot]) { _optionIndices[slot] = i; break; }
                int index = _optionIndices[slot];
                _enabledSlots[slot] = index >= 0 && _permission.Settings.Options[index].IsEnabled && (!_spawn.HasOwnQuota || _spawn.OwnRemainingCount > 0);
            }
            UpdateVisuals(-1, _spawn.IsPending);
        }
        private void UpdateVisuals(int hovered, bool pending)
        {
            _wheel.SetState(hovered, _enabledSlots, pending);
            _wheel.SetVerticesDirty();
            for (int i = 0; i < 7; i++)
            {
                bool enabled = _enabledSlots[i] && !pending;
                _icons[i].color = enabled ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
            }
            _centerIcon.sprite = hovered >= 0 ? _icons[hovered].sprite : _icons[0].sprite;
            _centerIcon.preserveAspect = true;
        }
        private void HandleSelectionResponded()
        {
            if (!_points.HasOwnSelection || !_cameraView.IsTopViewActive || _spawn.IsPending) return;
            RefreshSlots();
            IsOpen = true;
            _hasFailure = false;
            _openedFrame = Time.frameCount;
            _hover = -2;
            _canvasObject.SetActive(true);
            _pointView.IsSelectionBlocked = true;
            Status = "몬스터 종류를 클릭하면 즉시 소환합니다.";
        }
        private void HandleQuotaChanged() { if (IsOpen) { RefreshSlots(); _hover = -2; } }
        private string GetQuotaText() => _spawn.HasOwnQuota ? $"오늘 남은 소환: {_spawn.OwnRemainingCount}/{_spawn.OwnDailyLimit}" : "소환 횟수 확인 중";
        private void HandleSummonResponded(bool success)
        {
            Status = _spawn.OwnResult;
            _hasFailure = !success;
            CloseMenu(true);
            if (success && _cameraView.IsTopViewActive) _cameraView.RequestToggle();
        }
        private void CloseMenu(bool cancelSelection)
        {
            bool wasOpen = IsOpen;
            IsOpen = false;
            if (_canvasObject != null) _canvasObject.SetActive(false);
            if (_pointView != null) _pointView.IsSelectionBlocked = false;
            if (wasOpen && cancelSelection && NetworkClient.active) _points.CancelSelection();
        }
    }
}


