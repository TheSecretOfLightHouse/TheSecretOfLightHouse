using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.World.Weather.Rules;
using Lighthouse.World.Weather.View;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeatherStateNet))]
    public sealed class WeatherFinalBattleBridge : NetworkBehaviour
    {
        [SerializeField] private TowerMatchNet _match;
        [SerializeField] private WeatherPresentationView _presentation;
        [SerializeField] private WeatherLightningView _lightning;
        [SerializeField] private WeatherLightningNet _events;
        [SerializeField, Min(0f)] private float _transitionSeconds = 10f;
        [Tooltip("How long the sea takes to settle into calm after a citizens' win.")]
        [SerializeField, Min(0f)] private float _victoryCalmSeconds = 7f;
        private WeatherStateNet _weather;
        private TowerMatchNet _subscribed;

        private void Awake()
        {
            _weather = GetComponent<WeatherStateNet>();
        }

        private void Update()
        {
            if ((isServer || isClient) && WorldServices.TryGet<TowerMatchNet>(out var match) && _match != match)
            {
                Unsubscribe();
                _match = match;
                if (isServer)
                {
                    Subscribe();
                }
            }
            if (isClient && _match != null && _match.Phase == TowerPhase.Ended)
            {
                StopEndedPresentation(_match.Outcome);
            }
        }

        public override void OnStartServer()
        {
            if (WorldServices.TryGet<TowerMatchNet>(out var match))
            {
                _match = match;
            }
            Subscribe();
        }

        private void Subscribe()
        {
            if (_match != null)
            {
                _subscribed = _match;
                _match.FinalBattleStarted += HandleFinalBattleStarted;
                _match.Ended += HandleEnded;
                if (_match.Phase == TowerPhase.FinalBattle)
                {
                    HandleFinalBattleStarted();
                }
            }
        }

        public override void OnStartClient()
        {
            if (_presentation != null)
            {
                _presentation.enabled = true;
            }
            if (_lightning != null)
            {
                _lightning.enabled = true;
            }
        }

        public override void OnStopServer()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.FinalBattleStarted -= HandleFinalBattleStarted;
                _subscribed.Ended -= HandleEnded;
            }
            _subscribed = null;
        }

        public override void OnStopClient()
        {
            StopPresentation();
        }

        [Server]
        private void HandleFinalBattleStarted()
        {
            _weather.BeginTransition(WeatherType.Maelstrom, _transitionSeconds);
        }

        [Server]
        private void HandleEnded(TowerOutcome outcome, TowerEndReason reason)
        {
            if (_events != null)
            {
                _events.StopEvents();
            }
            if (outcome == TowerOutcome.CitizensWin)
            {
                _weather.BeginCalmRelease(_victoryCalmSeconds);
            }
            StopEndedPresentation(outcome);
        }

        // A citizens' win keeps the sky and sea views running so the storm fades into calm instead of cutting out;
        // only the lightning stops.
        private void StopEndedPresentation(TowerOutcome outcome)
        {
            if (outcome == TowerOutcome.None)
            {
                return;
            }
            if (outcome == TowerOutcome.CitizensWin)
            {
                StopLightning();
                return;
            }
            StopPresentation();
        }

        private void StopLightning()
        {
            if (_lightning != null)
            {
                _lightning.enabled = false;
            }
        }

        private void StopPresentation()
        {
            if (_presentation != null)
            {
                _presentation.enabled = false;
            }
            StopLightning();
        }
    }
}
