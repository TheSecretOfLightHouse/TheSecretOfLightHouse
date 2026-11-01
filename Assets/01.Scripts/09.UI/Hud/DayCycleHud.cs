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
    // Day part of the top status bar (2026-11-01 mockup): the day count, a sun or moon with 낮/밤, and the time left in
    // the current part with its fill. The weather and wind parts of the same bar are drawn by WeatherWindHud.
    [DisallowMultipleComponent]
    public sealed class DayCycleHud : MonoBehaviour
    {
        private const float MatchLookupInterval = 1f;
        private const int SecondsPerMinute = 60;

        [Tooltip("The whole bar; hidden while the clock is not running and during the final battle.")]
        [SerializeField] private GameObject _display;
        [SerializeField] private Image _phaseIcon;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _dayText;
        [SerializeField] private TMP_Text _phaseText;
        [SerializeField] private TMP_Text _remainText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Sprite _dayIcon;
        [SerializeField] private Sprite _nightIcon;
        [SerializeField] private float _fillMinWidth = 10f;
        [SerializeField] private float _fillMaxWidth = 90f;

        private GameClock _gameClock;
        private IClock _clock;
        private TowerMatchNet _match;
        private float _nextMatchLookup;
        private int _shownDay = -1;
        private bool _isNight;
        private int _shownSeconds = -1;

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

            // The final battle band takes the top centre, so the bar steps aside.
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
            double partSeconds = isNight ? config.NightSeconds : config.DaySeconds;
            double inPart = isNight ? inCycle - config.DaySeconds : inCycle;
            int day = (int)Math.Floor(elapsed / config.CycleSeconds) + 1;
            Render(day, isNight, (float)(inPart / partSeconds), partSeconds - inPart);
        }

        // The editor preview uses the same layout path as the synchronized clock.
        public void Render(int day, bool isNight, float progress, double secondsLeft)
        {
            if (day != _shownDay || isNight != _isNight)
            {
                _shownDay = day;
                _isNight = isNight;
                _dayText.text = $"{day}일차";
                _phaseText.text = isNight ? "밤" : "낮";
                _remainText.text = isNight ? "밤 남음" : "낮 남음";
                _phaseIcon.sprite = isNight ? _nightIcon : _dayIcon;
            }

            // Rounded up, so the count reads 00:00 only as the part ends.
            int seconds = Math.Max(0, (int)Math.Ceiling(secondsLeft));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timeText.text = $"{seconds / SecondsPerMinute:00}:{seconds % SecondsPerMinute:00}";
            }

            // The sliced pill keeps both rounded ends, so it shrinks in width as the part runs out instead of being
            // clipped.
            float left = 1f - Mathf.Clamp01(progress);
            _fill.enabled = left > 0f;
            RectTransform fill = _fill.rectTransform;
            fill.sizeDelta = new Vector2(Mathf.Lerp(_fillMinWidth, _fillMaxWidth, left), fill.sizeDelta.y);
        }
    }
}
