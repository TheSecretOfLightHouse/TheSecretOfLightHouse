using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // Weather and wind part of the top status bar (2026-11-01 mockup): the weather icon with its name, and the wind's
    // heading as one of eight icons with "바람 → 동". Only the heading shows, not the speed.
    [DisallowMultipleComponent]
    public sealed class WeatherWindHud : MonoBehaviour
    {
        [SerializeField] private GameObject _display;
        [SerializeField] private Image _weatherIcon;
        [Tooltip("In WeatherType order.")]
        [SerializeField] private Sprite[] _weatherSprites = new Sprite[0];
        [SerializeField] private TMP_Text _weatherText;
        [SerializeField] private Image _windIcon;
        [Tooltip("In CompassPoint order (N, NE, E, ... NW). Each points the way the wind blows to.")]
        [SerializeField] private Sprite[] _windSprites = new Sprite[0];
        [SerializeField] private TMP_Text _windText;
        [SerializeField] private string[] _weatherNames = { "맑음", "흐림", "비", "폭풍", "소용돌이" };
        [SerializeField] private string[] _compassNames = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };
        [SerializeField] private string _stillText = "바람 없음";

        private IWeatherService _weather;
        private GameClock _gameClock;
        private WeatherType _shownWeather;
        private bool _hasShownWeather;
        private CompassPoint _shownCompass;
        private bool _hadWind;
        private bool _hasShownWind;

        private void LateUpdate()
        {
            if (!ResolveServices())
            {
                SetDisplay(false);
                return;
            }

            SetDisplay(true);
            RefreshWeather(_weather.Target);
            RefreshWind(WindDisplayModel.Evaluate(_weather.Snapshot.Wind));
        }

        private bool ResolveServices()
        {
            if (_weather == null && !WorldServices.TryGet(out _weather))
            {
                return false;
            }

            if (!_gameClock && WorldServices.TryGet<IGameClock>(out IGameClock gameClock))
            {
                _gameClock = gameClock as GameClock;
            }

            return _gameClock && _gameClock.IsRunning;
        }

        private void SetDisplay(bool isVisible)
        {
            if (_display && _display.activeSelf != isVisible)
            {
                _display.SetActive(isVisible);
            }
        }

        // The editor preview uses the same path as the synchronized weather.
        public void RefreshWeather(WeatherType type)
        {
            if (_hasShownWeather && type == _shownWeather)
            {
                return;
            }

            _hasShownWeather = true;
            _shownWeather = type;
            int index = (int)type;
            _weatherText.text = GetName(_weatherNames, index);
            SetIcon(_weatherIcon, _weatherSprites, index);
        }

        public void RefreshWind(WindDisplay wind)
        {
            if (_hasShownWind && wind.HasWind == _hadWind && wind.Compass == _shownCompass)
            {
                return;
            }

            _hasShownWind = true;
            _hadWind = wind.HasWind;
            _shownCompass = wind.Compass;
            int index = (int)wind.Compass;
            _windText.text = wind.HasWind ? $"바람 → {GetName(_compassNames, index)}" : _stillText;
            SetIcon(_windIcon, _windSprites, wind.HasWind ? index : -1);
        }

        private static void SetIcon(Image icon, Sprite[] sprites, int index)
        {
            Sprite sprite = index >= 0 && index < sprites.Length ? sprites[index] : null;
            icon.sprite = sprite;
            icon.enabled = sprite;
        }

        private static string GetName(string[] names, int index)
        {
            return names != null && index >= 0 && index < names.Length ? names[index] : string.Empty;
        }
    }
}
