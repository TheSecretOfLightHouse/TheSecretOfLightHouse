using Lighthouse.Player.Abilities.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // Always-on HUD from the 2026-10-29 in-game HUD mockup: carried module card and role/boat panel bottom left, key
    // hints top right (Esc exit, M, Tab, and Q by job: gatherer on land, others aboard). The boat's durability follows the local player's own boat, aboard or not. The card shows the
    // module the local player carries, read from the shared workbench (WorkbenchNet.OwnCarriedRecipe; not every scene
    // binds PlayerInventory to it), and hides while none is; the workbench screen's own carry card
    // shows only while that screen is open. The role shows the local player's job, named by the ability request its job
    // prefab carries (as VotingScreen does). The mockup's "E" hint is left out: each station shows its own prompt when in
    // range. Shown while a local player exists.
    [DisallowMultipleComponent]
    public sealed class InGameHud : MonoBehaviour
    {
        [Serializable]
        private struct RecipeIcon
        {
            public ModuleRecipe Recipe;
            public Sprite Icon;
        }

        private const float WorkbenchLookupInterval = 1f;

        [SerializeField]
        private GameObject _root;

        [Header("Carried module")]
        [SerializeField]
        private GameObject _card;
        [SerializeField]
        private Image _cardIcon;
        [SerializeField]
        private TMP_Text _cardName;
        [SerializeField]
        private TMP_Text _cardUse;
        [Tooltip("The workbench screen's module icons, so the card matches it. A recipe not listed uses its own icon.")]
        [SerializeField]
        private RecipeIcon[] _recipeIcons = new RecipeIcon[0];

        [Header("Role")]
        [SerializeField]
        private Image _roleIcon;
        [SerializeField]
        private TMP_Text _roleName;
        [Tooltip("The voting screen's job portraits, so both screens show the same face.")]
        [SerializeField]
        private Sprite _navigatorIcon;
        [SerializeField]
        private Sprite _mechanicIcon;
        [SerializeField]
        private Sprite _guardIcon;
        [SerializeField]
        private Sprite _gathererIcon;

        [Header("Boat")]
        [SerializeField]
        private Image _boatFill;
        [SerializeField]
        private TMP_Text _boatText;
        [Tooltip("The current durability in \"current / max\", as in the mockup. The max keeps the text colour.")]
        [SerializeField]
        private Color _boatNumberColor = new Color(0.98f, 0.73f, 0.15f);
        [Tooltip("Bar fill change per second while easing toward the synced value.")]
        [SerializeField, Min(0.01f)]
        private float _fillSpeed = 1.2f;

        [Header("Key hints")]
        [Tooltip("Q ability row: gatherer on land, other jobs aboard a boat.")]
        [SerializeField]
        private GameObject[] _boatOnlyHints = new GameObject[0];

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private string _boatNumberHex;
        private bool _isShown;
        private NetworkIdentity _boundPlayer;
        private PlayerController _player;
        private GatheringFeverAbility _gatheringAbility;
        private WorkbenchNet _workbench;
        private float _nextWorkbenchLookup;
        private int _shownRecipeId = -1;
        private ModuleRecipe _shownRecipe;
        private BoatController _boundBoat;
        private BoatDurability _durability;
        private int _shownDurability = -1;
        private int _shownMaxDurability = -1;
        private bool _isBoatHintShown = true;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _boatNumberHex = ColorUtility.ToHtmlStringRGB(_boatNumberColor);
            _root.SetActive(false);
            SetBoatHintShown(false);
        }

        private void LateUpdate()
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            bool hasPlayer = localPlayer;

            if (hasPlayer != _isShown)
            {
                _isShown = hasPlayer;
                _root.SetActive(hasPlayer);
            }

            if (!_isShown)
            {
                return;
            }

            if (localPlayer != _boundPlayer)
            {
                _boundPlayer = localPlayer;
                _player = localPlayer.GetComponent<PlayerController>();
                _gatheringAbility = localPlayer.GetComponent<GatheringFeverAbility>();
                _shownRecipeId = -1;
                RefreshRole(localPlayer);
            }

            RefreshCard();
            SetBoatHintShown(_player && !_player.IsDowned
                && (_gatheringAbility ? _gatheringAbility.IsOnLand : _player.IsOnBoat));

            // A wrecked boat is replaced by a recovery boat, so the owned boat is checked every frame.
            BoatController boat = _player ? _player.OwnedBoat : null;

            if (boat != _boundBoat)
            {
                _boundBoat = boat;
                _durability = boat ? boat.GetComponent<BoatDurability>() : null;
                _shownDurability = -1;
                _shownMaxDurability = -1;

                if (_boatFill)
                {
                    _boatFill.fillAmount = DurabilityRatio();
                }
            }

            if (_boatFill)
            {
                _boatFill.fillAmount = Mathf.MoveTowards(_boatFill.fillAmount, DurabilityRatio(), _fillSpeed * Time.unscaledDeltaTime);
            }

            RefreshBoatText();
        }

        // The workbench is a scene network object that spawns after the HUD, so it is looked up until found.
        private void RefreshCard()
        {
            if (!_card)
            {
                return;
            }

            if (!_workbench && Time.unscaledTime >= _nextWorkbenchLookup)
            {
                _nextWorkbenchLookup = Time.unscaledTime + WorkbenchLookupInterval;
                _workbench = FindAnyObjectByType<WorkbenchNet>();
            }

            int recipeId = _workbench ? _workbench.OwnCarriedRecipe : 0;
            ModuleRecipe recipe = recipeId != 0 && _workbench.Catalog
                && _workbench.Catalog.TryGetById(recipeId, out ModuleRecipe found) ? found : null;

            if (recipeId == _shownRecipeId && recipe == _shownRecipe)
            {
                return;
            }

            _shownRecipeId = recipeId;
            _shownRecipe = recipe;
            _card.SetActive(recipeId != 0);

            if (recipeId == 0)
            {
                return;
            }

            if (_cardIcon)
            {
                _cardIcon.sprite = recipe ? GetRecipeIcon(recipe) : null;
                _cardIcon.enabled = _cardIcon.sprite;
            }

            if (_cardName)
            {
                _cardName.text = recipe ? recipe.DisplayName : "모듈";
            }

            if (_cardUse)
            {
                _cardUse.text = !recipe ? "" : recipe.IsBoatModule ? "배 모듈" : "등대 모듈";
            }
        }

        // Each job prefab carries its own active ability request, so that names the job. The job never changes during
        // a match, so this runs only when the local player object changes.
        private void RefreshRole(NetworkIdentity player)
        {
            Sprite icon = null;
            string jobName = "";

            if (player.TryGetComponent(out NavigatorRouteRequest _))
            {
                icon = _navigatorIcon;
                jobName = "항해사";
            }
            else if (player.TryGetComponent(out EmergencyRepairRequest _))
            {
                icon = _mechanicIcon;
                jobName = "정비공";
            }
            else if (player.TryGetComponent(out GuardShieldRequest _))
            {
                icon = _guardIcon;
                jobName = "경비병";
            }
            else if (player.TryGetComponent(out GatheringFeverRequest _))
            {
                icon = _gathererIcon;
                jobName = "채집가";
            }

            if (_roleIcon)
            {
                _roleIcon.sprite = icon;
                _roleIcon.enabled = icon;
            }

            if (_roleName)
            {
                _roleName.text = jobName;
            }
        }

        // Keep the serialized row references while choosing visibility by job.
        private void SetBoatHintShown(bool isShown)
        {
            if (isShown == _isBoatHintShown)
            {
                return;
            }

            _isBoatHintShown = isShown;

            for (int i = 0; i < _boatOnlyHints.Length; i++)
            {
                if (_boatOnlyHints[i])
                {
                    _boatOnlyHints[i].SetActive(isShown);
                }
            }
        }

        private Sprite GetRecipeIcon(ModuleRecipe recipe)
        {
            for (int i = 0; i < _recipeIcons.Length; i++)
            {
                if (_recipeIcons[i].Recipe == recipe && _recipeIcons[i].Icon)
                {
                    return _recipeIcons[i].Icon;
                }
            }

            return recipe.Icon;
        }

        private float DurabilityRatio()
        {
            return _durability && _durability.MaxDurability > 0f
                ? Mathf.Clamp01(_durability.CurrentDurability / _durability.MaxDurability)
                : 0f;
        }

        private void RefreshBoatText()
        {
            int durability = _durability ? Mathf.CeilToInt(_durability.CurrentDurability) : 0;
            int maxDurability = _durability ? Mathf.CeilToInt(_durability.MaxDurability) : 0;

            if (!_boatText || (durability == _shownDurability && maxDurability == _shownMaxDurability))
            {
                return;
            }

            _shownDurability = durability;
            _shownMaxDurability = maxDurability;
            _boatText.text = _durability
                ? $"<color=#{_boatNumberHex}>{durability}</color> / {maxDurability}"
                : "- / -";
        }

        private void ApplyOsFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            Text[] texts = GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }
    }
}
