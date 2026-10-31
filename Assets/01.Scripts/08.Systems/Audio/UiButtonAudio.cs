using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Lighthouse.Systems.Audio
{
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class UiButtonAudio : MonoBehaviour
    {
        private const string ResourcePath = "Audio/UiButtonAudio";
        private static readonly Predicate<UnityEngine.UI.Button> DestroyedButton = IsDestroyed;

        [SerializeField] private AudioClip _clickClip;
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        private readonly HashSet<UnityEngine.UI.Button> _buttons = new HashSet<UnityEngine.UI.Button>();
        private UnityEngine.UI.Selectable[] _selectables = new UnityEngine.UI.Selectable[256];
        private UnityAction _clicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            UiButtonAudio prefab = Resources.Load<UiButtonAudio>(ResourcePath);
            if (prefab != null)
            {
                Instantiate(prefab);
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _clicked = HandleClicked;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            BindButtons();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            foreach (UnityEngine.UI.Button button in _buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(_clicked);
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindButtons();
        }

        private void HandleClicked()
        {
            if (SoundManager.Instance == null && _soundManagerPrefab != null)
            {
                Instantiate(_soundManagerPrefab);
            }
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayEffect(_clickClip, _volume);
            }
        }

        private void BindButtons()
        {
            _buttons.RemoveWhere(DestroyedButton);
            int count = UnityEngine.UI.Selectable.allSelectableCount;
            if (count > _selectables.Length)
            {
                Array.Resize(ref _selectables, Mathf.NextPowerOfTwo(count));
            }
            count = UnityEngine.UI.Selectable.AllSelectablesNoAlloc(_selectables);
            for (int i = 0; i < count; i++)
            {
                if (_selectables[i] is UnityEngine.UI.Button button && _buttons.Add(button))
                {
                    // Bind the accepted click event, so disabled buttons and cancelled presses stay silent.
                    button.onClick.AddListener(_clicked);
                }
                _selectables[i] = null;
            }
        }

        private static bool IsDestroyed(UnityEngine.UI.Button button)
        {
            return button == null;
        }
    }
}
