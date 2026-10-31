using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lighthouse.Common.UI
{
    // Texts are saved with the built-in LegacyRuntime font, which has no Hangul. Windows then draws Hangul with a
    // fallback font whose small sizes are pixel bitmaps, so labels look broken. On every scene load this swaps that
    // font on each Text in the scene (inactive ones too) for one Malgun Gothic dynamic font from the OS. Texts with
    // any other font are left alone. UI spawned later keeps the built-in font unless its screen swaps it itself.
    public static class KoreanUiFont
    {
        private static readonly string[] OsFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private static Font _builtinFont;
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (!_font)
                {
                    _font = Font.CreateDynamicFontFromOSFont(OsFontNames, 32);
                }

                return _font;
            }
        }

        public static void Apply(Scene scene)
        {
            if (!_builtinFont)
            {
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Text[] texts = roots[i].GetComponentsInChildren<Text>(true);

                for (int j = 0; j < texts.Length; j++)
                {
                    if (texts[j].font == _builtinFont)
                    {
                        texts[j].font = Font;
                    }
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // Static state survives play mode when domain reload is off.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _font = null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply(scene);
        }
    }
}
