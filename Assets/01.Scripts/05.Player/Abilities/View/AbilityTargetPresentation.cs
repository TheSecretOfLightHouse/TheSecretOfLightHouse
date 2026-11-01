using TMPro;
using UnityEngine;
using Lighthouse.Player.Abilities.Net;

namespace Lighthouse.Player.Abilities.View
{
    [CreateAssetMenu(menuName = "TheLightHouse/Player/Ability Target Presentation")]
    public sealed class AbilityTargetPresentation : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Material _fontMaterial;
        [SerializeField] private Sprite _navigator;
        [SerializeField] private Sprite _mechanic;
        [SerializeField] private Sprite _guard;
        [SerializeField] private Sprite _gatherer;

        public void ApplyFont(TMP_Text label)
        {
            label.font = _font;
            label.fontSharedMaterial = _fontMaterial;
        }

        public void UpgradeLabels(GameObject root)
        {
            foreach (UnityEngine.UI.Text label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                UpgradeLabel(label);
            }
        }

        public TMP_Text UpgradeLabel(UnityEngine.UI.Text source)
        {
            if (!source)
            {
                return null;
            }
            TMP_Text existing = source.GetComponentInChildren<TMP_Text>(true);
            if (existing)
            {
                return existing;
            }

            // Keep serialized scene bindings while the TMP child follows the original layout.
            var item = new GameObject("TmpCaption", typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(source.transform, false);
            TMP_Text label = item.GetComponent<TextMeshProUGUI>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            ApplyFont(label);
            label.text = source.text;
            label.color = source.color;
            label.fontSize = source.fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = source.fontSize * 0.7f;
            label.fontSizeMax = source.fontSize;
            label.richText = source.supportRichText;
            label.raycastTarget = false;
            label.textWrappingMode = source.horizontalOverflow == HorizontalWrapMode.Wrap
                ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.alignment = source.alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
            source.enabled = false;
            return label;
        }

        public static string GetJobName(PlayerController player)
        {
            if (!player)
            {
                return string.Empty;
            }
            string job = player.TryGetComponent(out NavigatorRouteRequest _) ? "항해사"
                : player.TryGetComponent(out EmergencyRepairRequest _) ? "정비공"
                : player.TryGetComponent(out GuardShieldRequest _) ? "경비병"
                : player.TryGetComponent(out GatheringFeverRequest _) ? "채집가"
                : "직군 미확인";
            return player.netIdentity && player.isLocalPlayer ? $"{job} (나)" : job;
        }

        public static UnityEngine.UI.Image[] CreateIcons(UnityEngine.UI.Button[] buttons, UnityEngine.UI.Text[] names)
        {
            var icons = new UnityEngine.UI.Image[buttons != null ? buttons.Length : 0];
            for (int i = 0; i < icons.Length; i++)
            {
                if (!buttons[i])
                {
                    continue;
                }
                var item = new GameObject("JobPortrait", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                item.transform.SetParent(buttons[i].transform, false);
                UnityEngine.UI.Image icon = item.GetComponent<UnityEngine.UI.Image>();
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(44f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(52f, 52f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                icons[i] = icon;
                if (names != null && i < names.Length && names[i])
                {
                    RectTransform label = names[i].rectTransform;
                    label.anchorMin = Vector2.zero;
                    label.anchorMax = Vector2.one;
                    label.offsetMin = new Vector2(84f, 4f);
                    label.offsetMax = new Vector2(-14f, -4f);
                    names[i].alignment = TextAnchor.MiddleLeft;
                }
            }
            return icons;
        }

        public void BindIcon(UnityEngine.UI.Image icon, PlayerController player)
        {
            if (!icon)
            {
                return;
            }
            icon.sprite = !player ? null
                : player.TryGetComponent(out NavigatorRouteRequest _) ? _navigator
                : player.TryGetComponent(out EmergencyRepairRequest _) ? _mechanic
                : player.TryGetComponent(out GuardShieldRequest _) ? _guard
                : player.TryGetComponent(out GatheringFeverRequest _) ? _gatherer : null;
            icon.enabled = icon.sprite;
        }
    }
}
