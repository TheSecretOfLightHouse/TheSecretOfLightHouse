using Lighthouse.Common.UI;
using Lighthouse.Interaction.GhostShipHints.Net;
using Lighthouse.Interaction.GhostShipHints.Rules;
using Lighthouse.Interaction.Relay.View;
using Lighthouse.Interaction.Targets.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.GhostShipHints.View
{
    // Client side of the ghost ship on the ship prefab: shows the E prompt while this ship is what E would inspect, and
    // opens the ledger on the machine that was sent the hint. The ledger screen is spawned from its own UI prefab on
    // first use, so the ship needs no scene UI. System & UI (최명기) can replace the screen by listening to
    // GhostShipHintNet's events the same way.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionTarget))]
    public sealed class GhostShipHintPopup : MonoBehaviour
    {
        [SerializeField] private GhostShipLedgerScreen _screenPrefab;

        private InteractionTarget _target;
        private GhostShipLedgerScreen _screen;
        private InteractionSelector _selector;

        private void Awake()
        {
            _target = GetComponent<InteractionTarget>();
        }

        private void OnEnable()
        {
            GhostShipHintNet.HintReceived += HandleHintReceived;
            GhostShipHintNet.HintAlreadyRead += HandleHintAlreadyRead;
        }

        private void Update()
        {
            if (!NetworkClient.active)
            {
                return;
            }

            bool isSelected = IsSelectedByLocalPlayer();

            if (isSelected && !_screen)
            {
                EnsureScreen();
            }

            if (_screen)
            {
                _screen.SetPromptShown(isSelected);
            }
        }

        private void OnDisable()
        {
            GhostShipHintNet.HintReceived -= HandleHintReceived;
            GhostShipHintNet.HintAlreadyRead -= HandleHintAlreadyRead;

            if (_screen)
            {
                _screen.SetPromptShown(false);
            }
        }

        private void OnDestroy()
        {
            if (_screen)
            {
                Destroy(_screen.gameObject);
            }
        }

        // The same pick E uses (nearest target in range), so the prompt never promises an inspect that goes elsewhere.
        private bool IsSelectedByLocalPlayer()
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            if (!player)
            {
                return false;
            }

            if (!_selector || _selector.gameObject != player.gameObject)
            {
                player.TryGetComponent(out _selector);
            }

            return _selector && _target.isActiveAndEnabled && _selector.Selected == _target;
        }

        private void HandleHintReceived(GhostShipHintEntry[] entries)
        {
            if (EnsureScreen())
            {
                _screen.Open(entries, ResourceName);
            }
        }

        private void HandleHintAlreadyRead()
        {
            ScreenNotice.Show(new ScreenNoticeRequest("유령선의 장부는 이미 누군가 읽었다."));
        }

        private static string ResourceName(int resourceId)
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            return player && player.TryGetComponent(out PlayerInventory inventory)
                ? inventory.GetResourceDisplayName(resourceId)
                : $"자원 {resourceId}";
        }

        private bool EnsureScreen()
        {
            if (_screen)
            {
                return true;
            }

            if (!_screenPrefab)
            {
                Debug.LogError($"[{nameof(GhostShipHintPopup)}] Ledger screen prefab is not assigned.", this);
                enabled = false;
                return false;
            }

            _screen = Instantiate(_screenPrefab);
            return true;
        }
    }
}
