using System;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    [DisallowMultipleComponent]
    public sealed class DayCycleHud : MonoBehaviour
    {
        [SerializeField] private GameObject _display;
        [SerializeField] private Image _badge;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _dayText;
        [SerializeField] private TMP_Text _phaseText;
        [SerializeField] private Sprite _dayBadge;
        [SerializeField] private Sprite _nightBadge;
        [SerializeField] private Sprite _dayFill;
        [SerializeField] private Sprite _nightFill;
        [SerializeField] private float _fillMinWidth = 20f;
        [SerializeField] private float _fillMaxWidth = 146f;

        private const float MatchLookupInterval = 1f;

        private GameClock _gameClock;
        private IClock _clock;
        private TowerMatchNet _match;
        private float _nextMatchLookup;
        private int _shownDay = -1;
        private bool _isNight;

        private void LateUpdate()
        {
            if (!_gameClock)
            {
                if (WorldServices.TryGet<IGameClock>(out IGameClock gameClock))
                {
                    _gameClock = gameClock as GameClock;
                }

                WorldServices.TryGet<IClock>(out _clock);
            }

            // The lighthouse is spawned with the match, so it is looked up lazily rather than wired in the prefab.
            if (!_match && Time.unscaledTime >= _nextMatchLookup)
            {
                _nextMatchLookup = Time.unscaledTime + MatchLookupInterval;
                _match = FindAnyObjectByType<TowerMatchNet>();
            }

            // The final battle band takes the top centre, so the day display steps aside.
            bool isFinalBattle = _match && _match.Phase == TowerPhase.FinalBattle;
            bool isReady = !isFinalBattle && _gameClock && _gameClock.IsRunning && _gameClock.Config && _clock != null;
            if (_display.activeSelf != isReady)
            {
                _display.SetActive(isReady);
            }

            if (!isReady)
            {
                return;
            }

            DayCycleConfig config = _gameClock.Config;
            if (config.DaySeconds <= 0f || config.NightSeconds <= 0f)
            {
                _display.SetActive(false);
                return;
            }

            double elapsed = Math.Max(0.0, _clock.Now - _gameClock.GameStartTime);
            double inCycle = elapsed % config.CycleSeconds;
            bool isNight = inCycle >= config.DaySeconds;
            float progress = isNight
                ? (float)((inCycle - config.DaySeconds) / config.NightSeconds)
                : (float)(inCycle / config.DaySeconds);
            int day = (int)Math.Floor(elapsed / config.CycleSeconds) + 1;
            Render(day, isNight, progress);
        }

        // The editor preview uses the same layout path as the synchronized clock.
        public void Render(int day, bool isNight, float progress)
        {
            if (day != _shownDay || isNight != _isNight)
            {
                _shownDay = day;
                _isNight = isNight;
                _dayText.text = $"{day}일차";
                _phaseText.text = isNight ? "밤" : "낮";
                _badge.sprite = isNight ? _nightBadge : _dayBadge;
                _fill.sprite = isNight ? _nightFill : _dayFill;
            }

            // The sliced pill keeps both rounded ends, so it grows in width instead of being clipped.
            progress = Mathf.Clamp01(progress);
            _fill.enabled = progress > 0f;
            RectTransform fill = _fill.rectTransform;
            fill.sizeDelta = new Vector2(Mathf.Lerp(_fillMinWidth, _fillMaxWidth, progress), fill.sizeDelta.y);
        }
    }
}
