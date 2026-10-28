using UnityEngine;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.Islands.View
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CoastalShelfView : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Mesh _mesh;
        [SerializeField, HideInInspector] private Mesh _rockMesh;
        [SerializeField, HideInInspector] private Mesh[] _rockContacts;
        [SerializeField, HideInInspector] private Mesh _groundMesh;
        [SerializeField, HideInInspector] private Mesh _originalGroundMesh;
        [SerializeField, HideInInspector] private Mesh _originalColliderMesh;
        [SerializeField, HideInInspector] private MeshFilter _groundFilter;
        [SerializeField, HideInInspector] private MeshCollider _groundCollider;
        [SerializeField, HideInInspector] private Renderer _dockConnection;
        [SerializeField, HideInInspector] private bool _wasDockConnectionEnabled;
        private int _coastSlot = -1;
        [SerializeField, HideInInspector] private Vector3[] _contour;
        [SerializeField, HideInInspector] private float _width;
        [SerializeField, HideInInspector] private Vector4 _profile;
        [SerializeField, HideInInspector] private float _lagoonReach;

        private void OnEnable()
        {
            if (_contour != null && _coastSlot < 0 && isActiveAndEnabled)
            {
                _coastSlot = CoastWaveRegistry.Register(_contour, _width, _profile, _lagoonReach);
            }
        }

        private void OnDisable()
        {
            CoastWaveRegistry.Unregister(_coastSlot);
            _coastSlot = -1;
        }

        private void OnDestroy()
        {
            if (_rockContacts != null)
            {
                foreach (Mesh contact in _rockContacts)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(contact);
                    }
                    else
                    {
                        DestroyImmediate(contact);
                    }
                }
            }
            if (_rockMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_rockMesh);
                }
                else
                {
                    DestroyImmediate(_rockMesh);
                }
            }
            CoastWaveRegistry.Unregister(_coastSlot);
            _coastSlot = -1;
            if (_dockConnection != null)
            {
                _dockConnection.enabled = _wasDockConnectionEnabled;
            }
            if (_groundMesh != null)
            {
                if (_groundFilter != null && _groundFilter.sharedMesh == _groundMesh)
                {
                    _groundFilter.sharedMesh = _originalGroundMesh;
                }
                if (_groundCollider != null && _groundCollider.sharedMesh == _groundMesh)
                {
                    _groundCollider.sharedMesh = _originalColliderMesh;
                }
                if (Application.isPlaying)
                {
                    Destroy(_groundMesh);
                }
                else
                {
                    DestroyImmediate(_groundMesh);
                }
            }
            if (_mesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_mesh);
                }
                else
                {
                    DestroyImmediate(_mesh);
                }
            }
        }

        public static bool Create(GameObject island, Material material, float width, Material rockMaterial = null,
            float lagoonRadiusScale = 0f, float lagoonMinReach = 0f)
        {
            if (island == null || material == null || width <= 0f)
            {
                return false;
            }

            MeshFilter ground = null;
            MeshFilter[] filters = island.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].name == "Ground_ContinuousClosed")
                {
                    ground = filters[i];
                    break;
                }
            }

            if (ground == null)
            {
                return false;
            }

            Vector3 center = ground.transform.TransformPoint(ground.sharedMesh.bounds.center);
            Vector3 beach = island.transform.forward;
            foreach (Transform child in island.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Walkable_Dock_Transition")
                {
                    beach = child.position - center;
                    beach.y = 0f;
                    beach.Normalize();
                    break;
                }
            }
            Vector4 profile = new Vector4(center.x, center.z, beach.x, beach.z);
            Mesh mesh = CoastalShelfMesh.Build(ground, island.transform, width, profile);
            if (mesh == null)
            {
                return false;
            }

            var shelf = new GameObject("CoastalShelf_Shore");
            shelf.layer = ground.gameObject.layer;
            shelf.transform.SetParent(island.transform, false);
            shelf.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = shelf.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshCollider collider = shelf.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            Collider groundCollider = ground.GetComponent<Collider>();
            if (groundCollider != null)
            {
                collider.sharedMaterial = groundCollider.sharedMaterial;
            }
            CoastalShelfView view = shelf.AddComponent<CoastalShelfView>();
            view._mesh = mesh;
            Vector3[] vertices = mesh.vertices;
            var contour = new Vector3[CoastWaveRegistry.Segments];
            for (int i = 0; i < contour.Length; i++)
            {
                contour[i] = shelf.transform.TransformPoint(vertices[CoastWaveRegistry.Segments + 1 + i]);
            }
            view._contour = contour;
            view._width = width;
            view._profile = profile;
            if (lagoonRadiusScale > 0f)
            {
                view._lagoonReach = CoastLagoonModel.ReachFor(CoastLagoonModel.EquivalentRadius(contour), lagoonRadiusScale, lagoonMinReach);
            }
            view._rockMesh = CoastalShelfMesh.BuildRockEdge(mesh, island.transform, width, profile, out view._rockContacts);
            var rocks = new GameObject("RockyCoast_Edge");
            rocks.layer = ground.gameObject.layer;
            rocks.transform.SetParent(shelf.transform, false);
            rocks.AddComponent<MeshFilter>().sharedMesh = view._rockMesh;
            rocks.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial != null ? rockMaterial : material;
            foreach (Mesh contact in view._rockContacts)
            {
                var boundary = new GameObject("RockContact");
                boundary.layer = ground.gameObject.layer;
                boundary.transform.SetParent(rocks.transform, false);
                MeshCollider rockCollider = boundary.AddComponent<MeshCollider>();
                rockCollider.sharedMesh = contact;
                rockCollider.convex = true;
            }
            view.OnEnable();
            MeshCollider sourceCollider = ground.GetComponent<MeshCollider>();
            if (sourceCollider != null)
            {
                view._groundMesh = DockSeabedMesh.Build(ground, island.transform, out Renderer connection);
                if (connection != null)
                {
                    view._dockConnection = connection;
                    view._wasDockConnectionEnabled = connection.enabled;
                    connection.enabled = true;
                }
                if (view._groundMesh != null)
                {
                    view._groundFilter = ground;
                    view._groundCollider = sourceCollider;
                    view._originalGroundMesh = ground.sharedMesh;
                    view._originalColliderMesh = sourceCollider.sharedMesh;
                    ground.sharedMesh = view._groundMesh;
                    sourceCollider.sharedMesh = view._groundMesh;
                }
            }
            if (view._coastSlot < 0)
            {
                Debug.LogError($"[{nameof(CoastalShelfView)}] Coast wave capacity exceeded.");
            }
            return true;
        }
    }
}
