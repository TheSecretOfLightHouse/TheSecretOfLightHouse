using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Lays the workbench screen out like the 2026-10-21 mockup: lighthouse and boat sections in a scrolling recipe
    // list, a carried module bar with put back, and a take button under each stored module. New pieces are copies of
    // the screen's own header, button and slot so the look stays the same. Running it again skips what is there.
    public static class WorkbenchScreenCarryLayout
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen.prefab";
        private const string LayoutPath = "Assets/02.Prefabs/Lighthouse/Data/Trial/TowerLayout_JH04Trial.asset";
        private const string RowSpritePath = "Assets/03.Images/WorkbenchUi/wb_row.png";
        private const float FrameHeight = 960f;
        private const float PanelHeight = 736f;
        private const float StoredCellHeight = 120f;

        private static readonly Color LightText = new Color(0.8627451f, 0.83137256f, 0.7607843f);
        private static readonly Color DimText = new Color(0.54901963f, 0.59607846f, 0.6745098f);

        [MenuItem("Lighthouse/Workbench/Apply Carry Layout To Screen Prefab")]
        public static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                WorkbenchScreen screen = root.GetComponent<WorkbenchScreen>();
                Transform frame = root.transform.Find("Window/Frame");

                if (!screen || !frame)
                {
                    Debug.LogError($"[{nameof(WorkbenchScreenCarryLayout)}] {PrefabPath} is not the workbench screen.");
                    return;
                }

                // Taller window so the stock panel fits the carry bar and two rows of stored modules.
                SetHeight(frame, FrameHeight);
                SetHeight(frame.Find("Recipes"), PanelHeight);
                SetHeight(frame.Find("Progress"), PanelHeight);
                SetHeight(frame.Find("Stock"), PanelHeight);

                Button buttonSource = frame.Find("Close").GetComponent<Button>();
                Text sectionTemplate = BuildRecipeScroll(frame.Find("Recipes"));
                CarryBar bar = BuildStock(frame.Find("Stock"), buttonSource);

                SerializedObject serialized = new SerializedObject(screen);
                serialized.FindProperty("_carryBar").objectReferenceValue = bar.Root;
                serialized.FindProperty("_carryIcon").objectReferenceValue = bar.Icon;
                serialized.FindProperty("_carryText").objectReferenceValue = bar.Text;
                serialized.FindProperty("_returnButton").objectReferenceValue = bar.ReturnButton;
                serialized.FindProperty("_sectionHeaderTemplate").objectReferenceValue = sectionTemplate;
                serialized.FindProperty("_towerLayout").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TowerLayout>(LayoutPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(WorkbenchScreenCarryLayout)}] {PrefabPath} updated.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Eight recipes and two headings no longer fit, so the list scrolls inside a masked viewport.
        private static Text BuildRecipeScroll(Transform recipes)
        {
            Transform viewport = recipes.Find("Viewport");
            Transform list = viewport ? viewport.Find("List") : recipes.Find("List");

            if (!viewport)
            {
                RectTransform listRect = (RectTransform)list;
                RectTransform viewportRect = CreateRect("Viewport", recipes);
                CopyRect(listRect, viewportRect);
                viewportRect.SetSiblingIndex(listRect.GetSiblingIndex());
                viewportRect.gameObject.AddComponent<RectMask2D>();
                list.SetParent(viewportRect, false);
                listRect.anchorMin = new Vector2(0f, 1f);
                listRect.anchorMax = Vector2.one;
                listRect.pivot = new Vector2(0.5f, 1f);
                listRect.anchoredPosition = Vector2.zero;
                listRect.sizeDelta = Vector2.zero;
                ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect scroll = viewportRect.gameObject.AddComponent<ScrollRect>();
                scroll.content = listRect;
                scroll.viewport = viewportRect;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 30f;
            }

            Transform existing = list.Find("SectionHeader");

            if (existing)
            {
                return existing.GetComponent<Text>();
            }

            Text header = Object.Instantiate(recipes.Find("Header").GetComponent<Text>(), list, false);
            header.name = "SectionHeader";
            ((RectTransform)header.transform).sizeDelta = new Vector2(0f, 40f);
            header.gameObject.SetActive(false);
            return header;
        }

        private static CarryBar BuildStock(Transform stock, Button buttonSource)
        {
            // Stock children in order: resource header, resource grid, stored header, stored grid.
            Text resourceHeader = stock.GetChild(0).GetComponent<Text>();
            Text storedHeader = stock.GetChild(2).GetComponent<Text>();
            storedHeader.text = "보관함";
            SetTop(storedHeader.transform, -318f);
            RectTransform storedGrid = (RectTransform)stock.Find("StoredGrid");
            SetTop(storedGrid, -360f);
            storedGrid.sizeDelta = new Vector2(storedGrid.sizeDelta.x, StoredCellHeight * 2f + 8f);
            GridLayoutGroup grid = storedGrid.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(grid.cellSize.x, StoredCellHeight);

            for (int i = 0; i < storedGrid.childCount; i++)
            {
                BuildStoredSlot(storedGrid.GetChild(i).GetComponent<WorkbenchItemSlot>(), resourceHeader, buttonSource);
            }

            CarryBar bar = BuildCarryBar(stock, resourceHeader, buttonSource);

            if (!stock.Find("CarryNote"))
            {
                Text note = CreateText("CarryNote", stock, resourceHeader, "모듈은 한 번에 1개만 들 수 있습니다.", 14, DimText, TextAnchor.MiddleCenter);
                RectTransform rect = (RectTransform)note.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-32f, 22f);
                rect.anchoredPosition = new Vector2(0f, -292f);
            }

            return bar;
        }

        private static CarryBar BuildCarryBar(Transform stock, Text textSource, Button buttonSource)
        {
            Transform existing = stock.Find("CarryBar");

            if (existing)
            {
                return new CarryBar(existing.gameObject, existing.Find("Icon").GetComponent<Image>(),
                    existing.Find("Text").GetComponent<Text>(), existing.Find("Return").GetComponent<Button>());
            }

            RectTransform bar = CreateRect("CarryBar", stock);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(-32f, 52f);
            bar.anchoredPosition = new Vector2(0f, -236f);
            Image background = bar.gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RowSpritePath);
            background.type = Image.Type.Sliced;

            RectTransform icon = CreateRect("Icon", bar);
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(40f, 40f);
            icon.anchoredPosition = new Vector2(30f, 0f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;

            Text text = CreateText("Text", bar, textSource, "들고 있는 모듈: 없음", 17, LightText, TextAnchor.MiddleLeft);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(58f, 0f);
            textRect.offsetMax = new Vector2(-124f, 0f);

            Button returnButton = CreateButton("Return", bar, buttonSource, "되돌려놓기", 16);
            RectTransform buttonRect = (RectTransform)returnButton.transform;
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.sizeDelta = new Vector2(112f, 38f);
            buttonRect.anchoredPosition = new Vector2(-8f, 0f);
            return new CarryBar(bar.gameObject, iconImage, text, returnButton);
        }

        // Name on top, icon in the middle, count above a take button at the bottom.
        private static void BuildStoredSlot(WorkbenchItemSlot slot, Text textSource, Button buttonSource)
        {
            Transform cell = slot.transform;

            if (cell.Find("Take"))
            {
                return;
            }

            RectTransform icon = (RectTransform)cell.Find("Icon");
            icon.sizeDelta = new Vector2(46f, 46f);
            icon.anchoredPosition = new Vector2(0f, 8f);
            RectTransform count = (RectTransform)cell.Find("Count");
            count.anchoredPosition = new Vector2(count.anchoredPosition.x, 32f);

            Text name = CreateText("Name", cell, textSource, string.Empty, 13, LightText, TextAnchor.UpperCenter);
            RectTransform nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = Vector2.one;
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(-6f, 30f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);

            Button take = CreateButton("Take", cell, buttonSource, "가져가기", 14);
            RectTransform takeRect = (RectTransform)take.transform;
            takeRect.anchorMin = Vector2.zero;
            takeRect.anchorMax = new Vector2(1f, 0f);
            takeRect.pivot = new Vector2(0.5f, 0f);
            takeRect.sizeDelta = new Vector2(-8f, 26f);
            takeRect.anchoredPosition = new Vector2(0f, 4f);

            SerializedObject serialized = new SerializedObject(slot);
            serialized.FindProperty("_name").objectReferenceValue = name;
            serialized.FindProperty("_actionButton").objectReferenceValue = take;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateButton(string name, Transform parent, Button source, string label, int fontSize)
        {
            Button button = Object.Instantiate(source, parent, false);
            button.name = name;
            Text text = button.GetComponentInChildren<Text>(true);
            text.text = label;
            text.fontSize = fontSize;
            return button;
        }

        // Copies the source text's font and settings, then sets what differs.
        private static Text CreateText(string name, Transform parent, Text source, string content, int fontSize, Color color, TextAnchor alignment)
        {
            Text text = Object.Instantiate(source, parent, false);
            text.name = name;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
        }

        private static void SetHeight(Transform target, float height)
        {
            RectTransform rect = (RectTransform)target;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static void SetTop(Transform target, float y)
        {
            RectTransform rect = (RectTransform)target;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private readonly struct CarryBar
        {
            public readonly GameObject Root;
            public readonly Image Icon;
            public readonly Text Text;
            public readonly Button ReturnButton;

            public CarryBar(GameObject root, Image icon, Text text, Button returnButton)
            {
                Root = root;
                Icon = icon;
                Text = text;
                ReturnButton = returnButton;
            }
        }
    }
}
