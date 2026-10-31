using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    [DisallowMultipleComponent]
    public sealed class WeatherWindHud : MonoBehaviour
    {
        private const float SpeedPrecision = 10f;

        [SerializeField] private GameObject _display;
        [SerializeField] private Image _weatherIcon;
        [SerializeField] private Sprite[] _weatherSprites = new Sprite[0];
        [SerializeField] private Text _weatherText;
        [SerializeField] private RectTransform _windArrow;
        [SerializeField] private Text _windSpeedText;
        [SerializeField] private Text _windDirectionText;
        [SerializeField] private Text _windStrengthText;
        [SerializeField] private string[] _weatherNames = { "맑음", "흐림", "비", "폭풍", "소용돌이" };
        [SerializeField] private string[] _compassNames = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };
        [SerializeField] private string[] _strengthNames = { "무풍", "약풍", "보통", "강풍", "폭풍" };

        private IWeatherService _weather;
        private GameClock _gameClock;
        private WeatherType _shownWeather;
        private bool _hasShownWeather;
        private int _shownSpeed = -1;
        private int _shownAngle = -1;
        private CompassPoint _shownCompass;
        private WindStrength _shownStrength;
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

        private void RefreshWeather(WeatherType type)
        {
            if (_hasShownWeather && type == _shownWeather)
            {
                return;
            }

            _hasShownWeather = true;
            _shownWeather = type;
            int index = (int)type;

            if (_weatherText)
            {
                _weatherText.text = GetName(_weatherNames, index);
            }

            if (_weatherIcon)
            {
                Sprite sprite = index < _weatherSprites.Length ? _weatherSprites[index] : null;
                _weatherIcon.sprite = sprite;
                _weatherIcon.enabled = sprite;
            }
        }

        private void RefreshWind(WindDisplay wind)
        {
            int speed = Mathf.RoundToInt(wind.Speed * SpeedPrecision);
            int angle = Mathf.RoundToInt(wind.AngleDegrees);

            if (_hasShownWind && speed == _shownSpeed && angle == _shownAngle
                && wind.Compass == _shownCompass && wind.Strength == _shownStrength)
            {
                return;
            }

            _hasShownWind = true;
            _shownSpeed = speed;
            _shownAngle = angle;
            _shownCompass = wind.Compass;
            _shownStrength = wind.Strength;

            if (_windArrow)
            {
                _windArrow.gameObject.SetActive(wind.HasWind);
                _windArrow.localRotation = Quaternion.Euler(0f, 0f, -wind.AngleDegrees);
            }

            if (_windSpeedText)
            {
                _windSpeedText.text = $"{wind.Speed:0.0} m/s";
            }

            if (_windDirectionText)
            {
                _windDirectionText.text = wind.HasWind ? GetName(_compassNames, (int)wind.Compass) : string.Empty;
            }

            if (_windStrengthText)
            {
                _windStrengthText.text = GetName(_strengthNames, (int)wind.Strength);
            }
        }

        private static string GetName(string[] names, int index)
        {
            return names != null && index >= 0 && index < names.Length ? names[index] : string.Empty;
        }
    }
}
