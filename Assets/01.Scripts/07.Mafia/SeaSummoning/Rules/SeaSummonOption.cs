using System;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    [Serializable]
    public sealed class SeaSummonOption
    {
        [SerializeField] private string _monsterId;
        [SerializeField] private string _displayName;
        [SerializeField] private bool _isEnabled;
        public string MonsterId => _monsterId;
        public string DisplayName => _displayName;
        public bool IsEnabled => _isEnabled;

        public SeaSummonOption(string monsterId, string displayName, bool isEnabled)
        {
            _monsterId = monsterId;
            _displayName = displayName;
            _isEnabled = isEnabled;
        }
    }
}
