using System;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.Workbench.View
{
    // How one deposited unit of a resource looks on the workbench rack.
    [Serializable]
    public struct RackPieceStyle
    {
        [SerializeField]
        private ResourceDefinition _resource;
        [SerializeField]
        private RackPieceShape _shape;
        [Tooltip("Meters. x = across the shelf, y = up, z = front to back.")]
        [SerializeField]
        private Vector3 _size;
        [SerializeField]
        private Color _color;

        public int ResourceId => _resource ? _resource.Id : 0;
        public RackPieceShape Shape => _shape;
        public Vector3 Size => _size;
        public Color Color => _color;
    }
}
