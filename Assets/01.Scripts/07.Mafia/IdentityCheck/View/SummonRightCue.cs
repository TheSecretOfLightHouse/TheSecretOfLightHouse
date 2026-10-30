using Lighthouse.Common.UI;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.IdentityCheck.View
{
    // Makes the mafia's 10 second summon right hard to miss (2026-10-29): a line on the shared ScreenNotice strip and a
    // sound when the right is granted, a pulse on the summon button that quickens for the last seconds, and a line when
    // it lapses (the public battle-start notice belongs to TowerMatchEffects). The green glow, screen flash, timer bar
    // and the cue's own line were dropped; the screen's status text keeps the seconds. Only the mafia receives identity state, so citizens
    // never see any of it. Nothing here takes input, so the right's seconds are never spent on a box.
    [DisallowMultipleComponent]
    public sealed class SummonRightCue : MonoBehaviour
    {
        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;

        [Header("Parts")]
        [Tooltip("Needs a centre pivot so the pulse grows in place.")]
        [SerializeField]
        private RectTransform _summonButton;
        [SerializeField]
        private AudioSource _audio;

        [Header("Look")]
        [Tooltip("The last seconds pulse faster.")]
        [SerializeField]
        private float _hurrySeconds = 3f;
        [SerializeField]
        private float _pulseScale = 0.08f;
        [Tooltip("Pulses per second.")]
        [SerializeField]
        private float _pulseRate = 1.2f;
        [SerializeField]
        private float _hurryPulseRate = 3f;
        [Tooltip("Played on this client only when the right is granted. Empty = silent.")]
        [SerializeField]
        private AudioClip _grantedClip;

        private double _grantedExpiresAt = -1;
        private bool _wasLive;
        private bool _hasSummoned;

        private void OnEnable()
        {
            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>(FindObjectsInactive.Include);
            }

            if (_voting)
            {
                _voting.IdentityResponded += HandleIdentityResponded;
            }
        }

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
            }

            HideRight();
        }

        private void Update()
        {
            if (_voting && _voting.HasOwnIdentityState)
            {
                RefreshRight(_voting.OwnIdentityState);
            }
            else
            {
                _hasSummoned = false;
                _wasLive = false;
                _grantedExpiresAt = -1;
                HideRight();
            }
        }

        private void RefreshRight(in IdentityResponse state)
        {
            double left = state.SummonExpiresAt - NetworkTime.time;
            bool isLive = state.HasSummonRight && left > 0;

            // A new expiry is a new grant, so a second successful check plays the opening again.
            if (isLive && state.SummonExpiresAt != _grantedExpiresAt)
            {
                _grantedExpiresAt = state.SummonExpiresAt;
                PlayGranted();
            }
            else if (!isLive && _wasLive && !_hasSummoned)
            {
                ScreenNotice.Show(new ScreenNoticeRequest("소환 권리가 사라졌다", ScreenNoticeStyle.Warning));
            }

            _wasLive = isLive;

            // Downed keeps the clock running but the button cannot be pressed, so it stays still.
            if (!isLive || !state.CanSummon)
            {
                HideRight();
                return;
            }

            float rate = left <= _hurrySeconds ? _hurryPulseRate : _pulseRate;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * rate * 2f * Mathf.PI);
            _summonButton.localScale = Vector3.one * (1f + _pulseScale * wave);
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.Result != IdentityActionResult.SummonApproved || !response.IsNewDecision)
            {
                return;
            }

            _hasSummoned = true;
            // The phase observer announces the start to everyone, including this mafia, exactly once.
        }

        private void PlayGranted()
        {
            ScreenNotice.Show(new ScreenNoticeRequest("바다가 응답했다 — 지금 크툴루를 소환할 수 있다", ScreenNoticeStyle.Gold));

            if (_audio && _grantedClip)
            {
                _audio.PlayOneShot(_grantedClip);
            }
        }

        private void HideRight()
        {
            if (_summonButton)
            {
                _summonButton.localScale = Vector3.one;
            }
        }
    }
}
