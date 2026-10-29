using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.Editor
{
    // Builds the secret vote UI from the 2026-10-22 sketches: the vote screen on its board (positions are its pixels at
    // 1024x852), the confirm box and the in-game button. Fixed wording stays in the sprites; names and counts are live text.
    public static class VotingScreenSketchSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Voting/VotingScreen_Sketch.prefab";
        private const string Folder = "Assets/03.Images/VotingUiSketch/";
        private const string KeyFolder = "Assets/03.Images/WorkbenchUiSketch/";
        private const float CardWidth = 290f;
        private const float CardHeight = 400f;
        private const float CardGap = 21f;
        // The job picture fills the card above the name; the check draws over it.
        private const float PortraitSize = 200f;
        // The bust stands on the frame's bottom edge so its flat cut hides under the border.
        private const float BustScale = 0.92f;
        private static readonly Color Cream = new Color(0.98f, 0.92f, 0.79f);
        private static readonly Color Muted = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color Dark = new Color(0.12f, 0.09f, 0.05f);

        [MenuItem("Lighthouse/Voting/Build Sketch Screen Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(VotingScreenSketchSetup)}] Stop play mode before building.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("VotingScreen_Sketch", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 75;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                VotingScreen screen = root.AddComponent<VotingScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                BuildHud(root.transform, serialized);
                BuildWindow(root.transform, serialized);
                BuildConfirm(root.transform, serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(VotingScreenSketchSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Voting/Use Sketch Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            VotingNetworkController[] controllers = Object.FindObjectsByType<VotingNetworkController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || controllers.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(VotingScreenSketchSetup)}] Open a scene with one voting controller in edit mode and build the prefab first.");
                return;
            }

            VotingScreen screen = null;

            foreach (VotingScreen existing in Object.FindObjectsByType<VotingScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    screen = existing;
                }
            }

            if (!screen)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add secret vote screen");
                screen = instance.GetComponent<VotingScreen>();
            }

            SerializedObject links = new SerializedObject(screen);
            Set(links, "_voting", controllers[0]);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = screen.gameObject;
            Debug.Log($"[{nameof(VotingScreenSketchSetup)}] Vote screen connected. Save the scene to keep it.");
        }

        // The sketch's own in-game button at the lower right. Its bottom lines up with the role/boat panel at the lower
        // left (InGameHudSetup: 1080 - 893 x 1920/1672 = about 55).
        private static void BuildHud(Transform root, SerializedObject serialized)
        {
            Image panel = Picture("Hud", root, Folder, "vt_hud_panel", 0f, 0f, 258f, 108f);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-28f, 55f);
            // The sketch is drawn small; at 1920x1080 it reads at 1.2 times next to the in-game HUD.
            rect.localScale = new Vector3(1.2f, 1.2f, 1f);
            panel.raycastTarget = true;
            Button button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.82f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f);
            button.colors = colors;
            Text remaining = Label("Remaining", panel.transform, string.Empty, 15, 114f, 52f, 136f, 22f, TextAnchor.MiddleLeft);
            Text target = Label("Target", panel.transform, string.Empty, 13, 114f, 74f, 136f, 20f, TextAnchor.MiddleLeft);
            remaining.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.color = Muted;
            Keycap(panel.transform, "V", -6f, -6f, 30f);

            Set(serialized, "_hud", panel.gameObject);
            Set(serialized, "_hudButton", button);
            Set(serialized, "_hudRemaining", remaining);
            Set(serialized, "_hudTarget", target);
            panel.gameObject.SetActive(false);
        }

        private static void BuildWindow(Transform root, SerializedObject serialized)
        {
            RectTransform window = Rect("Window", root, 0f, 0f, 0f, 0f);
            Stretch(window);
            Image scrim = window.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform frame = Rect("Frame", window, 0f, 0f, 1024f, 852f);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;

            // The board stretches as a 9-slice so the close hint fits inside it too.
            Picture("Board", frame, Folder, "vt_window_board", 0f, 0f, 1024f, 872f);
            Picture("Title", frame, Folder, "vt_title", 62f, 69f, 397f, 87f);
            Picture("Subtitle", frame, Folder, "vt_subtitle", 134f, 158f, 410f, 36f);
            Picture("RemainingBox", frame, Folder, "vt_remain_box", 626f, 76f, 344f, 86f);
            Image used = Picture("Ballot1", frame, Folder, "vt_ballot_used", 641f, 99f, 54f, 44f);
            Image left = Picture("Ballot2", frame, Folder, "vt_ballot_left", 702f, 98f, 56f, 45f);
            Picture("RemainingLabel", frame, Folder, "vt_remain_label", 782f, 107f, 81f, 26f);
            Text remaining = Label("Remaining", frame, string.Empty, 30, 870f, 96f, 110f, 44f, TextAnchor.MiddleLeft);
            remaining.supportRichText = true;

            var cards = new VoteCandidateCard[3];
            float startX = (1024f - CardWidth * cards.Length - CardGap * (cards.Length - 1)) * 0.5f;

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = BuildCard(frame, i, startX + i * (CardWidth + CardGap), 213f);
            }

            Picture("Divider", frame, Folder, "vt_divider", 64f, 640f, 874f, 36f);
            Picture("Rules", frame, Folder, "vt_rules", 250f, 698f, 504f, 55f);
            Picture("Sparkle", frame, Folder, "vt_sparkle", 892f, 720f, 46f, 47f);
            Text notice = Label("Notice", frame, string.Empty, 20, 150f, 760f, 724f, 36f);
            Picture("CloseHint", frame, Folder, "vt_esc_close", 456f, 804f, 112f, 38f);

            Set(serialized, "_window", window.gameObject);
            SetArray(serialized, "_ballots", new Object[] { used, left });
            Set(serialized, "_ballotUsed", Load(Folder, "vt_ballot_used"));
            Set(serialized, "_ballotLeft", Load(Folder, "vt_ballot_left"));
            Set(serialized, "_remaining", remaining);
            SetArray(serialized, "_cards", cards);
            Set(serialized, "_notice", notice);
            Set(serialized, "_navigatorPortrait", Load(Folder, "vt_job_navigator"));
            Set(serialized, "_mechanicPortrait", Load(Folder, "vt_job_mechanic"));
            Set(serialized, "_guardPortrait", Load(Folder, "vt_job_guard"));
            Set(serialized, "_gathererPortrait", Load(Folder, "vt_job_gatherer"));
            window.gameObject.SetActive(false);
        }

        private static VoteCandidateCard BuildCard(Transform parent, int index, float x, float y)
        {
            Image frame = Picture($"Card{index + 1}", parent, Folder, "vt_card", x, y, CardWidth, CardHeight);
            Image portraitFrame = Picture("PortraitFrame", frame.transform, Folder, "vt_portrait_frame",
                (CardWidth - PortraitSize) * 0.5f, 22f, PortraitSize, PortraitSize);
            // The mask is the frame's inside, so the bust never covers the border.
            Image portraitMask = Picture("PortraitMask", portraitFrame.transform, Folder, "vt_portrait_mask",
                0f, 0f, PortraitSize, PortraitSize);
            portraitMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image portrait = Picture("Portrait", portraitMask.transform, Folder, "vt_job_mechanic",
                0f, 0f, PortraitSize * BustScale, PortraitSize * BustScale);
            RectTransform bust = portrait.rectTransform;
            bust.anchorMin = bust.anchorMax = bust.pivot = new Vector2(0.5f, 0f);
            bust.anchoredPosition = Vector2.zero;
            portrait.preserveAspect = true;
            portrait.gameObject.SetActive(false);
            // Mask treats any alpha above zero as inside, so the border is drawn again on top to keep its line clean.
            Picture("PortraitBorder", portraitFrame.transform, Folder, "vt_portrait_border", 0f, 0f, PortraitSize, PortraitSize);
            Text name = Label("Name", frame.transform, string.Empty, 26, 0f, 228f, CardWidth, 40f);
            Image buttonImage = Picture("Button", frame.transform, Folder, "vt_button_vote", 40f, 278f, 210f, 55f);
            buttonImage.raycastTarget = true;
            Button button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.93f, 0.7f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            button.colors = colors;
            Image badge = Picture("Badge", frame.transform, Folder, "vt_badge", 120f, 342f, 50f, 45f);
            Text number = Label("Number", badge.transform, string.Empty, 22, 0f, 0f, 50f, 45f);
            Image check = Picture("Check", frame.transform, Folder, "vt_check", 226f, 16f, 58f, 59f);

            VoteCandidateCard card = frame.gameObject.AddComponent<VoteCandidateCard>();
            SerializedObject serialized = new SerializedObject(card);
            Set(serialized, "_frame", frame);
            Set(serialized, "_name", name);
            Set(serialized, "_portrait", portrait);
            Set(serialized, "_button", button);
            Set(serialized, "_buttonImage", buttonImage);
            Set(serialized, "_badge", number);
            Set(serialized, "_check", check.gameObject);
            Set(serialized, "_normalFrame", Load(Folder, "vt_card"));
            Set(serialized, "_selectedFrame", Load(Folder, "vt_card_selected"));
            Set(serialized, "_voteButton", Load(Folder, "vt_button_vote"));
            Set(serialized, "_currentButton", Load(Folder, "vt_button_current"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // Sits over the vote screen: board, live question and the two buttons. The sketch's sailor in the lower-left
        // corner is left out (2026-10-29); the cards show the job pictures instead.
        private static void BuildConfirm(Transform root, SerializedObject serialized)
        {
            RectTransform confirm = Rect("Confirm", root, 0f, 0f, 0f, 0f);
            Stretch(confirm);
            Image scrim = confirm.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            Image board = Picture("Board", confirm, Folder, "vt_board", 0f, 0f, 640f, 420f);
            RectTransform boardRect = board.rectTransform;
            boardRect.anchorMin = boardRect.anchorMax = boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            board.raycastTarget = true;

            Picture("Title", board.transform, Folder, "vt_confirm_title", 259f, 34f, 121f, 35f);
            Text target = Label("Target", board.transform, string.Empty, 30, 40f, 86f, 560f, 92f);
            target.supportRichText = true;
            Text count = Label("Count", board.transform, string.Empty, 24, 40f, 186f, 560f, 36f);
            count.supportRichText = true;
            Text warning = Label("Warning", board.transform, "기회 회복 및 투표 철회 불가", 17, 40f, 224f, 560f, 28f);
            warning.color = Muted;

            Button cancel = SwapButton("Cancel", board.transform, "vt_button_cancel", "vt_button_cancel_hover", 104f, 300f, 199f, 67f);
            Button accept = SwapButton("Accept", board.transform, "vt_button_confirm", "vt_button_confirm_hover", 339f, 301f, 195f, 65f);

            Set(serialized, "_confirm", confirm.gameObject);
            Set(serialized, "_confirmTarget", target);
            Set(serialized, "_confirmCount", count);
            Set(serialized, "_confirmButton", accept);
            Set(serialized, "_cancelButton", cancel);
            confirm.gameObject.SetActive(false);
        }

        // The sketch draws a normal and a hover look for each button, so they swap sprites instead of tinting.
        private static Button SwapButton(string name, Transform parent, string normal, string hover, float x, float y, float width, float height)
        {
            Image image = Picture(name, parent, Folder, normal, x, y, width, height);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = Load(Folder, hover);
            state.pressedSprite = Load(Folder, hover);
            button.spriteState = state;
            return button;
        }

        // Card frames and the board stretch as 9-slices; the rest keep the sketch's size.
        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                // Mipmaps keep the drawn-in lettering readable when a small window shrinks these large pictures.
                importer.mipmapEnabled = true;
                importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.spriteBorder = file == "vt_window_board" ? new Vector4(60f, 60f, 60f, 60f)
                    : file == "vt_board" || file == "vt_remain_box" ? new Vector4(40f, 40f, 40f, 40f)
                    : file.StartsWith("vt_card") ? new Vector4(30f, 30f, 30f, 30f)
                    : Vector4.zero;
                importer.SaveAndReimport();
            }
        }

        private static Image Keycap(Transform parent, string key, float x, float y, float width)
        {
            Image image = Picture($"Key{key}", parent, KeyFolder, "ws_key", x, y, width, width);
            Text text = Label("Text", image.transform, key, 18, 1f, 0f, width - 2f, width - 2f);
            text.color = Cream;
            return image;
        }

        private static Text Label(string name, Transform parent, string caption, int size, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Cream;
            text.alignment = alignment;
            text.text = caption;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Dark.r, Dark.g, Dark.b, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static Image Picture(string name, Transform parent, string folder, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite == null ? null : Load(folder, sprite);
            image.type = image.sprite && image.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite Load(string folder, string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing sketch sprite: {folder}{name}");
            }

            return sprite;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing screen field: {name}");
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string name, Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
