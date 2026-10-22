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
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        [SerializeField] private SeaSummonSpawnController _spawn;
        [SerializeField] private SeaSummonPointView _pointView;
        [SerializeField] private SeaSummonCameraView _cameraView;
        private GameObject _canvasObject;
        private RectTransform _wheelRect;
        private SeaSummonWheelGraphic _wheel;
        private Text _centerLabel;
        private Text _footer;
        private readonly Text[] _labels = new Text[7];
        private readonly bool[] _enabledSlots = new bool[7];
        private int _hover = -2;
        private int _openedFrame;
        private bool _hasFailure;
        public bool IsOpen { get; private set; }
        public string Status { get; private set; } = "지점을 선택하면 몬스터 메뉴가 열립니다.";

        private void Awake()
        {
            BuildMenu();
        }

        private void OnEnable()
        {
            _points.SelectionResponded += HandleSelectionResponded;
            _spawn.SummonResponded += HandleSummonResponded;
        }

        private void LateUpdate()
        {
            if (_cameraView != null && !_cameraView.IsTopViewActive)
            {
                _hasFailure = false;
            }
            if (!IsOpen)
            {
                return;
            }
            if (_cameraView == null || !_cameraView.IsTopViewActive || !NetworkClient.ready)
            {
                CloseMenu(true);
                return;
            }
            if (Mouse.current == null)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_wheelRect,
                Mouse.current.position.ReadValue(), null, out Vector2 local);
            int hover = GetSlotAt(local);
            bool pending = _spawn.IsPending;
            if (hover != _hover || pending)
            {
                _hover = hover;
                _wheel.SetState(hover, _enabledSlots, pending);
                _centerLabel.text = pending ? "소환 중…" : hover >= 0
                    ? _permission.Settings.Options[hover].DisplayName + (_enabledSlots[hover] ? "\n\n클릭하여 소환" : "\n\n준비 중")
                    : "몬스터 선택\n\n칸을 클릭하면\n즉시 소환됩니다";
                _footer.text = pending ? "서버 응답을 기다리는 중입니다" : "X: 지점 다시 선택    ·    Q: 탑뷰 종료";
            }
            if (Time.frameCount <= _openedFrame || pending || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }
            if (new Rect(285f, 320f, 60f, 60f).Contains(local))
            {
                CloseMenu(true);
                Status = "몬스터 선택 취소 — 지점을 다시 선택해 주세요.";
                return;
            }
            if (hover >= 0 && _enabledSlots[hover])
            {
                _spawn.RequestSummon(_permission.Settings.Options[hover].MonsterId);
            }
        }

        private void OnDisable()
        {
            _points.SelectionResponded -= HandleSelectionResponded;
            _spawn.SummonResponded -= HandleSummonResponded;
            CloseMenu(false);
        }

        private void OnGUI()
        {
            if (_hasFailure && !IsOpen && _cameraView != null && _cameraView.IsTopViewActive)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 300f, Screen.height - 100f, 600f, 60f),
                    "소환 실패: " + Status + "\n지점을 다시 선택해 주세요.");
            }
        }

        private static int GetSlotAt(Vector2 point)
        {
            float radius = point.magnitude;
            if (radius < 310f * 0.44f || radius > 310f)
            {
                return -1;
            }
            float angle = Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg + 180f / 7f, 360f);
            return Mathf.FloorToInt(angle / (360f / 7f));
        }

        private void HandleSelectionResponded()
        {
            if (!_points.HasOwnSelection || !_cameraView.IsTopViewActive || _spawn.IsPending)
            {
                return;
            }
            for (int i = 0; i < 7; i++)
            {
                var option = _permission.Settings.Options[i];
                _enabledSlots[i] = option.IsEnabled;
                _labels[i].text = option.DisplayName + (option.IsEnabled ? "" : "\n준비 중");
                _labels[i].color = option.IsEnabled ? Color.white : new Color(0.5f, 0.53f, 0.58f);
            }
            IsOpen = true;
            _hasFailure = false;
            _openedFrame = Time.frameCount;
            _hover = -2;
            _canvasObject.SetActive(true);
            _pointView.IsSelectionBlocked = true;
            Status = "몬스터 종류를 클릭하면 즉시 소환합니다.";
        }

        private void HandleSummonResponded(bool success)
        {
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

        private void BuildMenu()
        {
            _canvasObject = new GameObject("Sea Summon Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.transform.SetParent(transform, false);
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var background = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(_canvasObject.transform, false);
            RectTransform backdropRect = background.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 0.9f);
            background.GetComponent<Image>().raycastTarget = false;
            var wheelObject = new GameObject("Seven Slot Wheel", typeof(RectTransform), typeof(CanvasRenderer), typeof(SeaSummonWheelGraphic));
            wheelObject.transform.SetParent(_canvasObject.transform, false);
            _wheelRect = wheelObject.GetComponent<RectTransform>();
            _wheelRect.anchorMin = _wheelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _wheelRect.sizeDelta = new Vector2(620f, 620f);
            _wheel = wheelObject.GetComponent<SeaSummonWheelGraphic>();
            _wheel.raycastTarget = false;
            MakeText("Title", "바다 몬스터 소환", new Vector2(0, 375), new Vector2(640, 60), 34);
            MakeText("Close", "X", new Vector2(315, 350), new Vector2(60, 60), 28);
            _centerLabel = MakeText("Center", "몬스터 선택", Vector2.zero, new Vector2(240, 200), 24);
            _footer = MakeText("Footer", "", new Vector2(0, -370), new Vector2(800, 50), 22);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                _labels[i] = MakeText("Slot " + i, "", new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 226f,
                    new Vector2(155, 65), 23);
            }
            _canvasObject.SetActive(false);
        }

        private Text MakeText(string name, string content, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_wheelRect, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
