#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 trial GUI: the tower's floors top to bottom with each slot, repair and reward tier, and the module carry
    // flow (2026-10-20): take a finished module out of the workbench, then install it in the tower or on your own boat,
    // or put it back. It stands in for the inventory screen and boat input, which belong to Player.
    // Requests go through the local player's relay like real input would.
    [DisallowMultipleComponent]
    public sealed class TowerTestPanel : MonoBehaviour
    {
        private const float Width = 300f;

        [SerializeField]
        private TowerNet _tower;
        [Tooltip("Label per reward tier, same order as the layout's reward percents.")]
        [SerializeField]
        private string[] _rewardLabels = { "배 이동 속도", "자원 파밍량", "배 대포 피해" };
        [SerializeField]
        private float _top = 230f;

        private InteractionRelay _relay;
        private PlayerController _player;
        private string _lastResult = "-";
        private Vector2 _scroll;

        private void OnEnable()
        {
            DevPanelSwitch.Register();
        }

        private void OnDisable()
        {
            DevPanelSwitch.Unregister();

            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private void OnGUI()
        {
            // In a build, Mirror keeps scene objects inactive on a client until spawned, and netId is not readable before.
            if (!DevPanelSwitch.IsOn || !_tower || !_tower.isActiveAndEnabled || !NetworkClient.active || !_tower.Layout || !_tower.Workbench)
            {
                return;
            }

            BindLocalPlayer();
            TowerLayout layout = _tower.Layout;
            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, _top, Width, Screen.height - _top - 10f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"등대{(layout.IsTestValue ? " (시험안)" : string.Empty)}  마지막 결과: {_lastResult}");
            int carriedRecipeId = DrawCarry();
            GUILayout.Label($"수리도 {_tower.RepairRatio * 100f:0.#}%  ({_tower.InstalledCount}/{layout.SlotCount})");
            DrawRewards(layout);

            for (int floor = layout.FloorCount - 1; floor >= 0; floor--)
            {
                DrawFloor(layout, floor, carriedRecipeId);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void BindLocalPlayer()
        {
            if (_relay || !NetworkClient.localPlayer)
            {
                return;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();
            _player = NetworkClient.localPlayer.GetComponent<PlayerController>();

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }
        }

        // The local player's own module and the bench stock. Who else holds what is private (2026-10-21).
        // Returns the recipe id in the local player's hands, or 0.
        private int DrawCarry()
        {
            WorkbenchNet workbench = _tower.Workbench;
            ModuleRecipeCatalog catalog = workbench.Catalog;
            int carriedRecipeId = workbench.OwnCarriedRecipe;
            GUILayout.Label("── 모듈 운반 (작업대 → 손 → 등대·배)");

            if (carriedRecipeId != 0)
            {
                ModuleRecipe carried = catalog && catalog.TryGetById(carriedRecipeId, out ModuleRecipe recipe) ? recipe : null;
                GUILayout.Label($"내 손: {(carried ? carried.DisplayName : carriedRecipeId.ToString())}{(carried && carried.IsBoatModule ? " (배 모듈)" : string.Empty)}");
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("작업대에 되돌리기"))
                {
                    Send(workbench.netId, InteractionAction.ReturnModule);
                }

                if (carried && carried.IsBoatModule)
                {
                    BoatController boat = GetOwnBoat();
                    GUI.enabled = boat;

                    if (GUILayout.Button(boat ? "내 배에 장착" : "배에 타야 장착"))
                    {
                        Send(boat.netId, InteractionAction.InstallModule, carriedRecipeId);
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("내 손: 비어 있음 (작업대 근처에서 꺼내기)");
            }

            DrawStock(workbench, catalog, carriedRecipeId == 0);
            return carriedRecipeId;
        }

        private void DrawStock(WorkbenchNet workbench, ModuleRecipeCatalog catalog, bool canTake)
        {
            if (!catalog)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                ModuleRecipe recipe = catalog[i];
                int stored = workbench.GetStoredCount(recipe.Id);

                if (stored <= 0)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"  보관 {recipe.DisplayName} {stored}개");
                GUI.enabled = canTake;

                if (GUILayout.Button("꺼내기", GUILayout.Width(60f)))
                {
                    Send(workbench.netId, InteractionAction.TakeModule, recipe.Id);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawRewards(TowerLayout layout)
        {
            int tier = _tower.RewardTier;

            for (int i = 0; i < layout.RewardTierCount; i++)
            {
                string label = i < _rewardLabels.Length ? _rewardLabels[i] : $"보상 {i + 1}";
                GUILayout.Label($"{(i < tier ? "[받음]" : "[  ]")} {layout.GetRewardPercent(i)}% {label}");
            }

            GUILayout.Label($"{(_tower.IsComplete ? "[가능]" : "[  ]")} 100% 점화 의식 (가운데 판 진행 패널)");
        }

        private void DrawFloor(TowerLayout layout, int floor, int carriedRecipeId)
        {
            bool isOpen = _tower.IsFloorOpen(floor);
            GUILayout.Label($"── {layout.GetFloorName(floor)} {(isOpen ? string.Empty : "(잠김: 아래층 먼저)")}");

            for (int i = 0; i < layout.SlotCount; i++)
            {
                TowerSlot slot = layout[i];

                if (slot.Floor != floor)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                string name = slot.Recipe ? slot.Recipe.DisplayName : "?";

                if (_tower.IsInstalled(i))
                {
                    GUILayout.Label($"  {name}: 장착됨");
                }
                else
                {
                    GUILayout.Label($"  {name}: 비어 있음");
                    // Only the slot the server would pick for the module in hand gets a live button, so the order rule is visible.
                    GUI.enabled = carriedRecipeId == slot.RecipeId && _tower.FindOpenSlot(slot.RecipeId) == i;

                    if (GUILayout.Button("장착", GUILayout.Width(50f)))
                    {
                        Send(_tower.netId, InteractionAction.InstallModule, slot.RecipeId);
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }
        }

        // The boat the local player is aboard, if it is their own. The server checks the same again.
        private BoatController GetOwnBoat()
        {
            BoatController boat = _player ? _player.CurrentBoat : null;
            return boat && boat.Owner == _player ? boat : null;
        }

        private void Send(uint targetNetId, InteractionAction action, int arg0 = 0)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(targetNetId, action, arg0))
            {
                _lastResult = "보내지 못함";
            }
        }

        private void HandleResultReceived(InteractionResult result)
        {
            _lastResult = result.Code.ToString();
        }
    }
}
#endif
