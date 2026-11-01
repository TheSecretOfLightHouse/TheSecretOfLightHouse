using Lighthouse.Mafia.IdentityCheck.View;
using Lighthouse.Mafia.Voting.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.Editor
{
    // Builds the mafia's identity check and summon UI from the 2026-10-23 sketch. The confirm and notice boxes reuse the
    // vote confirm box (board, cancel button) so both read as one family; the accept button is the vote one
    // with its wording erased, and the label is live text. Kept beside the vote builder to share its editor assembly.
    public static class IdentityActionScreenSketchSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Voting/IdentityActionScreen_Sketch.prefab";
        private const string Folder = "Assets/03.Images/IdentityUiSketch/";
        private const string VoteFolder = "Assets/03.Images/VotingUiSketch/";
        private const float ButtonWidth = 300f;
        private const float ButtonHeight = 78f;
        // The check and summon buttons are drawn at 0.9 times the sketch, kept to the group's right edge.
        private const float ButtonScale = 0.9f;
        // The reveal banner sketch is 861x306; it hangs under the day board (8 + 186 at the top centre).
        private const float RevealWidth = 861f;
        private const float RevealHeight = 306f;
        private const float RevealScale = 0.72f;
        private const float RevealTop = 200f;
        private const float RevealBustScale = 0.95f;
        private static readonly Color Cream = new Color(0.98f, 0.92f, 0.79f);
        private static readonly Color Sand = new Color(0.86f, 0.77f, 0.63f);
        private static readonly Color Muted = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color Gold = new Color(0.96f, 0.78f, 0.29f);
        private static readonly Color Dark = new Color(0.12f, 0.09f, 0.05f);

        [MenuItem("Lighthouse/Voting/Build Identity Action Screen Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(IdentityActionScreenSketchSetup)}] Stop play mode before building.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("IdentityActionScreen_Sketch", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Above the vote screen (75), whose window sets the shared overlay flag that keeps these boxes shut.
                canvas.sortingOrder = 76;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                IdentityActionScreen screen = root.AddComponent<IdentityActionScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                BuildButtons(root.transform, serialized);
                BuildConfirm(root.transform, serialized);
                BuildNotice(root.transform, serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                BuildRevealBanner(root.transform);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(IdentityActionScreenSketchSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Voting/Use Identity Action Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            VotingNetworkController[] controllers = Object.FindObjectsByType<VotingNetworkController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || controllers.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(IdentityActionScreenSketchSetup)}] Open a scene with one voting controller in edit mode and build the prefab first.");
                return;
            }

            IdentityActionScreen screen = null;

            foreach (IdentityActionScreen existing in Object.FindObjectsByType<IdentityActionScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    screen = existing;
                }
            }

            if (!screen)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add identity action screen");
                screen = instance.GetComponent<IdentityActionScreen>();
            }

            SerializedObject links = new SerializedObject(screen);
            Set(links, "_voting", controllers[0]);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = screen.gameObject;
            Debug.Log($"[{nameof(IdentityActionScreenSketchSetup)}] Identity action screen connected. Save the scene to keep it.");
        }

        // Stacked over the vote button at the lower right (that button is 258x108 at 1.2 times, 55 from the bottom).
        private static void BuildButtons(Transform root, SerializedObject serialized)
        {
            float width = ButtonWidth * ButtonScale;
            float height = ButtonHeight * ButtonScale;
            float x = ButtonWidth - width;
            float summonY = 32f + height + 8f * ButtonScale;
            RectTransform group = Rect("Buttons", root, 0f, 0f, ButtonWidth, summonY + height + 4f);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(1f, 0f);
            group.anchoredPosition = new Vector2(-28f, 196f);

            Text status = Label("Status", group, string.Empty, 18, 0f, 0f, ButtonWidth, 28f, TextAnchor.MiddleRight);
            status.supportRichText = true;
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            Button check = SwapButton("Check", group, Folder, "id_check", "id_check_hover", x, 32f, width, height);
            Button summon = SwapButton("Summon", group, Folder, "id_summon", "id_summon_hover", x, summonY, width, height);

            Set(serialized, "_hud", group.gameObject);
            Set(serialized, "_checkButton", check);
            Set(serialized, "_summonButton", summon);
            Set(serialized, "_status", status);
            BuildSummonCue(root, summon);
            group.gameObject.SetActive(false);
        }

        // The summon right's cue pulses the summon button and posts its lines on the shared ScreenNotice strip
        // (2026-10-30), so it only needs the button and a sound source.
        private static void BuildSummonCue(Transform root, Button summon)
        {
            RectTransform button = (RectTransform)summon.transform;
            CenterPivot(button);

            RectTransform holder = Rect("SummonCue", root, 0f, 0f, 0f, 0f);
            Stretch(holder);
            SummonRightCue cue = holder.gameObject.AddComponent<SummonRightCue>();
            AudioSource audio = holder.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            SerializedObject serialized = new SerializedObject(cue);
            Set(serialized, "_summonButton", button);
            Set(serialized, "_audio", audio);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Same board and button places as the vote confirm box, without the sailor picture (removed 2026-10-29).
        private static void BuildConfirm(Transform root, SerializedObject serialized)
        {
            Image board = Box("Confirm", root, 640f, 420f, out RectTransform confirm);
            Text title = Label("Title", board.transform, string.Empty, 30, 40f, 28f, 560f, 44f);
            title.color = Gold;
            Text message = Label("Message", board.transform, string.Empty, 30, 40f, 86f, 560f, 60f);
            Text detail = Label("Detail", board.transform, string.Empty, 24, 40f, 160f, 560f, 36f);
            detail.supportRichText = true;
            Text warning = Label("Warning", board.transform, string.Empty, 17, 60f, 206f, 520f, 56f);
            warning.color = Muted;

            Button cancel = SwapButton("Cancel", board.transform, VoteFolder, "vt_button_cancel", "vt_button_cancel_hover", 104f, 300f, 199f, 67f);
            Button accept = SwapButton("Accept", board.transform, Folder, "id_button_accept", "id_button_accept_hover", 339f, 301f, 195f, 65f);
            Text acceptLabel = ButtonLabel(accept.transform, 195f, 65f);

            Set(serialized, "_confirm", confirm.gameObject);
            Set(serialized, "_confirmTitle", title);
            Set(serialized, "_confirmMessage", message);
            Set(serialized, "_confirmDetail", detail);
            Set(serialized, "_confirmWarning", warning);
            Set(serialized, "_acceptButton", accept);
            Set(serialized, "_acceptLabel", acceptLabel);
            Set(serialized, "_cancelButton", cancel);
            confirm.gameObject.SetActive(false);
        }

        private static void BuildNotice(Transform root, SerializedObject serialized)
        {
            Image board = Box("Notice", root, 640f, 360f, out RectTransform notice);
            Text title = Label("Title", board.transform, string.Empty, 30, 40f, 30f, 560f, 44f);
            title.color = Gold;
            Text message = Label("Message", board.transform, string.Empty, 26, 50f, 96f, 540f, 110f);
            Button close = SwapButton("Close", board.transform, Folder, "id_button_accept", "id_button_accept_hover", 222f, 252f, 195f, 65f);
            Text closeLabel = ButtonLabel(close.transform, 195f, 65f);
            closeLabel.text = "확인";

            Set(serialized, "_notice", notice.gameObject);
            Set(serialized, "_noticeTitle", title);
            Set(serialized, "_noticeMessage", message);
            Set(serialized, "_noticeButton", close);
            notice.gameObject.SetActive(false);
        }

        // The failed check banner from the 2026-10-29 sketch, laid out in the sketch's own pixels (861x306) and scaled
        // down as one group under the day board at the top centre. Parts sit where they were cut from the sketch; the
        // ones that pop or stamp get a centre pivot. Last child, so it draws over the boxes.
        private static void BuildRevealBanner(Transform root)
        {
            RectTransform holder = Rect("RevealBanner", root, 0f, 0f, 0f, 0f);
            Stretch(holder);
            InnocentRevealBanner banner = holder.gameObject.AddComponent<InnocentRevealBanner>();
            SerializedObject serialized = new SerializedObject(banner);

            RectTransform group = Rect("Banner", holder, 0f, 0f, RevealWidth, RevealHeight);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(0.5f, 1f);
            group.anchoredPosition = new Vector2(0f, -RevealTop);
            group.localScale = Vector3.one * RevealScale;
            CanvasGroup groupFade = Fade(group);

            Image strip = Picture("Strip", group, Folder, "ir_strip", 16f, 26f, 806f, 238f);
            strip.type = Image.Type.Filled;
            strip.fillMethod = Image.FillMethod.Horizontal;
            strip.fillOrigin = (int)Image.OriginHorizontal.Left;

            Image frame = Picture("Frame", group, Folder, "ir_frame", 52f, 32f, 231f, 229f);
            CenterPivot(frame.rectTransform);
            Image mask = Picture("PortraitMask", frame.transform, Folder, "ir_portrait_mask", 18f, 18f, 195f, 193f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image portrait = Picture("Portrait", mask.transform, VoteFolder, "vt_job_mechanic", 0f, 0f,
                195f * RevealBustScale, 195f * RevealBustScale);
            RectTransform bust = portrait.rectTransform;
            bust.anchorMin = bust.anchorMax = bust.pivot = new Vector2(0.5f, 0f);
            bust.anchoredPosition = Vector2.zero;
            portrait.preserveAspect = true;

            Text title = Label("Title", group, "정체 확인 실패", 24, 312f, 82f, 320f, 34f, TextAnchor.MiddleLeft);
            title.color = Sand;
            Text name = Label("Name", group, string.Empty, 48, 308f, 114f, 300f, 70f, TextAnchor.MiddleLeft);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 30;
            name.resizeTextMaxSize = 48;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            Text message = Label("Message", group, "이 선원은 마피아가 아닙니다.", 26, 312f, 186f, 480f, 38f, TextAnchor.MiddleLeft);

            Image badge = Picture("Badge", group, Folder, "ir_badge", 591f, 126f, 159f, 48f);
            CenterPivot(badge.rectTransform);
            Text badgeLabel = Label("Label", badge.transform, "무고한 시민", 25, 0f, 0f, 159f, 48f);
            badgeLabel.color = Dark;
            badgeLabel.GetComponent<Outline>().effectColor = Color.clear;

            Image sparkleTop = Picture("SparkleTop", group, Folder, "ir_sparkle_top", 276f, 20f, 30f, 40f);
            CenterPivot(sparkleTop.rectTransform);
            Image sparkleBottom = Picture("SparkleBottom", group, Folder, "ir_sparkle_bottom", 274f, 214f, 32f, 50f);
            CenterPivot(sparkleBottom.rectTransform);

            Set(serialized, "_banner", group);
            Set(serialized, "_soundManagerPrefab", AssetDatabase.LoadAssetAtPath<Lighthouse.Systems.Audio.SoundManager>(
                "Assets/02.Prefabs/Player/PlayerSoundManager.prefab"));
            Set(serialized, "_revealClip", AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/08.Audio/IdentityCheck/Ambience_Clock_Strike_Loop_First4Seconds.wav"));
            Set(serialized, "_bannerGroup", groupFade);
            Set(serialized, "_frame", frame.rectTransform);
            Set(serialized, "_frameGroup", Fade(frame.rectTransform));
            Set(serialized, "_portrait", portrait);
            Set(serialized, "_strip", strip);
            Set(serialized, "_title", Fade(title.rectTransform));
            Set(serialized, "_name", name);
            Set(serialized, "_nameGroup", Fade(name.rectTransform));
            Set(serialized, "_message", Fade(message.rectTransform));
            Set(serialized, "_badge", badge.rectTransform);
            Set(serialized, "_badgeGroup", Fade(badge.rectTransform));
            SerializedProperty sparkles = serialized.FindProperty("_sparkles");
            sparkles.arraySize = 2;
            sparkles.GetArrayElementAtIndex(0).objectReferenceValue = sparkleTop.rectTransform;
            sparkles.GetArrayElementAtIndex(1).objectReferenceValue = sparkleBottom.rectTransform;
            Set(serialized, "_navigatorPortrait", Load(VoteFolder, "vt_job_navigator"));
            Set(serialized, "_mechanicPortrait", Load(VoteFolder, "vt_job_mechanic"));
            Set(serialized, "_guardPortrait", Load(VoteFolder, "vt_job_guard"));
            Set(serialized, "_gathererPortrait", Load(VoteFolder, "vt_job_gatherer"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup Fade(RectTransform rect)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        // Keeps the place drawn in the sketch while letting the part scale around its own centre.
        private static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
        }

        private static Image Box(string name, Transform root, float width, float height, out RectTransform box)
        {
            box = Rect(name, root, 0f, 0f, 0f, 0f);
            Stretch(box);
            Image scrim = box.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            Image board = Picture("Board", box, VoteFolder, "vt_board", 0f, 0f, width, height);
            RectTransform rect = board.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            board.raycastTarget = true;
            return board;
        }

        private static Text ButtonLabel(Transform button, float width, float height)
        {
            Text label = Label("Label", button, string.Empty, 24, 0f, 0f, width, height);
            label.color = Dark;
            label.GetComponent<Outline>().effectColor = Color.clear;
            return label;
        }

        // The sketch draws a normal and a hover look for each button, so they swap sprites instead of tinting.
        private static Button SwapButton(string name, Transform parent, string folder, string normal, string hover,
            float x, float y, float width, float height)
        {
            Image image = Picture(name, parent, folder, normal, x, y, width, height);
            image.raycastTarget = true;
            image.preserveAspect = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = Load(folder, hover);
            state.pressedSprite = Load(folder, hover);
            state.selectedSprite = Load(folder, normal);
            button.spriteState = state;
            return button;
        }

        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
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
                importer.spriteBorder = Vector4.zero;
                importer.SaveAndReimport();
            }
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
            image.sprite = Load(folder, sprite);
            image.type = image.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
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
    }
}
