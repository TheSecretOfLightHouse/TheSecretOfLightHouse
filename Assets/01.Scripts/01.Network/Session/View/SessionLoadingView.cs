using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // Shown while every client loads the game scene, and briefly while a match closes.
    [DisallowMultipleComponent]
    public sealed class SessionLoadingView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _spinner;
        [SerializeField]
        private Text _title;
        [SerializeField]
        private Text _detail;
        [SerializeField]
        private float _spinDegreesPerSecond = 70f;
        [Tooltip("Optional bar showing arrived crew / all crew: a Filled image, or a sliced one anchored to the left of its track. Its root is hidden while closing.")]
        [SerializeField]
        private Image _progress;
        [SerializeField]
        private GameObject _progressRoot;

        private int _shownLoaded = -1;
        private int _shownPlayers = -1;

        private void OnEnable()
        {
            _shownLoaded = -1;
            _shownPlayers = -1;
        }

        private void Update()
        {
            if (_spinner)
            {
                _spinner.Rotate(0f, 0f, -_spinDegreesPerSecond * Time.unscaledDeltaTime);
            }
        }

        public void ShowLoading(int loaded, int players)
        {
            if (loaded == _shownLoaded && players == _shownPlayers)
            {
                return;
            }

            _shownLoaded = loaded;
            _shownPlayers = players;
            _title.text = "출항 준비 중";
            _detail.text = players > 0 ? $"선원 도착 {loaded} / {players}" : "바다로 나가는 중…";
            SetProgress(true, players > 0 ? (float)loaded / players : 0f);
        }

        public void ShowClosing()
        {
            if (_shownPlayers == -2)
            {
                return;
            }

            _shownPlayers = -2;
            _title.text = "판 종료 중";
            _detail.text = "첫 화면으로 돌아갑니다…";
            SetProgress(false, 0f);
        }

        private void SetProgress(bool isShown, float amount)
        {
            if (_progressRoot)
            {
                _progressRoot.SetActive(isShown);
            }

            if (!_progress)
            {
                return;
            }

            amount = Mathf.Clamp01(amount);

            if (_progress.type == Image.Type.Filled)
            {
                _progress.fillAmount = amount;
                return;
            }

            // A sliced bar keeps its rounded ends by changing width instead of being cropped.
            RectTransform bar = _progress.rectTransform;
            bar.anchorMax = new Vector2(amount, bar.anchorMax.y);
            _progress.enabled = amount > 0f;
        }
    }
}
