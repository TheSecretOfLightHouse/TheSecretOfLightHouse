using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // Short tweens for the workbench screen (deposit flight, gain "+N", punches, shakes, blinks, the ready glow, and
    // for a craft the ingredients gathering into a burst of light that leaves the module icon behind).
    // The screen decides when; this only draws. Everything it shows is built once and reused, so ticking allocates
    // nothing. Unscaled time, like the other screen feedback, so a slowed match still answers at once.
    internal sealed class WorkbenchScreenEffects
    {
        private const int PunchCapacity = 8;
        private const int FlashCapacity = 4;
        private const int ShakeCapacity = 2;
        private const int FlightCapacity = 8;
        private const int FloaterCapacity = 4;

        [Serializable]
        public sealed class Settings
        {
            public float FlightSeconds = 0.45f;
            public float FlightArc = 70f;
            public float FlightIconSize = 56f;
            public float PunchSeconds = 0.28f;
            public float PunchStrength = 0.25f;
            public float BadgePunchStrength = 0.4f;
            public float GlowPeriod = 1.1f;
            [Range(0f, 1f)]
            public float GlowAlpha = 0.5f;
            public float GlowOutset = 6f;
            public Color GlowColor = new Color(1f, 0.86f, 0.45f);
            public float ShakeSeconds = 0.35f;
            public float ShakeAmplitude = 10f;
            public float ShakeCycles = 3.5f;
            public float FlashSeconds = 0.6f;
            public int FlashBlinks = 3;
            public Color FlashColor = new Color(1f, 0.25f, 0.2f);
            public float FloaterSeconds = 0.9f;
            public float FloaterRise = 48f;
            public int FloaterFontSize = 26;
            public Color FloaterColor = new Color(1f, 0.86f, 0.45f);
            public float FillSpeed = 1.6f;
            [Tooltip("How long after a successful deposit an increase on that ingredient still counts as this player's own.")]
            public float OwnDepositSeconds = 1.5f;
            public float GatherSeconds = 0.4f;
            public float BurstSeconds = 0.55f;
            [Tooltip("How long the new module icon stays in the middle before it flies to the stock.")]
            public float BurstHoldSeconds = 0.45f;
            public float BurstLightSize = 260f;
            public Color BurstColor = new Color(1f, 0.9f, 0.62f);
            public float ModuleIconSize = 96f;
            public Color HighlightColor = new Color(1f, 0.86f, 0.45f);
            [Tooltip("How long a craft result and the finish call may wait for each other, and a stock count may lag behind.")]
            public float CraftWaitSeconds = 1.5f;
        }

        private struct Punch
        {
            public RectTransform Target;
            public Vector3 BaseScale;
            public float Strength;
            public float Elapsed;
        }

        private struct Flash
        {
            public Graphic Target;
            public Color BaseColor;
            public Color Color;
            public int Blinks;
            public float Elapsed;
        }

        private struct Shake
        {
            public RectTransform Target;
            public Vector2 BasePosition;
            public float Elapsed;
        }

        private struct Flight
        {
            public Image Image;
            public Vector3 From;
            public RectTransform To;
            public RectTransform LandCount;
            public WorkbenchEffectCue LandCue;
            public float Elapsed;
            public bool IsActive;
            // Straight in and shrinking away, with nothing on landing (ingredients gathering into a craft).
            public bool IsGather;
        }

        private struct Floater
        {
            public Text Text;
            public Vector3 From;
            public float Elapsed;
            public bool IsActive;
        }

        private readonly Settings _settings;
        private readonly RectTransform _layer;
        private readonly Action<WorkbenchEffectCue> _cued;
        private readonly Punch[] _punches = new Punch[PunchCapacity];
        private readonly Flash[] _flashes = new Flash[FlashCapacity];
        private readonly Shake[] _shakes = new Shake[ShakeCapacity];
        private readonly Flight[] _flights = new Flight[FlightCapacity];
        private readonly Floater[] _floaters = new Floater[FloaterCapacity];
        private readonly Image _glow;
        private readonly Image _burstLight;
        private readonly Image _burstIcon;
        private int _punchCount;
        private int _flashCount;
        private int _shakeCount;
        private bool _isGlowing;
        private float _glowElapsed;
        private bool _isBursting;
        private float _burstElapsed;

        // layer is where flights and floaters are drawn (the window); glowHost is the craft button.
        public WorkbenchScreenEffects(Settings settings, RectTransform layer, Font font, Button glowHost, Action<WorkbenchEffectCue> cued)
        {
            _settings = settings;
            _layer = layer;
            _cued = cued;

            for (int i = 0; i < FlightCapacity; i++)
            {
                GameObject flight = new GameObject("DepositFlight", typeof(RectTransform), typeof(Image));
                flight.transform.SetParent(layer, false);
                Image image = flight.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(settings.FlightIconSize, settings.FlightIconSize);
                flight.SetActive(false);
                _flights[i].Image = image;
            }

            for (int i = 0; i < FloaterCapacity; i++)
            {
                GameObject floater = new GameObject("DepositGain", typeof(RectTransform), typeof(Text), typeof(Outline));
                floater.transform.SetParent(layer, false);
                Text text = floater.GetComponent<Text>();
                text.font = font;
                text.fontSize = settings.FloaterFontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(120f, 40f);
                Outline outline = floater.GetComponent<Outline>();
                outline.effectColor = new Color(0.12f, 0.09f, 0.05f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                floater.SetActive(false);
                _floaters[i].Text = text;
            }

            _burstLight = CreateLayerImage("CraftBurst", BuildBurstSprite(), settings.BurstLightSize);
            _burstIcon = CreateLayerImage("CraftModule", null, settings.ModuleIconSize);

            if (glowHost && glowHost.image)
            {
                GameObject glow = new GameObject("ReadyGlow", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)glow.transform;
                rect.SetParent(glowHost.transform, false);
                // First child: drawn over the button art but under its caption.
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-settings.GlowOutset, -settings.GlowOutset);
                rect.offsetMax = new Vector2(settings.GlowOutset, settings.GlowOutset);
                _glow = glow.GetComponent<Image>();
                _glow.sprite = glowHost.image.sprite;
                _glow.type = glowHost.image.type;
                _glow.raycastTarget = false;
                SetGlowAlpha(0f);
            }
        }

        public void Tick(float deltaTime)
        {
            TickPunches(deltaTime);
            TickFlashes(deltaTime);
            TickShakes(deltaTime);
            TickFlights(deltaTime);
            TickFloaters(deltaTime);
            TickGlow(deltaTime);
            TickBurst(deltaTime);
        }

        // Where the module icon of the last burst sits, for the flight that carries it away.
        public RectTransform BurstIconRect => _burstIcon.rectTransform;

        // Puts every touched graphic back as it was, for a closed screen to reopen at rest.
        public void StopAll()
        {
            for (int i = 0; i < _punchCount; i++)
            {
                if (_punches[i].Target)
                {
                    _punches[i].Target.localScale = _punches[i].BaseScale;
                }
            }

            for (int i = 0; i < _flashCount; i++)
            {
                if (_flashes[i].Target)
                {
                    _flashes[i].Target.color = _flashes[i].BaseColor;
                }
            }

            for (int i = 0; i < _shakeCount; i++)
            {
                if (_shakes[i].Target)
                {
                    _shakes[i].Target.anchoredPosition = _shakes[i].BasePosition;
                }
            }

            for (int i = 0; i < FlightCapacity; i++)
            {
                _flights[i].IsActive = false;
                _flights[i].Image.gameObject.SetActive(false);
            }

            for (int i = 0; i < FloaterCapacity; i++)
            {
                _floaters[i].IsActive = false;
                _floaters[i].Text.gameObject.SetActive(false);
            }

            _punchCount = 0;
            _flashCount = 0;
            _shakeCount = 0;
            SetReadyGlow(false);
            _isBursting = false;
            _burstLight.gameObject.SetActive(false);
            HideBurstIcon();
        }

        public void PlayPunch(RectTransform target, float strength)
        {
            if (!target || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < _punchCount; i++)
            {
                if (_punches[i].Target == target)
                {
                    _punches[i].Elapsed = 0f;
                    _punches[i].Strength = strength;
                    return;
                }
            }

            if (_punchCount == PunchCapacity)
            {
                return;
            }

            _punches[_punchCount++] = new Punch { Target = target, BaseScale = target.localScale, Strength = strength };
        }

        // Blinks toward the flash colour and settles back on the graphic's colour at the moment it started.
        public void PlayFlash(Graphic target)
        {
            PlayFlash(target, _settings.FlashColor, _settings.FlashBlinks);
        }

        public void PlayFlash(Graphic target, Color color, int blinks)
        {
            if (!target || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < _flashCount; i++)
            {
                if (_flashes[i].Target == target)
                {
                    _flashes[i].Color = color;
                    _flashes[i].Blinks = blinks;
                    _flashes[i].Elapsed = 0f;
                    return;
                }
            }

            if (_flashCount == FlashCapacity)
            {
                return;
            }

            _flashes[_flashCount++] = new Flash { Target = target, BaseColor = target.color, Color = color, Blinks = blinks };
        }

        // For a graphic whose colour the screen is about to change: the old blink must not paint it back later.
        public void CancelFlash(Graphic target)
        {
            for (int i = 0; i < _flashCount; i++)
            {
                if (_flashes[i].Target == target)
                {
                    target.color = _flashes[i].BaseColor;
                    _flashes[i] = _flashes[--_flashCount];
                    return;
                }
            }
        }

        public void PlayShake(RectTransform target)
        {
            if (!target || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < _shakeCount; i++)
            {
                if (_shakes[i].Target == target)
                {
                    _shakes[i].Elapsed = 0f;
                    return;
                }
            }

            if (_shakeCount == ShakeCapacity)
            {
                return;
            }

            _shakes[_shakeCount++] = new Shake { Target = target, BasePosition = target.anchoredPosition };
        }

        // An icon arcs from one place to another; on landing the target icon and its count punch.
        public void PlayFlight(Sprite icon, RectTransform from, RectTransform to, RectTransform landCount)
        {
            StartFlight(icon, from, to, landCount, WorkbenchEffectCue.DepositLanded, false);
        }

        public void PlayFlight(Sprite icon, RectTransform from, RectTransform to, RectTransform landCount, WorkbenchEffectCue landCue)
        {
            StartFlight(icon, from, to, landCount, landCue, false);
        }

        // From a world point taken earlier (see CenterOf), for a source that may be gone by the time the flight starts.
        public void PlayFlight(Sprite icon, Vector3 from, RectTransform to, RectTransform landCount, WorkbenchEffectCue landCue)
        {
            StartFlight(icon, from, to, landCount, landCue, false);
        }

        // A copy of the icon is pulled into the target and vanishes there.
        public void PlayGather(Sprite icon, RectTransform from, RectTransform to)
        {
            StartFlight(icon, from, to, null, WorkbenchEffectCue.DepositLanded, true);
        }

        public static Vector3 CenterOf(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        // Light swells and fades at the target while the module icon pops in and stays until HideBurstIcon.
        public void PlayBurst(Sprite moduleIcon, RectTransform at)
        {
            if (!at)
            {
                return;
            }

            Vector3 center = at.TransformPoint(at.rect.center);
            _burstLight.rectTransform.position = center;
            _burstLight.transform.SetAsLastSibling();
            _burstLight.gameObject.SetActive(true);
            _burstIcon.sprite = moduleIcon;
            _burstIcon.rectTransform.position = center;
            _burstIcon.transform.SetAsLastSibling();
            _burstIcon.gameObject.SetActive(moduleIcon);
            _burstElapsed = 0f;
            _isBursting = true;
            TickBurst(0f);
        }

        public void HideBurstIcon()
        {
            _burstIcon.gameObject.SetActive(false);
        }

        private void StartFlight(Sprite icon, RectTransform from, RectTransform to, RectTransform landCount, WorkbenchEffectCue landCue,
            bool isGather)
        {
            if (from)
            {
                StartFlight(icon, CenterOf(from), to, landCount, landCue, isGather);
            }
        }

        private void StartFlight(Sprite icon, Vector3 from, RectTransform to, RectTransform landCount, WorkbenchEffectCue landCue,
            bool isGather)
        {
            if (!icon || !to)
            {
                return;
            }

            for (int i = 0; i < FlightCapacity; i++)
            {
                if (_flights[i].IsActive)
                {
                    continue;
                }

                Image image = _flights[i].Image;
                image.sprite = icon;
                image.rectTransform.position = from;
                image.rectTransform.localScale = Vector3.one;
                image.transform.SetAsLastSibling();
                image.gameObject.SetActive(true);
                _flights[i].From = image.rectTransform.position;
                _flights[i].To = to;
                _flights[i].LandCount = landCount;
                _flights[i].LandCue = landCue;
                _flights[i].IsGather = isGather;
                _flights[i].Elapsed = 0f;
                _flights[i].IsActive = true;
                return;
            }
        }

        public void PlayGain(RectTransform anchor, int amount)
        {
            if (!anchor || !anchor.gameObject.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < FloaterCapacity; i++)
            {
                if (_floaters[i].IsActive)
                {
                    continue;
                }

                Text text = _floaters[i].Text;
                text.text = "+" + amount;
                text.color = _settings.FloaterColor;
                text.rectTransform.position = anchor.TransformPoint(anchor.rect.center);
                text.transform.SetAsLastSibling();
                text.gameObject.SetActive(true);
                _floaters[i].From = text.rectTransform.position;
                _floaters[i].Elapsed = 0f;
                _floaters[i].IsActive = true;
                return;
            }
        }

        public void SetReadyGlow(bool isGlowing)
        {
            if (isGlowing == _isGlowing)
            {
                return;
            }

            _isGlowing = isGlowing;
            _glowElapsed = 0f;

            if (!isGlowing)
            {
                SetGlowAlpha(0f);
            }
        }

        private void TickPunches(float deltaTime)
        {
            float duration = Mathf.Max(0.01f, _settings.PunchSeconds);

            for (int i = _punchCount - 1; i >= 0; i--)
            {
                ref Punch punch = ref _punches[i];
                punch.Elapsed += deltaTime;

                if (!punch.Target)
                {
                    _punches[i] = _punches[--_punchCount];
                    continue;
                }

                float t = punch.Elapsed / duration;

                if (t >= 1f)
                {
                    punch.Target.localScale = punch.BaseScale;
                    _punches[i] = _punches[--_punchCount];
                    continue;
                }

                // Up fast, back down with a small dip under rest.
                float bump = Mathf.Sin(t * Mathf.PI) * (1f - t) + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * t;
                punch.Target.localScale = punch.BaseScale * (1f + punch.Strength * bump);
            }
        }

        private void TickFlashes(float deltaTime)
        {
            float duration = Mathf.Max(0.01f, _settings.FlashSeconds);

            for (int i = _flashCount - 1; i >= 0; i--)
            {
                ref Flash flash = ref _flashes[i];
                flash.Elapsed += deltaTime;
                int blinks = Mathf.Max(1, flash.Blinks);

                if (!flash.Target)
                {
                    _flashes[i] = _flashes[--_flashCount];
                    continue;
                }

                float t = flash.Elapsed / duration;

                if (t >= 1f)
                {
                    flash.Target.color = flash.BaseColor;
                    _flashes[i] = _flashes[--_flashCount];
                    continue;
                }

                // Snaps to the flash colour at the start of each blink and fades back within it.
                float blink = 1f - Mathf.Repeat(t * blinks, 1f);
                Color color = Color.Lerp(flash.BaseColor, flash.Color, blink);
                color.a = flash.BaseColor.a;
                flash.Target.color = color;
            }
        }

        private void TickShakes(float deltaTime)
        {
            float duration = Mathf.Max(0.01f, _settings.ShakeSeconds);

            for (int i = _shakeCount - 1; i >= 0; i--)
            {
                ref Shake shake = ref _shakes[i];
                shake.Elapsed += deltaTime;

                if (!shake.Target)
                {
                    _shakes[i] = _shakes[--_shakeCount];
                    continue;
                }

                float t = shake.Elapsed / duration;

                if (t >= 1f)
                {
                    shake.Target.anchoredPosition = shake.BasePosition;
                    _shakes[i] = _shakes[--_shakeCount];
                    continue;
                }

                float offset = Mathf.Sin(t * _settings.ShakeCycles * Mathf.PI * 2f) * _settings.ShakeAmplitude * (1f - t);
                shake.Target.anchoredPosition = shake.BasePosition + new Vector2(offset, 0f);
            }
        }

        private void TickFlights(float deltaTime)
        {
            float duration = Mathf.Max(0.01f, _settings.FlightSeconds);
            // Overlay canvases put world units in screen pixels; the arc is authored in canvas units.
            float arc = _settings.FlightArc * _layer.lossyScale.y;

            for (int i = 0; i < FlightCapacity; i++)
            {
                ref Flight flight = ref _flights[i];

                if (!flight.IsActive)
                {
                    continue;
                }

                flight.Elapsed += deltaTime;
                float t = flight.Elapsed / duration;

                if (t >= 1f || !flight.To || !flight.To.gameObject.activeInHierarchy)
                {
                    flight.IsActive = false;
                    flight.Image.gameObject.SetActive(false);

                    if (t >= 1f && !flight.IsGather)
                    {
                        PlayPunch(flight.To, _settings.PunchStrength);
                        PlayPunch(flight.LandCount, _settings.PunchStrength);
                        _cued?.Invoke(flight.LandCue);
                    }

                    continue;
                }

                Vector3 to = flight.To.TransformPoint(flight.To.rect.center);

                if (flight.IsGather)
                {
                    // Speeds up as it is drawn in.
                    flight.Image.rectTransform.position = Vector3.LerpUnclamped(flight.From, to, t * t);
                    flight.Image.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t * t);
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);
                Vector3 position = Vector3.LerpUnclamped(flight.From, to, eased);
                position.y += Mathf.Sin(t * Mathf.PI) * arc;
                flight.Image.rectTransform.position = position;
                // Shrinks a little into the slot.
                flight.Image.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.8f, t);
            }
        }

        private void TickFloaters(float deltaTime)
        {
            float duration = Mathf.Max(0.01f, _settings.FloaterSeconds);
            float rise = _settings.FloaterRise * _layer.lossyScale.y;

            for (int i = 0; i < FloaterCapacity; i++)
            {
                ref Floater floater = ref _floaters[i];

                if (!floater.IsActive)
                {
                    continue;
                }

                floater.Elapsed += deltaTime;
                float t = floater.Elapsed / duration;

                if (t >= 1f)
                {
                    floater.IsActive = false;
                    floater.Text.gameObject.SetActive(false);
                    continue;
                }

                floater.Text.rectTransform.position = floater.From + new Vector3(0f, rise * (1f - (1f - t) * (1f - t)), 0f);
                Color color = _settings.FloaterColor;
                // Holds full for the first half, then fades.
                color.a *= Mathf.Clamp01(2f - t * 2f);
                floater.Text.color = color;
            }
        }

        private void TickGlow(float deltaTime)
        {
            if (!_isGlowing || !_glow)
            {
                return;
            }

            _glowElapsed += deltaTime;
            float phase = _glowElapsed / Mathf.Max(0.05f, _settings.GlowPeriod);
            SetGlowAlpha(_settings.GlowAlpha * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f)));
        }

        private void TickBurst(float deltaTime)
        {
            if (!_isBursting)
            {
                return;
            }

            _burstElapsed += deltaTime;
            float t = _burstElapsed / Mathf.Max(0.01f, _settings.BurstSeconds);

            // The icon grows past full size in the first quarter, settles in the second and stays until HideBurstIcon.
            float iconScale = t < 0.25f ? Mathf.Sin(t * 4f * Mathf.PI * 0.5f) * 1.2f : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.25f) * 4f));
            _burstIcon.rectTransform.localScale = Vector3.one * iconScale;

            if (t >= 1f)
            {
                _isBursting = false;
                _burstLight.gameObject.SetActive(false);
                return;
            }

            float eased = 1f - (1f - t) * (1f - t);
            _burstLight.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, eased);
            Color color = _settings.BurstColor;
            color.a = 1f - t;
            _burstLight.color = color;
        }

        private Image CreateLayerImage(string name, Sprite sprite, float size)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(_layer, false);
            Image image = item.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            item.SetActive(false);
            return image;
        }

        // Built once so the screen needs no image asset: a soft round glow, full in the middle and gone at the rim.
        private static Sprite BuildBurstSprite()
        {
            const int Size = 64;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float falloff = 1f - Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(falloff * falloff * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
        }

        private void SetGlowAlpha(float alpha)
        {
            if (!_glow)
            {
                return;
            }

            Color color = _settings.GlowColor;
            color.a = alpha;
            _glow.color = color;
            _glow.enabled = alpha > 0.001f;
        }
    }
}
