using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    // Keep these anchors and display references when replacing the island's art.
    [DisallowMultipleComponent]
    public sealed class MainIslandPresentation : MonoBehaviour
    {
        [SerializeField] private Transform _workbenchAnchor;
        [SerializeField] private Transform _towerAnchor;
        [SerializeField] private GameObject[] _slotVisuals = new GameObject[0];
        [SerializeField] private GameObject _completeVisual;

        [SerializeField] private Transform[] _boatSpawnPoints = new Transform[0];

        [SerializeField] private Transform[] _playerSpawnPoints = new Transform[0];
        public Transform[] PlayerSpawnPoints => _playerSpawnPoints;
        public Transform[] BoatSpawnPoints => _boatSpawnPoints;

        public Transform WorkbenchAnchor => _workbenchAnchor;
        public Transform TowerAnchor => _towerAnchor;
        public GameObject[] SlotVisuals => _slotVisuals;
        public GameObject CompleteVisual => _completeVisual;

        public bool IsValid => _workbenchAnchor && _towerAnchor && _completeVisual
            && _slotVisuals != null && _slotVisuals.Length > 0;
    }
}
