using UnityEngine;

namespace Lighthouse.UI.SharedMap.Rules
{
    [CreateAssetMenu(menuName = "TheLightHouse/UI/Shared Map Settings")]
    public sealed class SharedMapSettings : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _editSeconds = 60f;
        [SerializeField, Min(1f)] private float _interactionRange = 3f;
        [SerializeField] private Vector2 _tableOffset = new Vector2(3f, 3f);
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _selectedButtonSprite;
        [SerializeField] private Sprite _lighthouseSprite;

        public float EditSeconds => _editSeconds;
        public float InteractionRange => _interactionRange;
        public Vector2 TableOffset => _tableOffset;
        public Sprite PanelSprite => _panelSprite;
        public Sprite ButtonSprite => _buttonSprite;
        public Sprite SelectedButtonSprite => _selectedButtonSprite;
        public Sprite LighthouseSprite => _lighthouseSprite;
    }
}
