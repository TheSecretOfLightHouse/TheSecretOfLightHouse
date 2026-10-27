using Lighthouse.Interaction.Requests.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    public sealed partial class InventoryChestScreen
    {
        private static readonly Color Ink = new Color(0.12f, 0.065f, 0.035f, 1f);
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
        private static readonly BoatModuleType[] ModuleKinds =
        {
            BoatModuleType.Searchlight, BoatModuleType.Dash, BoatModuleType.DepthAlarm, BoatModuleType.Stabilizer
        };
        private static readonly string[] ModuleNames = { "탐조등", "대시 추진기", "장애물 경보", "선체 안정기" };
        private static readonly string[] ModuleDescriptions = { "어두운 바다를 밝히는 빛", "위험을 벗어나는 순간 가속", "지형과 몬스터 충돌 경고", "배의 좌우·앞뒤 흔들림 완화" };
        private static readonly int[] BoatRecipeIds = { 101, 102, 103, 104 };

        private readonly GameObject[] _pages = new GameObject[3];
        private readonly UnityEngine.UI.Image[] _tabImages = new UnityEngine.UI.Image[3];
        private readonly UnityEngine.UI.Text[] _tabLabels = new UnityEngine.UI.Text[3];
        private readonly UnityEngine.UI.Text[] _moduleStates = new UnityEngine.UI.Text[4];
        private readonly UnityEngine.UI.Image[] _moduleCards = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Text[] _repairRows = new UnityEngine.UI.Text[8];
        private readonly UnityEngine.UI.Text[] _repairAmounts = new UnityEngine.UI.Text[8];
        private UnityEngine.UI.Text _capacityLabel;
        private UnityEngine.UI.Text _carriedLabel;
        private UnityEngine.UI.Text _loadoutCarriedLabel;
        private UnityEngine.UI.Image _loadoutCarriedIcon;
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
            // Preserve serialized legacy objects while replacing the presentation at runtime.
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
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(1140f, 760f);
            window.localScale = Vector3.one;
            UnityEngine.UI.CanvasScaler scaler = GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 900f);
                scaler.matchWidthOrHeight = 1f;
            }
            CreateRoundedSprite();
            var dim = MakePanel("Backdrop", window, -2500f, -1500f, 6140f, 3760f, new Color(0.01f, 0.025f, 0.035f, 0.8f));
            dim.raycastTarget = true;
            MakePanel("Frame", window, -2f, -2f, 1144f, 764f, Cream);
            MakePanel("Body", window, 0f, 0f, 1140f, 760f, Ink);
            MakeText("Eyebrow", window, "등대 항해일지  /  개인 보관함", 32f, 20f, 750f, 24f, 13, Muted);
            MakeText("Title", window, "항해자의 보관함", 30f, 48f, 700f, 48f, 32, Cream);
            MakeText("CloseHint", window, "I · TAB · ESC 닫기", 850f, 49f, 250f, 40f, 14, Muted, TextAnchor.MiddleRight);
            string[] names = { "01   소지품", "02   선체 수리", "03   배 모듈" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                UnityEngine.UI.Button tab = MakeButton("Tab" + i, window, names[i], 30f + i * 362f, 112f, 352f, 52f, () => SelectTab(index));
                _tabImages[i] = tab.GetComponent<UnityEngine.UI.Image>();
                _tabLabels[i] = tab.GetComponentInChildren<UnityEngine.UI.Text>();
                _pages[i] = MakeRect("Page" + i, window, 30f, 184f, 1080f, 466f).gameObject;
            }
            BuildInventoryPage(_pages[0].transform);
            BuildRepairPage(_pages[1].transform);
            BuildLoadoutPage(_pages[2].transform);
            MakePanel("FooterDivider", window, 30f, 674f, 1080f, 1f, new Color(0.3f, 0.36f, 0.34f));
            _message = MakeText("Message", window, "", 32f, 689f, 850f, 44f, 15, Gold);
            _closeButton = MakeButton("Close", window, "닫기  ×", 965f, 690f, 145f, 42f, null);
            SelectTab(0);
        }

        private void BuildInventoryPage(Transform page)
        {
            MakeText("Heading", page, "항해에 필요한 것들", 0f, 0f, 600f, 38f, 23, Cream);
            _capacityLabel = MakeText("Capacity", page, "0 / 8 칸", 505f, 0f, 180f, 38f, 15, Gold, TextAnchor.MiddleRight);
            MakeText("Hint", page, "같은 자원은 한 묶음으로 정리됩니다.", 0f, 40f, 700f, 30f, 14, Muted);
            _resourceSlots = new InventoryChestSlot[8];
            for (int i = 0; i < 8; i++)
            {
                float x = i % 4 * 176f;
                float y = 87f + i / 4 * 166f;
                Transform cell = MakePanel("Resource" + i, page, x, y, 160f, 150f, PanelColor).transform;
                MakeText("Index", cell, (i + 1).ToString("00"), 12f, 8f, 36f, 24f, 12, Muted);
                _resourceSlots[i] = MakeSlot(cell, 0f, 0f, 160f, 150f, -1);
            }
            Transform carry = MakePanel("Carried", page, 730f, 0f, 350f, 422f, PanelColor).transform;
            MakeText("Title", carry, "운반 중인 모듈", 24f, 20f, 300f, 36f, 21, Cream);
            MakeText("Limit", carry, "개인 운반  0–1개", 24f, 60f, 300f, 24f, 13, Muted);
            _moduleSlots = new InventoryChestSlot[1];
            _moduleSlots[0] = MakeSlot(carry, 95f, 107f, 160f, 145f, 0);
            _carriedLabel = MakeText("Name", carry, "운반 중인 모듈 없음", 24f, 263f, 300f, 42f, 19, Gold, TextAnchor.MiddleCenter);
            MakeText("Help", carry, "작업대에서 완성품을 가져온 뒤\n배 모듈 탭에서 장착하세요.", 24f, 315f, 300f, 65f, 15, Muted, TextAnchor.MiddleCenter);
            MakeText("BottomHint", page, "소지품은 본인에게만 표시됩니다.", 0f, 430f, 700f, 26f, 13, Muted);
        }

        private void BuildRepairPage(Transform page)
        {
            Transform status = MakePanel("HullStatus", page, 0f, 0f, 440f, 446f, PanelColor).transform;
            MakeText("Eyebrow", status, "선체 점검", 28f, 22f, 380f, 26f, 13, Gold);
            MakeText("Heading", status, "내 배의 상태", 26f, 57f, 380f, 40f, 26, Cream);
            _durabilityLabel = MakeText("Durability", status, "배 정보 없음", 26f, 126f, 388f, 60f, 36, Cream);
            Transform bar = MakePanel("Track", status, 28f, 207f, 384f, 18f, Ink).transform;
            _durabilityFill = MakePanel("Fill", bar, 0f, 0f, 384f, 18f, Teal);
            MakeText("Threshold", status, "수리 가능 기준  ·  내구도 30% 이상", 28f, 240f, 385f, 36f, 15, Gold);
            _repairStatus = MakeText("Status", status, "배에 탑승하면 수리할 수 있습니다.", 28f, 302f, 385f, 100f, 17, Muted);
            Transform recipe = MakePanel("RepairRecipe", page, 460f, 0f, 620f, 446f, PanelColor).transform;
            MakeText("Title", recipe, "선체 수리 레시피", 28f, 22f, 550f, 38f, 23, Cream);
            _repairDescription = MakeText("Description", recipe, "재료가 모이면 내구도를 회복합니다.", 28f, 65f, 550f, 42f, 15, Muted);
            MakeText("MaterialHeading", recipe, "필요 재료", 28f, 116f, 220f, 28f, 14, Gold);
            MakeText("AmountsHeading", recipe, "보유 / 투입 / 필요", 300f, 116f, 255f, 28f, 14, Gold, TextAnchor.MiddleRight);
            for (int i = 0; i < _repairRows.Length; i++)
            {
                _repairRows[i] = MakeText("Material" + i, recipe, "", 28f, 151f + i * 23f, 255f, 24f, 16, Cream);
                _repairAmounts[i] = MakeText("Amounts" + i, recipe, "", 300f, 151f + i * 23f, 255f, 24f, 16, Cream, TextAnchor.MiddleRight);
            }
            MakeText("PartialDeposit", recipe, "재료가 부족해도 보유한 만큼 먼저 투입할 수 있습니다.", 28f, 328f, 564f, 22f, 12, Muted);
            _repairButton = MakeButton("Repair", recipe, "재료 투입 · 수리", 28f, 355f, 564f, 58f, HandleRepairClicked);
        }

        private void BuildLoadoutPage(Transform page)
        {
            MakeText("Heading", page, "나의 배 · 장착 현황", 0f, 0f, 700f, 36f, 23, Cream);
            MakeText("Slots", page, "종류별 1개  /  최대 4종 동시 장착", 680f, 0f, 400f, 36f, 14, Gold, TextAnchor.MiddleRight);
            Transform sea = MakePanel("BoatDiagram", page, 359f, 50f, 362f, 320f, PanelColor).transform;
            RectTransform boat = MakeRect("Boat", sea, 81f, 5f, 200f, 305f);
            boat.gameObject.AddComponent<InventoryBoatGraphic>().raycastTarget = false;
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? 0f : 744f;
                float y = i % 2 == 0 ? 52f : 219f;
                _moduleCards[i] = MakePanel("Module" + i, page, x, y, 336f, 145f, PanelColor);
                Transform card = _moduleCards[i].transform;
                UnityEngine.UI.Image icon = MakePanel("Icon", card, 17f, 23f, 64f, 64f, Color.white);
                icon.sprite = GetRecipeIcon(BoatRecipeIds[i]);
                icon.type = UnityEngine.UI.Image.Type.Simple;
                icon.preserveAspect = true;
                icon.enabled = icon.sprite;
                MakeText("Name", card, ModuleNames[i], 98f, 15f, 222f, 32f, 20, Cream);
                _moduleStates[i] = MakeText("State", card, "미장착", 98f, 50f, 220f, 30f, 16, Muted);
                MakeText("Description", card, ModuleDescriptions[i], 17f, 101f, 302f, 30f, 13, Muted);
                MakePanel("Connector" + i, page, i < 2 ? 336f : 721f, y + 68f, 23f, 2f, Gold);
            }
            Transform carry = MakePanel("InstallBar", page, 0f, 389f, 1080f, 77f, PanelColor).transform;
            _loadoutCarriedIcon = MakePanel("Icon", carry, 17f, 12f, 52f, 52f, Color.white);
            _loadoutCarriedIcon.type = UnityEngine.UI.Image.Type.Simple;
            _loadoutCarriedIcon.preserveAspect = true;
            _loadoutCarriedLabel = MakeText("Carried", carry, "운반 중인 모듈 없음", 87f, 12f, 650f, 52f, 17, Cream);
            _installButton = MakeButton("Install", carry, "내 배에 장착", 815f, 14f, 244f, 49f, null);
        }

        private InventoryChestSlot MakeSlot(Transform parent, float x, float y, float width, float height, int moduleIndex)
        {
            RectTransform root = MakeRect("Slot", parent, x, y, width, height);
            UnityEngine.UI.Image hitArea = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.001f);
            GameObject highlight = MakePanel("Highlight", root, 3f, 3f, width - 6f, height - 6f, new Color(0.9f, 0.72f, 0.38f, 0.18f)).gameObject;
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
                Sprite tabSkin = i == _selectedTab ? _activeButtonSkin : _buttonSkin;
                _tabImages[i].sprite = tabSkin ? tabSkin : _roundedSprite;
                _tabImages[i].color = tabSkin ? Color.white : i == _selectedTab ? Gold : PanelColor;
                _tabLabels[i].color = i == _selectedTab ? Ink : Cream;
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
            for (int i = 0; i < ModuleKinds.Length; i++)
            {
                bool installed = _equipment && _equipment.HasModule(ModuleKinds[i]);
                _moduleStates[i].text = !_equipment ? "배 정보 없음" : installed ? "● 장착 완료" : "○ 빈 슬롯";
                _moduleStates[i].color = installed ? Teal : Muted;
                _moduleCards[i].sprite = installed && _selectedSkin ? _selectedSkin : _panelSkin ? _panelSkin : _roundedSprite;
                _moduleCards[i].color = _panelSkin ? Color.white : PanelColor;
            }
            RefreshCarriedModule();
        }

        private void RefreshRepairPage()
        {
            float current = _boatDurability ? _boatDurability.CurrentDurability : 0f;
            float maximum = _boatDurability ? _boatDurability.MaxDurability : 0f;
            float ratio = maximum > 0f ? current / maximum : 0f;
            _durabilityLabel.text = _boatDurability ? $"{current:0} / {maximum:0}  ·  {ratio:P0}" : "배 정보 없음";
            _durabilityFill.rectTransform.sizeDelta = new Vector2(384f * Mathf.Clamp01(ratio), 18f);
            _durabilityFill.color = ratio < 0.3f ? new Color(0.9f, 0.35f, 0.28f) : Teal;
            bool hasMaterials = false;
            for (int i = 0; i < _repairRows.Length; i++)
            {
                bool valid = _boatRepair && i < _boatRepair.RequirementCount;
                _repairRows[i].gameObject.SetActive(valid);
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
                _repairAmounts[i].text = $"{owned}  /  {deposited}  /  {required}";
                _repairRows[i].color = owned + deposited >= required ? Teal : Cream;
                _repairAmounts[i].color = _repairRows[i].color;
                hasMaterials |= owned > 0 && deposited < required;
            }
            float multiplier = _player && _player.PassiveController && _displayBoat
                ? _player.PassiveController.GetRepairAmountMultiplier(_displayBoat) : 1f;
            _repairDescription.text = _boatRepair ? $"할당량 충족 시 내구도 +{_boatRepair.RepairAmount * multiplier:0.#} · 최대치까지만 회복" : "배의 수리 정보를 기다리고 있습니다.";
            bool aboard = _player && _player.IsOnBoat && _player.CurrentBoat == _displayBoat;
            _repairStatus.text = !_boatDurability ? "소유한 배 정보가 없습니다." : _boatDurability.IsWrecked ? "난파한 배는 일반 수리를 할 수 없습니다."
                : ratio < 0.3f ? "내구도 30% 미만 · 일반 수리 불가" : ratio >= 1f ? "선체 상태가 완전합니다."
                : !aboard ? "자기 배에 탑승하면 수리할 수 있습니다." : !hasMaterials ? "투입할 수리 재료가 부족합니다." : "수리 가능 · 보유 재료를 투입하세요.";
            _repairButton.interactable = _boatRepair && _boatRepair.RequirementCount > 0 && _boatDurability && !_boatDurability.IsWrecked
                && ratio >= 0.3f && ratio < 1f && aboard && hasMaterials && _inventory && _relay && !_relay.IsAwaitingResult
                && !_isRepairing && !_isInstalling && NetworkClient.ready;
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
            _capacityLabel.text = $"{used} / {InventoryData.SlotCapacity} 칸";
        }

        private void RefreshCarriedModule()
        {
            if (!_carriedLabel)
            {
                return;
            }
            int recipeId = _inventory ? _inventory.ModuleRecipeId : 0;
            string name = GetModuleName();
            _carriedLabel.text = name;
            _loadoutCarriedLabel.text = recipeId == 0 ? "운반 중인 모듈 없음 · 작업대에서 가져오세요." : name + "  ·  자기 배 탑승 후 장착";
            _loadoutCarriedIcon.sprite = GetRecipeIcon(recipeId);
            _loadoutCarriedIcon.enabled = _loadoutCarriedIcon.sprite;
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
            Sprite skin = null;
            if (name == "Body")
            {
                skin = _frameSkin;
            }
            else if (color == PanelColor)
            {
                skin = name.StartsWith("Resource") ? _slotSkin : _panelSkin;
            }
            if (skin)
            {
                image.sprite = skin;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1.5f;
            }
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
            if (_activeButtonSkin)
            {
                image.sprite = _activeButtonSkin;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1.5f;
            }
            image.raycastTarget = true;
            UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.72f);
            colors.disabledColor = new Color(0.35f, 0.4f, 0.4f, 0.6f);
            button.colors = colors;
            if (clicked != null)
            {
                button.onClick.AddListener(clicked);
            }
            MakeText("Label", image.transform, label, 0f, 0f, width, height, 18, Ink, TextAnchor.MiddleCenter);
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
