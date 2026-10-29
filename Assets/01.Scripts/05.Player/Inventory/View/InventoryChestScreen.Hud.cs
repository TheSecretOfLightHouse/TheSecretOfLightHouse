using Lighthouse.Network.Session.View;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    public sealed partial class InventoryChestScreen
    {
        private RectTransform _carriedHud;
        private UnityEngine.UI.Image _carriedHudIcon;
        private UnityEngine.UI.Text _carriedHudName;
        private int _carriedHudRecipeId = -1;
        private ModuleRecipe _carriedHudRecipe;
        private Rect _carriedHudSafeArea;
        private int _carriedHudScreenWidth;
        private int _carriedHudScreenHeight;

        private void BuildCarriedModuleHud()
        {
            // A sibling of the inventory window stays available while the window is closed.
            _carriedHud = MakeRect("CarriedModuleHud", _window.transform.parent, 0f, 0f, 230f, 76f);
            _carriedHud.pivot = Vector2.zero;
            MakeFrame("Card", _carriedHud, 0f, 0f, 230f, 76f, new Color(0.10f, 0.075f, 0.045f, 0.9f), Cream);
            _carriedHudIcon = MakePanel("ModuleIcon", _carriedHud, 10f, 9f, 58f, 58f, Color.white);
            _carriedHudIcon.type = UnityEngine.UI.Image.Type.Simple;
            _carriedHudIcon.preserveAspect = true;
            MakeText("CarryingLabel", _carriedHud, "운반 중", 82f, 10f, 135f, 25f, 16, Gold);
            _carriedHudName = MakeText("ModuleName", _carriedHud, "", 82f, 36f, 135f, 27f, 17, Cream);
            _carriedHudName.resizeTextForBestFit = true;
            _carriedHudName.resizeTextMinSize = 12;
            _carriedHudName.resizeTextMaxSize = 17;
            UpdateCarriedHudAnchor();
            _carriedHud.gameObject.SetActive(false);
        }

        private void UpdateCarriedModuleHud()
        {
            UpdateCarriedHudAnchor();
            int recipeId = _inventory ? _inventory.ModuleRecipeId : 0;
            ModuleRecipe recipe = recipeId != 0 ? _inventory.GetModuleRecipe() : null;
            bool isVisible = recipeId != 0 && _player && NetworkClient.isConnected
                && !_isOpen && !SessionScreen.IsGameplayOverlayOpen;
            RefreshCarriedModuleHud(recipeId, recipe, isVisible);
        }

        private void RefreshCarriedModuleHud(int recipeId, ModuleRecipe recipe, bool isVisible)
        {
            if (_carriedHudRecipeId != recipeId || _carriedHudRecipe != recipe)
            {
                _carriedHudRecipeId = recipeId;
                _carriedHudRecipe = recipe;
                _carriedHudIcon.sprite = GetRecipeIcon(recipeId);
                _carriedHudIcon.enabled = _carriedHudIcon.sprite;
                _carriedHudName.text = recipe ? recipe.DisplayName : recipeId == 0 ? "" : "모듈";
            }
            if (_carriedHud.gameObject.activeSelf != isVisible)
            {
                _carriedHud.gameObject.SetActive(isVisible);
            }
        }

        private void UpdateCarriedHudAnchor()
        {
            Rect safeArea = Screen.safeArea;
            if (_carriedHudSafeArea == safeArea && _carriedHudScreenWidth == Screen.width
                && _carriedHudScreenHeight == Screen.height)
            {
                return;
            }
            _carriedHudSafeArea = safeArea;
            _carriedHudScreenWidth = Screen.width;
            _carriedHudScreenHeight = Screen.height;
            Vector2 anchor = new Vector2(safeArea.xMin / Mathf.Max(1, Screen.width),
                safeArea.yMin / Mathf.Max(1, Screen.height));
            _carriedHud.anchorMin = _carriedHud.anchorMax = anchor;
            _carriedHud.anchoredPosition = new Vector2(26f, 52f);
        }
    }
}
