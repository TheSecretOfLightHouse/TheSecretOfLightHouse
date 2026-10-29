using System;
using Lighthouse.Modules.Rules;
using Lighthouse.Modules.Server;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Tower.Net
{
    // The lighthouse's module slots. Only finished modules from the shared workbench go in (2026-10-17), carried there
    // by hand (2026-10-20), floors fill
    // bottom-up and repair counts installed modules (2026-10-19 trial). Which slots are filled is public; which module
    // id sits where stays on the server for the corrosion work (JH05). Installed modules never come back out by hand.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class TowerNet : NetworkBehaviour
    {
        [SerializeField]
        private TowerLayout _layout;
        [Tooltip("Bench whose modules, once taken out by hand, are installed here.")]
        [SerializeField]
        private WorkbenchNet _workbench;

        // Indexed like the layout's slots.
        private readonly SyncList<bool> _installed = new SyncList<bool>();

        // Server-only, indexed like the layout's slots. 0 = empty.
        private uint[] _installedModuleIds = new uint[0];
        private bool _isLayoutValid;

        public TowerLayout Layout => _layout;
        public WorkbenchNet Workbench => _workbench;
        public bool IsComplete => _layout && _layout.SlotCount > 0 && InstalledCount == _layout.SlotCount;
        public float RepairRatio => _layout ? _layout.GetRepairRatio(InstalledCount) : 0f;
        public int RewardTier => _layout ? _layout.GetRewardTier(InstalledCount) : 0;

        // Read from the synced slots, so every machine shows the same repair.
        public int InstalledCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _installed.Count; i++)
                {
                    if (_installed[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // Raised on every machine when the slots change, and once on a client when the initial state arrives.
        public event Action Changed;
        // Server-only. (actorId, slot, moduleId) for logs, history and tests.
        public event Action<uint, int, uint> ModuleInstalled;

        private void OnEnable()
        {
            _installed.OnChange += HandleInstalledChanged;
        }

        private void OnDisable()
        {
            _installed.OnChange -= HandleInstalledChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            string error = !_layout ? "no tower layout assigned." : !_workbench ? "no workbench assigned." : _layout.Validate();
            _isLayoutValid = error == null;

            // A bad asset refuses every install instead of filling a half-described tower.
            if (!_isLayoutValid)
            {
                Debug.LogError($"[{nameof(TowerNet)}] {name}: {error}");
                return;
            }

            _installedModuleIds = new uint[_layout.SlotCount];
            _installed.Clear();

            for (int i = 0; i < _layout.SlotCount; i++)
            {
                _installed.Add(false);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // The initial slot list arrives without change callbacks.
            Changed?.Invoke();
        }

        public void BindWorkbench(WorkbenchNet workbench)
        {
            _workbench = workbench;
        }

        public bool IsInstalled(int slot)
        {
            return slot >= 0 && slot < _installed.Count && _installed[slot];
        }

        public bool IsFloorOpen(int floor)
        {
            return _layout && _layout.IsFloorOpen(floor, _installed);
        }

        // Slot an install of this recipe would fill right now, or -1. Same answer on every machine, so UI can show it.
        public int FindOpenSlot(int recipeId)
        {
            if (!_layout || recipeId == 0)
            {
                return -1;
            }

            for (int i = 0; i < _layout.SlotCount && i < _installed.Count; i++)
            {
                TowerSlot slot = _layout[i];

                if (!_installed[i] && slot.RecipeId == recipeId && IsFloorOpen(slot.Floor))
                {
                    return i;
                }
            }

            return -1;
        }

        // Installs the module the actor carries (taken out of this tower's workbench, 2026-10-20) into the lowest open
        // slot that fits it. Installs stay open after the summon, since the final battle finishes the same repair (2026-10-22).
        // recipeId is what the sender's screen showed in their hands, so a stale screen cannot install something else. Requests run one at a time on the server, so two players installing together fill two slots or one fails.
        [Server]
        public bool TryInstall(uint actorId, int recipeId, ModuleLedger ledger)
        {
            if (!_isLayoutValid || ledger == null
                || !_workbench.TryGetCarried(actorId, out int carriedRecipeId, out _) || carriedRecipeId != recipeId)
            {
                return false;
            }

            int slot = FindOpenSlot(recipeId);

            if (slot < 0 || !_workbench.TryReleaseCarried(actorId, out _, out uint moduleId))
            {
                return false;
            }

            ledger.TrySetState(moduleId, ModuleState.Installed);
            _installedModuleIds[slot] = moduleId;
            _installed[slot] = true;
            ModuleInstalled?.Invoke(actorId, slot, moduleId);
            return true;
        }

        private void HandleInstalledChanged(SyncList<bool>.Operation operation, int index, bool value)
        {
            Changed?.Invoke();
        }
    }
}
