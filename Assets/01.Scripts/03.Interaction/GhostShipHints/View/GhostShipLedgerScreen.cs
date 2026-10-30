using System;
using System.Collections.Generic;
using System.Text;
using Lighthouse.Interaction.GhostShipHints.Rules;
using Lighthouse.Network.Session.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Interaction.GhostShipHints.View
{
    // The ghost ship's ledger window and its E prompt (2026-10-30 mockup). The board grows with the rows: one divider and
    // one day per row, from day 1 to the last day anything went missing, so two days of withdrawals show two rows. A day
    // with nothing taken in between still gets its row. No withdrawals at all shows the "nothing yet" line instead.
    // Day labels are art for days 1-5 (the match length); any later day falls back to text in the same place.
    // Esc closes it, and the window reserves Esc while open so the game menu does not open on the same press.
    [DisallowMultipleComponent]
    public sealed class GhostShipLedgerScreen : MonoBehaviour
    {
        private const string NothingTaken = "없음";

        [SerializeField] private GameObject _prompt;
        [SerializeField] private GameObject _window;
        [Tooltip("Parent of the rows, after the header. Row clones go here in day order.")]
        [SerializeField] private RectTransform _rows;
        [Tooltip("Inactive divider + day + resources row, cloned per day.")]
        [SerializeField] private GameObject _rowTemplate;
        [SerializeField] private GameObject _emptyRow;
        [Tooltip("Day label art, index 0 = day 1.")]
        [SerializeField] private Sprite[] _daySprites;
        [SerializeField, Range(0f, 1f)] private float _nothingAlpha = 0.55f;
        [SerializeField] private Font _font;
        [SerializeField] private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<GameObject> _shownRows = new List<GameObject>();
        private readonly StringBuilder _line = new StringBuilder();
        private bool _isOpen;

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
                _line.Clear();

                while (next < entries.Length && entries[next].Day <= day)
                {
                    if (_line.Length > 0)
                    {
                        _line.Append(", ");
                    }

                    _line.Append(resourceName(entries[next].ResourceId)).Append(' ').Append(entries[next].Amount);
                    next++;
                }

                AddRow(day, _line.Length > 0 ? _line.ToString() : null);
            }

            _isOpen = true;
            _window.SetActive(true);
            _prompt.SetActive(false);
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
            SessionScreen.SetGameplayOverlayOpen(this, false);
        }

        private void AddRow(int day, string resources)
        {
            GameObject row = Instantiate(_rowTemplate, _rows);
            row.SetActive(true);
            Image dayArt = row.transform.Find("Day").GetComponent<Image>();
            Text dayText = row.transform.Find("DayText").GetComponent<Text>();
            bool hasArt = day >= 1 && day <= _daySprites.Length && _daySprites[day - 1];
            dayArt.enabled = hasArt;
            dayText.enabled = !hasArt;

            if (hasArt)
            {
                dayArt.sprite = _daySprites[day - 1];
            }
            else
            {
                dayText.text = day > 0 ? $"{day}일차:" : "날짜 미상:";
            }

            Text resourcesText = row.transform.Find("Resources").GetComponent<Text>();
            resourcesText.text = resources ?? NothingTaken;

            // A day with nothing taken reads dimmer than the days that lost something.
            if (resources == null)
            {
                Color color = resourcesText.color;
                color.a *= _nothingAlpha;
                resourcesText.color = color;
            }

            _shownRows.Add(row);
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
