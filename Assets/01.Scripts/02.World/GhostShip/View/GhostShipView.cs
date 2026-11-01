using Lighthouse.Lantern.View;
using Lighthouse.Systems.Audio;
using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.View
{
    // Shows the ship only while it is out. Its fog bank stays off through the diagonal rise and slowly fades in once the
    // ship levels out and starts roaming (2026-10-31); it fades out over a second the moment the ship starts sinking,
    // rather than lingering on the water while the particles live out their lifetime.
    // The sea is see-through, so under the surface the lamp dims with depth and the ship is hidden once fully under;
    // otherwise a rising or sinking ship glows from deep water far away.
    [DisallowMultipleComponent]
    public sealed class GhostShipView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GhostShipNet _ship;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Light _lamp;
        [Tooltip("Deck lanterns; they dim with depth like the lamp.")]
        [SerializeField] private LanternFlameFlicker[] _lanterns;
        [SerializeField] private ParticleSystem[] _fog;
        [Tooltip("Depth below the sea at which the lamp is fully off.")]
        [SerializeField, Min(0.1f)] private float _lampFadeDepth = 6f;
        [Tooltip("Depth below the sea at which the whole ship is hidden (the hull and masts are under by then).")]
        [SerializeField, Min(0f)] private float _hideDepth = 10f;
        [Tooltip("Seconds the fog takes to fade out once the ship starts sinking.")]
        [SerializeField, Min(0.01f)] private float _fogFadeSeconds = 1f;
        [Tooltip("Seconds the fog takes to fade in once the ship has risen and levelled out.")]
        [SerializeField, Min(0.01f)] private float _fogFadeInSeconds = 4f;

        // Rising starts the two ambience clips together and the horn sounds once it has fully risen (2026-11-01).
        // While it sinks the ambience fades with depth and stops when the ship is hidden. The sources ride on the ship,
        // so it is heard only nearby and from where it is; SoundManager's volume and mute settings still apply.
        [Header("Audio")]
        [Tooltip("Created if missing, for the player's volume settings.")]
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField] private AudioClip _hauntClip;
        [SerializeField, Range(0f, 1f)] private float _hauntVolume = 0.684f;
        [SerializeField] private AudioClip _rumbleClip;
        [SerializeField, Range(0f, 1f)] private float _rumbleVolume = 0.684f;
        [SerializeField] private AudioClip _hornClip;
        [SerializeField, Range(0f, 1f)] private float _hornVolume = 0.24624f;
        [Tooltip("Ambience is at full volume within this distance and fades linearly to silence at the max distance.")]
        [SerializeField, Min(0f)] private float _ambienceMinDistance = 15f;
        [SerializeField, Min(0.01f)] private float _ambienceMaxDistance = 90f;
        [Tooltip("The horn carries further than the ambience.")]
        [SerializeField, Min(0f)] private float _hornMinDistance = 20f;
        [SerializeField, Min(0.01f)] private float _hornMaxDistance = 150f;

        private bool _isOut;
        private GhostShipPhase _soundPhase;
        private AudioSource _haunt;
        private AudioSource _rumble;
        private AudioSource _horn;
        // Ambience gain, lowered with depth while sinking.
        private float _ambienceFade = 1f;
        private bool _isAmbienceFading;
        private float _lampIntensity;
        private ParticleSystemRenderer[] _fogRenderers;
        private Color[] _fogColors;
        private MaterialPropertyBlock _fogBlock;
        // Seconds into the fog fade; below zero when not fading.
        private float _fogFadeTime = -1f;
        private bool _isFogFadingIn;

        private void Awake()
        {
            _lampIntensity = _lamp != null ? _lamp.intensity : 0f;
            CacheFog();
            _haunt = CreateSource("HauntAudio", _hauntClip, _ambienceMinDistance, _ambienceMaxDistance);
            _rumble = CreateSource("RumbleAudio", _rumbleClip, _ambienceMinDistance, _ambienceMaxDistance);
            _horn = CreateSource("HornAudio", _hornClip, _hornMinDistance, _hornMaxDistance);
            Apply(GhostShipPhase.Hidden);
        }

        private void OnEnable()
        {
            if (_ship != null)
            {
                _ship.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (_ship != null)
            {
                _ship.PhaseChanged -= HandlePhaseChanged;
            }
        }

        private void LateUpdate()
        {
            if (_isOut)
            {
                ApplyDepth();
            }

            if (_fogFadeTime >= 0f)
            {
                StepFogFade();
            }

            if (_isAmbienceFading)
            {
                StepAmbienceFade();
            }

            ApplyAudioVolumes();
        }

        private void HandlePhaseChanged(GhostShipPhase phase)
        {
            Apply(phase);
            PlayPhaseSound(phase);
        }

        // Keyed on the previous phase so a late joiner's first notice of a ship already out does not sound the horn.
        private void PlayPhaseSound(GhostShipPhase phase)
        {
            GhostShipPhase previous = _soundPhase;
            _soundPhase = phase;

            if (phase == previous)
            {
                return;
            }

            if (!SoundManager.Instance && _soundManagerPrefab)
            {
                Instantiate(_soundManagerPrefab);
            }

            if (phase == GhostShipPhase.Rising)
            {
                _ambienceFade = 1f;
                _isAmbienceFading = false;
                ApplyAudioVolumes();
                Play(_haunt);
                Play(_rumble);
            }
            else if (phase == GhostShipPhase.Roaming && previous == GhostShipPhase.Rising)
            {
                ApplyAudioVolumes();
                Play(_horn);
            }
            else if (phase == GhostShipPhase.Sinking)
            {
                _isAmbienceFading = true;
            }
            else if (phase == GhostShipPhase.Hidden)
            {
                StopAmbience();
            }
        }

        // Fades down as the ship goes under and ends the moment it is hidden (_hideDepth), not at the start of the sink.
        // Never climbs back, so a wave lifting the hull does not swell the sound again.
        private void StepAmbienceFade()
        {
            float seaLevel = _ship != null ? _ship.SeaLevel : 0f;
            float depth = seaLevel - transform.position.y;
            _ambienceFade = Mathf.Min(_ambienceFade, 1f - Mathf.Clamp01(depth / Mathf.Max(0.01f, _hideDepth)));

            if (_ambienceFade <= 0f)
            {
                StopAmbience();
            }
        }

        private void StopAmbience()
        {
            _isAmbienceFading = false;

            if (_haunt)
            {
                _haunt.Stop();
            }

            if (_rumble)
            {
                _rumble.Stop();
            }
        }

        private void ApplyAudioVolumes()
        {
            SoundManager sound = SoundManager.Instance;
            float settings = !sound ? 1f : sound.IsMuted ? 0f : sound.MasterVolume * sound.EffectVolume;

            if (_haunt)
            {
                _haunt.volume = _hauntVolume * _ambienceFade * settings;
            }

            if (_rumble)
            {
                _rumble.volume = _rumbleVolume * _ambienceFade * settings;
            }

            if (_horn)
            {
                _horn.volume = _hornVolume * settings;
            }
        }

        private static void Play(AudioSource source)
        {
            if (source && source.clip)
            {
                source.Play();
            }
        }

        // On a child of the ship root, which moves with the ship and is never hidden (the visual root is).
        private AudioSource CreateSource(string objectName, AudioClip clip, float minDistance, float maxDistance)
        {
            if (!clip)
            {
                return null;
            }

            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
            return source;
        }

        private void Apply(GhostShipPhase phase)
        {
            _isOut = phase != GhostShipPhase.Hidden;
            ApplyDepth();

            if (_fog == null)
            {
                return;
            }

            if (phase == GhostShipPhase.Roaming)
            {
                _fogFadeTime = 0f;
                _isFogFadingIn = true;
                SetFogAlpha(0f);

                for (int i = 0; i < _fog.Length; i++)
                {
                    _fog[i].Play(true);
                }

                return;
            }

            if (phase == GhostShipPhase.Sinking)
            {
                for (int i = 0; i < _fog.Length; i++)
                {
                    _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                // A fade-in still under way carries on down from where it is.
                _fogFadeTime = _isFogFadingIn ? (1f - FogFadeInAlpha()) * _fogFadeSeconds : 0f;
                _isFogFadingIn = false;
            }
            else
            {
                ClearFog();
            }
        }

        private void ApplyDepth()
        {
            float seaLevel = _ship != null ? _ship.SeaLevel : 0f;
            float depth = seaLevel - transform.position.y;
            bool isShown = _isOut && depth < _hideDepth;

            if (_visualRoot != null && _visualRoot.activeSelf != isShown)
            {
                _visualRoot.SetActive(isShown);
            }

            float lampScale = 1f - Mathf.Clamp01(depth / _lampFadeDepth);

            if (_lamp != null)
            {
                float intensity = _lampIntensity * lampScale;
                _lamp.intensity = intensity;
                _lamp.enabled = isShown && intensity > 0f;
            }

            if (_lanterns != null)
            {
                for (int i = 0; i < _lanterns.Length; i++)
                {
                    if (_lanterns[i] != null)
                    {
                        _lanterns[i].Brightness = lampScale;
                    }
                }
            }
        }

        private void StepFogFade()
        {
            _fogFadeTime += Time.deltaTime;

            if (_isFogFadingIn)
            {
                float fadeIn = FogFadeInAlpha();
                SetFogAlpha(fadeIn);

                if (fadeIn >= 1f)
                {
                    _fogFadeTime = -1f;
                    _isFogFadingIn = false;
                }

                return;
            }

            float alpha = 1f - Mathf.Clamp01(_fogFadeTime / _fogFadeSeconds);
            SetFogAlpha(alpha);

            if (alpha <= 0f)
            {
                ClearFog();
            }
        }

        private float FogFadeInAlpha()
        {
            return Mathf.Clamp01(_fogFadeTime / _fogFadeInSeconds);
        }

        private void ClearFog()
        {
            _fogFadeTime = -1f;
            _isFogFadingIn = false;

            for (int i = 0; i < _fog.Length; i++)
            {
                _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetFogAlpha(1f);
        }

        private void CacheFog()
        {
            int count = _fog != null ? _fog.Length : 0;
            _fogRenderers = new ParticleSystemRenderer[count];
            _fogColors = new Color[count];
            _fogBlock = new MaterialPropertyBlock();

            for (int i = 0; i < count; i++)
            {
                _fogRenderers[i] = _fog[i] != null ? _fog[i].GetComponent<ParticleSystemRenderer>() : null;
                Material material = _fogRenderers[i] != null ? _fogRenderers[i].sharedMaterial : null;
                _fogColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
        }

        // Scales each fog renderer's base color alpha without touching the shared material.
        private void SetFogAlpha(float alpha)
        {
            for (int i = 0; i < _fogRenderers.Length; i++)
            {
                if (_fogRenderers[i] == null)
                {
                    continue;
                }

                Color color = _fogColors[i];
                color.a *= alpha;
                _fogRenderers[i].GetPropertyBlock(_fogBlock);
                _fogBlock.SetColor(BaseColorId, color);
                _fogRenderers[i].SetPropertyBlock(_fogBlock);
            }
        }
    }
}
