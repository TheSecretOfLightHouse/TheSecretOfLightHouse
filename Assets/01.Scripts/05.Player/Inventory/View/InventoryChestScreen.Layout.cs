using Lighthouse.Interaction.Requests.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    public sealed partial class InventoryChestScreen
    {
        private static readonly Color Ink = new Color(0.13f, 0.09f, 0.055f, 0.96f);
        private static readonly Color PanelColor = new Color(0.19f, 0.105f, 0.06f, 1f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.27f, 1f);
        private static readonly Color Cream = new Color(0.98f, 0.92f, 0.79f, 1f);
        private static readonly Color Muted = new Color(0.72f, 0.67f, 0.56f, 1f);
        private static readonly Color Teal = new Color(1f, 0.78f, 0.27f, 1f);
        [SerializeField] private Sprite _panelSkin;
        [SerializeField] private Sprite _frameSkin;
        [SerializeField] private Sprite _slotSkin;
        [SerializeField] private Sprite _selectedSkin;
        [SerializeField] private Sprite _buttonSkin;
        [SerializeField] private Sprite _activeButtonSkin;
        [SerializeField] private Sprite _boatIllustration;
        private static readonly BoatModuleType[] ModuleKinds =
        {
            BoatModuleType.Searchlight, BoatModuleType.Dash, BoatModuleType.DepthAlarm, BoatModuleType.Stabilizer
        };
        private static readonly string[] ModuleNames = { "탐조등", "대쉬", "장애물 경보", "선체 안정기" };
        private static readonly string[] ModuleDescriptions = { "어두운 바다를 밝히는 빛", "위험을 벗어나는 순간 가속", "지형과 몬스터 충돌 경고", "배의 좌우·앞뒤 흔들림 완화" };
        private static readonly int[] BoatRecipeIds = { 101, 102, 103, 104 };

        private readonly GameObject[] _pages = new GameObject[3];
        private readonly UnityEngine.UI.Image[] _tabImages = new UnityEngine.UI.Image[3];
        private readonly UnityEngine.UI.Text[] _tabLabels = new UnityEngine.UI.Text[3];
        private readonly UnityEngine.UI.Text[] _moduleStates = new UnityEngine.UI.Text[4];
        private readonly UnityEngine.UI.Image[] _moduleCards = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Button[] _moduleButtons = new UnityEngine.UI.Button[4];
        private readonly InventoryFrameGraphic[] _moduleFrames = new InventoryFrameGraphic[4];
        private readonly UnityEngine.UI.Image[] _installedIcons = new UnityEngine.UI.Image[4];
        private readonly GameObject[] _tabUnderlines = new GameObject[3];
        private readonly UnityEngine.UI.Image[] _repairIcons = new UnityEngine.UI.Image[8];
        private readonly UnityEngine.UI.Text[] _repairRows = new UnityEngine.UI.Text[8];
        private readonly UnityEngine.UI.Text[] _repairAmounts = new UnityEngine.UI.Text[8];
        private UnityEngine.UI.Text _capacityLabel;
        private UnityEngine.UI.Text _carriedLabel;
        private UnityEngine.UI.Text _loadoutCarriedLabel;

        private UnityEngine.UI.Text _durabilityLabel;
        private UnityEngine.UI.Text _repairDescription;
        private UnityEngine.UI.Text _repairStatus;
        private UnityEngine.UI.Image _durabilityFill;
        private UnityEngine.UI.Button _repairButton;
        private BoatController _displayBoat;
        private BoatDurability _boatDurability;
        private BoatRepair _boatRepair;
        private BoatModuleEquipment _equipment;
        private Texture2D _roundedTexture;
        private Sprite _roundedSprite;
        private int _selectedTab;
        private int _lastShipHash;
        private float _nextShipRefresh;
        private bool _isRepairing;

        private void BuildTabbedLayout()
        {
            if (!_boatIllustration)
            {
                _boatIllustration = Resources.Load<Sprite>("Inventory/boat_deck");
            }
            foreach (Transform child in _window.transform)
            {
                child.gameObject.SetActive(false);
            }
            UnityEngine.UI.Image oldBackground = _window.GetComponent<UnityEngine.UI.Image>();
            if (oldBackground)
            {
                oldBackground.enabled = false;
            }
            RectTransform window = (RectTransform)_window.transform;
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(1000f, 660f);
            window.localScale = Vector3.one;
            UnityEngine.UI.CanvasScaler scaler = GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 900f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            CreateRoundedSprite();
            UnityEngine.UI.Image dim = MakePanel("Backdrop", window, -2500f, -1500f, 6000f, 4000f, new Color(0.01f, 0.015f, 0.025f, 0.42f));
            dim.raycastTarget = true;
            MakeFrame("Lid", window, 0f, -56f, 1000f, 68f, Ink, Cream);
            MakeFrame("Handle", window, 450f, -65f, 100f, 27f, Ink, Cream);
            MakeFrame("Body", window, 0f, 0f, 1000f, 600f, Ink, Cream);
            for (int i = 0; i < 2; i++)
            {
                MakeFrame("Hinge" + i, window, i == 0 ? 90f : 870f, -9f, 32f, 22f, Ink, Muted);
            }
            string[] names = { "소지품", "선체 수리", "배 모듈" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                UnityEngine.UI.Button tab = MakeButton("Tab" + i, window, names[i], 35f + i * 298f, 13f, 278f, 68f, () => SelectTab(index));
                _tabImages[i] = tab.GetComponent<UnityEngine.UI.Image>();
                _tabLabels[i] = tab.GetComponentInChildren<UnityEngine.UI.Text>();
                _tabLabels[i].fontSize = 26;
                _tabLabels[i].rectTransform.anchoredPosition = new Vector2(35f, 0f);
                _tabLabels[i].rectTransform.sizeDelta = new Vector2(230f, 68f);
                InventoryTabGlyph glyph = MakeRect("Glyph", tab.transform, 17f, 15f, 38f, 38f).gameObject.AddComponent<InventoryTabGlyph>();
                glyph.color = Cream;
                glyph.Configure(i);
                _tabUnderlines[i] = MakePanel("Selected", window, 55f + i * 298f, 84f, 238f, 5f, Gold).gameObject;
                _pages[i] = MakeRect("Page" + i, window, 40f, 120f, 920f, 420f).gameObject;
            }
            MakePanel("HeaderLine", window, 5f, 91f, 990f, 1f, Muted);
            BuildInventoryPage(_pages[0].transform);
            BuildRepairPage(_pages[1].transform);
            BuildLoadoutPage(_pages[2].transform);
            _message = MakeText("Message", window, "", 40f, 551f, 920f, 34f, 14, Gold);
            _closeButton = MakeButton("Close", window, "×", 939f, 15f, 43f, 43f, null);
            _closeButton.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 34;
            SelectTab(0);
        }

        private void BuildInventoryPage(Transform page)
        {
            MakePanel("Divider", page, 600f, 0f, 1f, 410f, Muted);
            _resourceSlots = new InventoryChestSlot[8];
            for (int i = 0; i < 8; i++)
            {
                Transform cell = MakeFrame("Resource" + i, page, i % 4 * 144f, 26f + i / 4 * 154f, 126f, 130f, Color.clear, Muted).transform;
                _resourceSlots[i] = MakeSlot(cell, 0f, 0f, 126f, 130f, -1);
            }
            _capacityLabel = MakeText("Capacity", page, "0 / 8", 0f, 366f, 300f, 35f, 23, Cream);
            MakeText("Heading", page, "모듈", 640f, 0f, 260f, 40f, 25, Cream);
            MakePanel("ModuleLine", page, 640f, 48f, 270f, 1f, Muted);
            Transform carry = MakeFrame("Carried", page, 655f, 80f, 240f, 238f, Color.clear, Muted).transform;
            _moduleSlots = new InventoryChestSlot[1];
            _moduleSlots[0] = MakeSlot(carry, 0f, 0f, 240f, 238f, 0);
            _carriedLabel = MakeText("Name", page, "", 635f, 331f, 275f, 35f, 16, Muted, TextAnchor.MiddleCenter);
        }

        private void BuildRepairPage(Transform page)
        {
            MakePanel("Divider", page, 600f, 0f, 1f, 410f, Muted);
            RectTransform boat = MakeBoat(page, 120f, -85f, 285f, 455f);
            boat.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakeText("DurabilityHeading", page, "내구도", 20f, 316f, 250f, 36f, 22, Cream);
            _durabilityLabel = MakeText("Durability", page, "배 정보 없음", 265f, 316f, 305f, 36f, 22, Cream, TextAnchor.MiddleRight);
            Transform bar = MakeFrame("Track", page, 20f, 359f, 550f, 26f, Color.clear, Muted).transform;
            _durabilityFill = MakePanel("Fill", bar, 4f, 4f, 542f, 18f, Gold);
            MakeText("RecipeHeading", page, "수리 재료", 640f, 0f, 270f, 38f, 25, Cream);
            MakePanel("RecipeLine", page, 640f, 48f, 270f, 1f, Muted);
            for (int i = 0; i < _repairRows.Length; i++)
            {
                float x = 640f + i % 4 * 68f;
                float y = 66f + i / 4 * 105f;
                Transform cell = MakeRect("Material" + i, page, x, y, 64f, 100f);
                _repairIcons[i] = MakePanel("Icon", cell, 4f, 0f, 56f, 56f, Color.white);
                _repairIcons[i].type = UnityEngine.UI.Image.Type.Simple;
                _repairIcons[i].preserveAspect = true;
                _repairRows[i] = MakeText("Name", cell, "", 0f, 56f, 64f, 19f, 11, Muted, TextAnchor.MiddleCenter);
                _repairAmounts[i] = MakeText("Amount", cell, "", 0f, 76f, 64f, 24f, 17, Cream, TextAnchor.MiddleCenter);
            }
            _repairDescription = MakeText("RepairAmount", page, "", 640f, 282f, 270f, 35f, 22, Gold);
            _repairButton = MakeButton("Repair", page, "수리", 650f, 334f, 250f, 56f, HandleRepairClicked);
            _repairStatus = MakeText("Status", page, "", 20f, 395f, 550f, 30f, 13, Muted);
        }

        private void BuildLoadoutPage(Transform page)
        {
            MakePanel("Divider", page, 350f, 0f, 1f, 410f, Muted);
            RectTransform boat = MakeBoat(page, 475f, -20f, 310f, 445f);
            Vector2[] positions = { new Vector2(113f, 52f), new Vector2(111f, 228f), new Vector2(172f, 167f), new Vector2(112f, 322f) };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                int displayIndex = i == 1 ? 2 : i == 2 ? 1 : i;
                float x = displayIndex % 2 * 165f;
                float y = displayIndex / 2 * 195f;
                Transform card = MakeFrame("ModuleFrame" + i, page, x, y, 135f, 125f, Color.clear, Muted).transform;
                _moduleFrames[i] = card.GetComponent<InventoryFrameGraphic>();
                _moduleCards[i] = MakePanel("ModuleIcon" + i, card, 14f, 10f, 107f, 101f, Color.white);
                _moduleCards[i].sprite = GetRecipeIcon(BoatRecipeIds[i]);
                _moduleCards[i].type = UnityEngine.UI.Image.Type.Simple;
                _moduleCards[i].preserveAspect = true;
                _moduleCards[i].raycastTarget = true;
                UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = _moduleCards[i];
                UnityEngine.UI.ColorBlock moduleColors = button.colors;
                moduleColors.disabledColor = Color.white;
                button.colors = moduleColors;
                button.onClick.AddListener(() => HandleModuleClicked(index));
                card.gameObject.AddComponent<Lighthouse.Common.UI.UiButtonFeedback>();
                _moduleButtons[i] = button;
                MakeText("Name", page, ModuleNames[i], x - 5f, y + 128f, 150f, 30f, 17, Cream, TextAnchor.MiddleCenter);
                _moduleStates[i] = MakeText("State", page, "", x - 5f, y + 157f, 150f, 25f, 13, Muted, TextAnchor.MiddleCenter);
                _installedIcons[i] = MakePanel("Installed" + i, boat, positions[i].x, positions[i].y, 70f, 70f, Color.white);
                _installedIcons[i].sprite = GetRecipeIcon(BoatRecipeIds[i]);
                _installedIcons[i].type = UnityEngine.UI.Image.Type.Simple;
                _installedIcons[i].preserveAspect = true;
                _installedIcons[i].enabled = false;
            }
            _loadoutCarriedLabel = MakeText("ClickHint", page, "모듈 클릭 시 바로 장착", 0f, 391f, 345f, 28f, 15, Muted);

        }

        private RectTransform MakeBoat(Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = MakeRect("Boat", parent, x, y, width, height);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x + width * 0.5f, -y - height * 0.5f);
            if (_boatIllustration)
            {
                UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.sprite = _boatIllustration;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                rect.gameObject.AddComponent<InventoryBoatGraphic>().raycastTarget = false;
            }
            return rect;
        }

        private InventoryFrameGraphic MakeFrame(string name, Transform parent, float x, float y, float width, float height, Color fill, Color border)
        {
            InventoryFrameGraphic frame = MakeRect(name, parent, x, y, width, height).gameObject.AddComponent<InventoryFrameGraphic>();
            frame.color = fill;
            frame.Configure(border);
            frame.raycastTarget = false;
            return frame;
        }

        private void HandleModuleClicked(int index)
        {
            if (!CanInstall() || !_inventory || _inventory.ModuleRecipeId != BoatRecipeIds[index]
                || (_equipment && _equipment.HasModule(ModuleKinds[index])))
            {
                return;
            }
            HandleInstallClicked();
            RefreshModuleButtons();
        }

        private void RefreshModuleButtons()
        {
            for (int i = 0; i < _moduleButtons.Length; i++)
            {
                bool installed = _equipment && _equipment.HasModule(ModuleKinds[i]);
                bool carried = _inventory && _inventory.ModuleRecipeId == BoatRecipeIds[i];
                _moduleButtons[i].interactable = carried && !installed && CanInstall();
                _moduleCards[i].color = installed || carried ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _moduleStates[i].text = installed ? "장착 완료" : carried ? "운반 중" : "미보유";
                _moduleStates[i].color = installed || carried ? Gold : Muted;
                _moduleFrames[i].Configure(installed || carried ? Gold : Muted);
                _installedIcons[i].enabled = installed && _installedIcons[i].sprite;
            }
        }

        private InventoryChestSlot MakeSlot(Transform parent, float x, float y, float width, float height, int moduleIndex)
        {
            RectTransform root = MakeRect("Slot", parent, x, y, width, height);
            UnityEngine.UI.Image hitArea = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.001f);
            GameObject highlight = MakeFrame("Highlight", root, 1f, 1f, width - 2f, height - 2f, Color.clear, Gold).gameObject;
            highlight.SetActive(false);
            UnityEngine.UI.Image icon = MakePanel("Icon", root, 30f, 27f, width - 60f, height - 62f, Color.white);
            icon.sprite = null;
            icon.enabled = false;
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            // Slot hover moves this anchored icon around its centered origin.
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            UnityEngine.UI.Text count = MakeText("Count", root, "", 10f, height - 37f, width - 24f, 30f, 21, Cream, TextAnchor.MiddleRight);
            InventoryChestSlot slot = root.gameObject.AddComponent<InventoryChestSlot>();
            slot.Configure(icon, count, highlight, this, moduleIndex);
            return slot;
        }

        private void SelectTab(int index)
        {
            _selectedTab = Mathf.Clamp(index, 0, 2);
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].SetActive(i == _selectedTab);
                _tabImages[i].color = Color.clear;
                _tabLabels[i].color = i == _selectedTab ? Gold : Cream;
                _tabUnderlines[i].SetActive(i == _selectedTab);
            }
            if (_message)
            {
                _message.text = "";
            }
            UpdateShipPanels(true);
        }

        private void BindBoat(BoatController boat)
        {
            _displayBoat = boat;
            _boatDurability = boat ? boat.GetComponent<BoatDurability>() : null;
            _boatRepair = boat ? boat.GetComponent<BoatRepair>() : null;
            _equipment = boat ? boat.GetComponent<BoatModuleEquipment>() : null;
            _lastShipHash = int.MinValue;
        }

        private void UpdateShipPanels(bool force = false)
        {
            if (!_repairButton || (!force && Time.unscaledTime < _nextShipRefresh))
            {
                return;
            }
            _nextShipRefresh = Time.unscaledTime + 0.2f;
            BoatController boat = _player ? _player.OwnedBoat : null;
            if (boat != _displayBoat)
            {
                BindBoat(boat);
            }
            int hash = _boatDurability ? _boatDurability.CurrentDurability.GetHashCode() ^ _boatDurability.MaxDurability.GetHashCode() : 0;
            hash = hash * 31 + (_player && _player.IsOnBoat ? 1 : 0);
            hash = hash * 31 + (_inventory ? _inventory.ModuleRecipeId : 0);
            hash = hash * 31 + (_relay && _relay.IsAwaitingResult ? 1 : 0);
            hash = hash * 31 + (_isInstalling ? 1 : 0);
            hash = hash * 31 + (_isRepairing ? 1 : 0);
            hash = hash * 31 + (NetworkClient.ready ? 1 : 0);
            hash = hash * 31 + GetDisplayedRepairMultiplier().GetHashCode();
            for (int i = 0; i < ModuleKinds.Length; i++)
            {
                hash = hash * 31 + (_equipment && _equipment.HasModule(ModuleKinds[i]) ? 1 : 0);
            }
            if (_boatRepair)
            {
                for (int i = 0; i < _boatRepair.RequirementCount; i++)
                {
                    int id = _boatRepair.GetRequirementResourceId(i);
                    hash = hash * 31 + _boatRepair.GetDepositedAmount(id);
                    hash = hash * 31 + (_inventory ? _inventory.GetSyncedCount(id) : 0);
                }
            }
            if (!force && hash == _lastShipHash)
            {
                return;
            }
            _lastShipHash = hash;
            RefreshRepairPage();
            RefreshModuleButtons();
            RefreshCarriedModule();
        }

        private void RefreshRepairPage()
        {
            float current = _boatDurability ? _boatDurability.CurrentDurability : 0f;
            float maximum = _boatDurability ? _boatDurability.MaxDurability : 0f;
            float ratio = maximum > 0f ? current / maximum : 0f;
            _durabilityLabel.text = _boatDurability ? $"{current:0} / {maximum:0}" : "배 정보 없음";
            _durabilityFill.rectTransform.sizeDelta = new Vector2(542f * Mathf.Clamp01(ratio), 18f);
            _durabilityFill.color = ratio < 0.3f ? new Color(0.9f, 0.35f, 0.28f) : Teal;
            bool hasMaterials = false;
            for (int i = 0; i < _repairRows.Length; i++)
            {
                bool valid = _boatRepair && i < _boatRepair.RequirementCount;
                _repairRows[i].transform.parent.gameObject.SetActive(valid);
                _repairAmounts[i].gameObject.SetActive(valid);
                if (!valid)
                {
                    continue;
                }
                int id = _boatRepair.GetRequirementResourceId(i);
                int owned = _inventory ? _inventory.GetSyncedCount(id) : 0;
                int deposited = _boatRepair.GetDepositedAmount(id);
                int required = _boatRepair.GetRequiredAmount(id);
                _repairRows[i].text = _inventory ? _inventory.GetResourceDisplayName(id) : "자원";
                _repairAmounts[i].text = deposited > 0 ? $"{owned}+{deposited} / {required}" : $"{owned} / {required}";
                _repairIcons[i].sprite = GetResourceIcon(id);
                _repairIcons[i].enabled = _repairIcons[i].sprite;
                int columns = Mathf.Min(4, _boatRepair.RequirementCount);
                RectTransform cell = (RectTransform)_repairRows[i].transform.parent;
                float spacing = 270f / Mathf.Max(1, columns);
                cell.anchoredPosition = new Vector2(640f + i % 4 * spacing + (spacing - 64f) * 0.5f, -66f - i / 4 * 105f);
                _repairRows[i].color = owned + deposited >= required ? Teal : Cream;
                _repairAmounts[i].color = _repairRows[i].color;
                hasMaterials |= owned > 0 && deposited < required;
            }
            float multiplier = GetDisplayedRepairMultiplier();
            _repairDescription.text = _boatRepair
                ? $"수리량  +{_boatRepair.RepairAmount * multiplier:0.#}" : "";
            if (_boatRepair && multiplier > 1f)
            {
                _repairDescription.text += $"  (×{multiplier:0.##})";
            }
            bool aboard = _player && _player.IsOnBoat && _player.CurrentBoat == _displayBoat;
            _repairStatus.text = !_boatDurability ? "소유한 배 정보가 없습니다." : _boatDurability.IsWrecked ? "난파한 배는 일반 수리를 할 수 없습니다."
                : ratio < 0.3f ? "내구도 30% 미만 · 일반 수리 불가" : ratio >= 1f ? "선체 상태가 완전합니다."
                : !aboard ? "자기 배에 탑승하면 수리할 수 있습니다." : !hasMaterials ? "투입할 수리 재료가 부족합니다." : "수리 가능 · 보유 재료를 투입하세요.";
            _repairButton.interactable = _boatRepair && _boatRepair.RequirementCount > 0 && _boatDurability && !_boatDurability.IsWrecked
                && ratio >= 0.3f && ratio < 1f && aboard && hasMaterials && _inventory && _relay && !_relay.IsAwaitingResult
                && !_isRepairing && !_isInstalling && NetworkClient.ready;
        }

        private float GetDisplayedRepairMultiplier()
        {
            return _player && _player.PassiveController && _displayBoat
                ? _player.PassiveController.GetRepairAmountMultiplier(_displayBoat) : 1f;
        }

        private void HandleRepairClicked()
        {
            if (!_repairButton.interactable || !_displayBoat || !_relay)
            {
                return;
            }
            _isRepairing = true;
            _repairButton.interactable = false;
            _message.text = "수리 재료 투입 요청 중...";
            if (!_relay.TrySendToNetworkTarget(_displayBoat.netId, InteractionAction.RepairBoat))
            {
                _isRepairing = false;
                _message.text = "요청을 보내지 못했습니다.";
            }
        }

        private void RefreshCapacity()
        {
            if (!_capacityLabel)
            {
                return;
            }
            int used = 0;
            if (_inventory)
            {
                foreach (var entry in _inventory.Counts)
                {
                    used += InventoryData.GetStackCount(entry.Value, _inventory.StackCapacity);
                }
            }
            _capacityLabel.text = $"{used} / {InventoryData.SlotCapacity}";
        }

        private void RefreshCarriedModule()
        {
            if (!_carriedLabel)
            {
                return;
            }
            _carriedLabel.text = "";
            _loadoutCarriedLabel.text = "모듈 클릭 시 바로 장착";
        }

        private Sprite GetResourceIcon(int resourceId)
        {
            for (int i = 0; i < _resourceIds.Length && i < _resourceIcons.Length; i++)
            {
                if (_resourceIds[i] == resourceId)
                {
                    return _resourceIcons[i];
                }
            }
            return null;
        }

        private Sprite GetRecipeIcon(int recipeId)
        {
            for (int i = 0; i < _recipeIds.Length; i++)
            {
                if (_recipeIds[i] == recipeId && i < _recipeIcons.Length)
                {
                    return _recipeIcons[i];
                }
            }
            return null;
        }

        private RectTransform MakeRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private UnityEngine.UI.Image MakePanel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            UnityEngine.UI.Image image = MakeRect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = _roundedSprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private UnityEngine.UI.Text MakeText(string name, Transform parent, string value, float x, float y, float width, float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            UnityEngine.UI.Text text = MakeRect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.text = value;
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private UnityEngine.UI.Button MakeButton(string name, Transform parent, string label, float x, float y, float width, float height, UnityEngine.Events.UnityAction clicked)
        {
            UnityEngine.UI.Image image = MakePanel(name, parent, x, y, width, height, Gold);
            image.color = Color.clear;
            if (name == "Repair")
            {
                MakeFrame("Border", image.transform, 0f, 0f, width, height, Color.clear, Gold);
            }
            image.raycastTarget = true;
            UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.72f);
            colors.disabledColor = Color.clear;
            button.colors = colors;
            if (clicked != null)
            {
                button.onClick.AddListener(clicked);
            }
            MakeText("Label", image.transform, label, 0f, 0f, width, height, 23, Cream, TextAnchor.MiddleCenter);
            image.gameObject.AddComponent<Lighthouse.Common.UI.UiButtonFeedback>();
            return button;
        }

        private void CreateRoundedSprite()
        {
            const int Size = 32;
            _roundedTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            _roundedTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = Mathf.Max(8f - x, x - 23f, 0f);
                    float dy = Mathf.Max(8f - y, y - 23f, 0f);
                    byte alpha = (byte)(255f * Mathf.Clamp01(8f - Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _roundedTexture.SetPixels32(pixels);
            _roundedTexture.Apply(false, true);
            _roundedSprite = Sprite.Create(_roundedTexture, new Rect(0f, 0f, Size, Size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, new Vector4(9f, 9f, 9f, 9f));
        }

        private void DisposeLayout()
        {
            if (_roundedSprite)
            {
                Destroy(_roundedSprite);
            }
            if (_roundedTexture)
            {
                Destroy(_roundedTexture);
            }
        }
    }
}
