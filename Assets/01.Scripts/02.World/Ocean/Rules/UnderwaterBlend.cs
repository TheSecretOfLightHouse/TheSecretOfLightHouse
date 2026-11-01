using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public struct UnderwaterBlend
    {
        private bool _isUnder;
        private float _weight;

        public bool IsUnder => _isUnder;
        public float Weight => _weight;

        public void Step(
            float depth,
            float enterDepth,
            float exitDepth,
            float blendSeconds,
            float deltaTime)
        {
            if (_isUnder)
            {
                _isUnder = depth >= exitDepth;
            }
            else
            {
                _isUnder = depth > enterDepth;
            }

            float step = blendSeconds > 0f ? deltaTime / blendSeconds : 1f;
            _weight = Mathf.MoveTowards(_weight, _isUnder ? 1f : 0f, step);
        }

        public void Reset()
        {
            _isUnder = false;
            _weight = 0f;
        }
    }
}
