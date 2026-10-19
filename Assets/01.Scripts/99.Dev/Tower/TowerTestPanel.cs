#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 trial GUI: the tower's floors top to bottom with each slot, repair and reward tier, and an install button
    // for the slot a stored module would go into. Requests go through the local player's relay like real input would.
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
        private string _lastResult = "-";

        private void OnDisable()
        {
            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private void OnGUI()
        {
            // In a build, Mirror keeps scene objects inactive on a client until spawned, and netId is not readable before.
            if (!_tower || !_tower.isActiveAndEnabled || !NetworkClient.active || !_tower.Layout || !_tower.Workbench)
            {
                return;
            }

            BindLocalPlayer();
            TowerLayout layout = _tower.Layout;
            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, _top, Width, Screen.height - _top - 10f), GUI.skin.box);
            GUILayout.Label($"등대{(layout.IsTestValue ? " (시험안)" : string.Empty)}  마지막 결과: {_lastResult}");
            GUILayout.Label($"수리도 {_tower.RepairRatio * 100f:0.#}%  ({_tower.InstalledCount}/{layout.SlotCount})");
            DrawRewards(layout);

            for (int floor = layout.FloorCount - 1; floor >= 0; floor--)
            {
                DrawFloor(layout, floor);
            }

            GUILayout.EndArea();
        }

        private void BindLocalPlayer()
        {
            if (_relay || !NetworkClient.localPlayer)
            {
                return;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
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

        private void DrawFloor(TowerLayout layout, int floor)
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
                    int stored = _tower.Workbench.GetStoredCount(slot.RecipeId);
                    GUILayout.Label($"  {name}: 비어 있음 (보관 {stored})");
                    // Only the slot the server would pick gets a live button, so the order rule is visible.
                    GUI.enabled = stored > 0 && _tower.FindOpenSlot(slot.RecipeId) == i;

                    if (GUILayout.Button("장착", GUILayout.Width(50f)))
                    {
                        Send(slot.RecipeId);
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }
        }

        private void Send(int recipeId)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_tower.netId, InteractionAction.InstallModule, recipeId))
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
