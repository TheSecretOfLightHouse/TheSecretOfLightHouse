using System;
using System.Collections.Generic;
using Lighthouse.Interaction.GhostShipHints.Rules;
using Lighthouse.Network.Session.View;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Interaction.GhostShipHints.View
{
    // The ghost ship's ledger window and its E prompt (2026-10-31 mockup). The board grows with the rows: one day per
    // row, from day 1 to the last day anything went missing, so two days of withdrawals show two rows. A day with
    // nothing taken in between still gets its row, with a dim day badge and "없음". No withdrawals at all shows the
    // "nothing yet" line instead. Each resource shows the workbench's icon, its name and the amount.
    // Day badges are art for days 1-5 (the match length); any later day falls back to text in the same place.
    // Esc or the X closes it, and the window reserves Esc while open so the game menu does not open on the same press.
    [DisallowMultipleComponent]
    public sealed class GhostShipLedgerScreen : MonoBehaviour
    {
        [Serializable]
        private struct ResourceIcon
        {
            public ResourceDefinition Resource;
            public Sprite Icon;
        }

        [SerializeField] private GameObject _prompt;
        [SerializeField] private GameObject _window;
        [Tooltip("Parent of the rows, under the board's printed header. Row clones go here in day order.")]
        [SerializeField] private RectTransform _rows;
        [Tooltip("Inactive day + resources row, cloned per day.")]
        [SerializeField] private GameObject _rowTemplate;
        [SerializeField] private GameObject _emptyRow;
        [SerializeField] private Button _closeButton;
        [Tooltip("Day badge art, index 0 = day 1.")]
        [SerializeField] private Sprite[] _daySprites;
        [Tooltip("Dim day badge art for a day with nothing taken, index 0 = day 1.")]
        [SerializeField] private Sprite[] _dimDaySprites;
        [Tooltip("Same icons as the workbench screen, so a resource looks the same in both places.")]
        [SerializeField] private ResourceIcon[] _resourceIcons = new ResourceIcon[0];
        [Tooltip("Tallest the window may be on the 1080 reference canvas; a longer ledger shrinks to fit.")]
        [SerializeField] private float _maxWindowHeight = 1040f;
        [SerializeField] private Font _font;
        [SerializeField] private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<GameObject> _shownRows = new List<GameObject>();
        private bool _isOpen;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (!_font)
            {
                _font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            }

            Text[] texts = GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = _font;
            }

            if (_closeButton)
            {
                _closeButton.onClick.AddListener(Close);
            }

            _rowTemplate.SetActive(false);
            _window.SetActive(false);
            _prompt.SetActive(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (_isOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnDisable()
        {
            Close();
        }

        public void SetPromptShown(bool isShown)
        {
            isShown = isShown && !_isOpen && !SessionScreen.IsGameplayOverlayOpen;

            if (_prompt.activeSelf != isShown)
            {
                _prompt.SetActive(isShown);
            }
        }

        // Entries come sorted by day then resource (GhostShipHintBuilder).
        public void Open(GhostShipHintEntry[] entries, Func<int, string> resourceName)
        {
            ClearRows();
            _emptyRow.SetActive(entries.Length == 0);
            int lastDay = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                lastDay = Mathf.Max(lastDay, entries[i].Day);
            }

            int next = 0;

            // Day 0 means the clock was missing when it was taken; it is listed first under a text label.
            for (int day = entries.Length > 0 && entries[0].Day <= 0 ? 0 : 1; day <= lastDay; day++)
            {
                int first = next;

                while (next < entries.Length && entries[next].Day <= day)
                {
                    next++;
                }

                AddRow(day, entries, first, next, resourceName);
            }

            _isOpen = true;
            _window.SetActive(true);
            _prompt.SetActive(false);
            FitWindow();
            ShowCursor(true);
            SessionScreen.SetGameplayOverlayOpen(this, true);
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _window.SetActive(false);
            ShowCursor(false);
            SessionScreen.SetGameplayOverlayOpen(this, false);
        }

        private void AddRow(int day, GhostShipHintEntry[] entries, int first, int end, Func<int, string> resourceName)
        {
            GameObject row = Instantiate(_rowTemplate, _rows);
            row.SetActive(true);
            Transform rowTransform = row.transform;
            bool isNothing = first == end;
            // The board prints the line under the column titles, so only later rows draw their own divider.
            rowTransform.Find("Line").gameObject.SetActive(_shownRows.Count > 0);

            Transform dayCell = rowTransform.Find("DayCell");
            Image dayArt = dayCell.Find("Day").GetComponent<Image>();
            Text dayText = dayCell.Find("DayText").GetComponent<Text>();
            Sprite[] daySprites = isNothing ? _dimDaySprites : _daySprites;
            bool hasArt = day >= 1 && day <= daySprites.Length && daySprites[day - 1];
            dayArt.enabled = hasArt;
            dayText.enabled = !hasArt;

            if (hasArt)
            {
                dayArt.sprite = daySprites[day - 1];
            }
            else
            {
                dayText.text = day > 0 ? $"{day}일차" : "날짜 미상";
            }

            rowTransform.Find("Nothing").gameObject.SetActive(isNothing);
            Transform items = rowTransform.Find("Items");
            items.gameObject.SetActive(!isNothing);
            GameObject itemTemplate = items.Find("ItemTemplate").gameObject;
            itemTemplate.SetActive(false);

            for (int i = first; i < end; i++)
            {
                GameObject item = Instantiate(itemTemplate, items);
                item.SetActive(true);
                Image icon = item.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = GetIcon(entries[i].ResourceId);
                icon.enabled = icon.sprite;
                item.transform.Find("Label").GetComponent<Text>().text = $"{resourceName(entries[i].ResourceId)} × {entries[i].Amount}";
            }

            _shownRows.Add(row);
        }

        private Sprite GetIcon(int resourceId)
        {
            for (int i = 0; i < _resourceIcons.Length; i++)
            {
                ResourceDefinition resource = _resourceIcons[i].Resource;

                if (resource && resource.Id == resourceId)
                {
                    return _resourceIcons[i].Icon ? _resourceIcons[i].Icon : resource.Icon;
                }
            }

            return null;
        }

        // Five days with many resources each can outgrow the screen; shrink the whole board instead of cutting rows.
        private void FitWindow()
        {
            RectTransform window = (RectTransform)_window.transform;
            window.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            float height = window.rect.height;
            float scale = height > _maxWindowHeight ? _maxWindowHeight / height : 1f;
            window.localScale = new Vector3(scale, scale, 1f);
        }

        // Gameplay hides the cursor; the X needs it while the ledger is open.
        private void ShowCursor(bool isShown)
        {
            if (isShown)
            {
                if (!_hasSavedCursor)
                {
                    _hasSavedCursor = true;
                    _savedLockMode = Cursor.lockState;
                    _savedCursorVisible = Cursor.visible;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (_hasSavedCursor)
            {
                _hasSavedCursor = false;
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _shownRows.Count; i++)
            {
                // Destroy lands at the end of the frame; hiding first keeps the layout from counting the old rows.
                _shownRows[i].SetActive(false);
                Destroy(_shownRows[i]);
            }

            _shownRows.Clear();
        }
    }
}
