using Mirror;
using Lighthouse.Monsters.AI.Rules;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class AnglerDisguiseView : NetworkBehaviour
    {
        [SerializeField] private GameObject _boatVisual;
        [SerializeField] private GameObject _fishVisual;
        [SerializeField, Min(0.01f)] private float _revealDistance = 12f;
        private bool _hasPresentation;
        private bool _isRevealed;

        public bool IsRevealed => _hasPresentation && _isRevealed;
        public float RevealDistance => _revealDistance;

        private void Awake()
        {
            SetPresentation(false, false);
        }

        private void LateUpdate()
        {
            if (!isClient || !NetworkClient.ready || NetworkClient.localPlayer == null)
            {
                SetPresentation(false, false);
                return;
            }
            Vector3 position = NetworkClient.localPlayer.transform.position;
            bool reveal = ShouldReveal(position);
            SetPresentation(true, reveal);
        }

        private void OnDisable()
        {
            SetPresentation(false, false);
        }

        public bool ShouldReveal(Vector3 observerPosition)
        {
            return AnglerDisguiseRules.ShouldReveal(transform.position, observerPosition, _revealDistance);
        }

        private void SetPresentation(bool visible, bool revealed)
        {
            _hasPresentation = visible;
            _isRevealed = revealed;
            // 관찰자마다 외형만 전환하며, 정체 공개 상태를 공유하거나 실제 플레이어 신원을 부여하지 않는다.
            if (_boatVisual != null && _boatVisual.activeSelf != (visible && !revealed))
            {
                _boatVisual.SetActive(visible && !revealed);
            }
            if (_fishVisual != null && _fishVisual.activeSelf != (visible && revealed))
            {
                _fishVisual.SetActive(visible && revealed);
            }
        }
    }
}
