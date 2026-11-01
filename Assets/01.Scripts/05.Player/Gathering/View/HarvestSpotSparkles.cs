using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using UnityEngine;

namespace Lighthouse.Player.Gathering.View
{
    [DisallowMultipleComponent]
    public sealed class HarvestSpotSparkles : MonoBehaviour
    {
        private const int MaxVisibleSpots = 24;
        private const float RefreshInterval = 0.4f;
        private const float VisibleDistance = 18f;
        private readonly HarvestSpot[] _spots = new HarvestSpot[MaxVisibleSpots];
        private readonly ParticleSystem[] _effects = new ParticleSystem[MaxVisibleSpots];
        private PlayerController _player;
        private Material _material;
        private Texture2D _texture;

        private void OnEnable()
        {
            InvokeRepeating(nameof(RefreshVisibleSpots), 0f, RefreshInterval);
        }

        private void RefreshVisibleSpots()
        {
            bool isVisible = _player && _player.CanSimulateLocalWorld && _material;
            for (int i = 0; i < MaxVisibleSpots; i++)
            {
                if (!isVisible || !IsVisible(_spots[i]))
                {
                    _spots[i] = null;
                    if (_effects[i])
                    {
                        _effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            if (!isVisible)
            {
                return;
            }

            var active = HarvestSpot.Active;
            for (int i = 0; i < active.Count; i++)
            {
                HarvestSpot spot = active[i];
                if (!IsVisible(spot))
                {
                    continue;
                }

                int free = -1;
                bool isAssigned = false;
                for (int j = 0; j < MaxVisibleSpots; j++)
                {
                    if (_spots[j] == spot)
                    {
                        isAssigned = true;
                        break;
                    }

                    if (!_spots[j] && free < 0)
                    {
                        free = j;
                    }
                }

                if (isAssigned || free < 0)
                {
                    continue;
                }

                Assign(free, spot);
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RefreshVisibleSpots));
            for (int i = 0; i < MaxVisibleSpots; i++)
            {
                _spots[i] = null;
                if (_effects[i])
                {
                    _effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < MaxVisibleSpots; i++)
            {
                if (_effects[i])
                {
                    Destroy(_effects[i].gameObject);
                }
            }

            if (_material)
            {
                Destroy(_material);
            }

            if (_texture)
            {
                Destroy(_texture);
            }
        }

        public void Initialize(PlayerController player, ParticleSystem impactPrefab)
        {
            _player = player;
            if (!impactPrefab)
            {
                return;
            }

            ParticleSystemRenderer template = impactPrefab.GetComponent<ParticleSystemRenderer>();
            if (!template || !template.sharedMaterial)
            {
                return;
            }

            // Reuse an already referenced URP shader so player builds retain it.
            _material = new Material(template.sharedMaterial);
            _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / 16f - 1f);
                    float v = Mathf.Abs((y + 0.5f) / 16f - 1f);
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 4f);
                    float star = Mathf.Clamp01(1f - Mathf.Min(u, v) * 18f)
                        * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Max(u, v)), 1.5f);
                    _texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(core, star)));
                }
            }

            _texture.Apply(false, true);
            _material.mainTexture = _texture;
            if (_material.HasProperty("_BaseMap"))
            {
                _material.SetTexture("_BaseMap", _texture);
            }

            if (_material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", Color.white);
            }

            for (int i = 0; i < MaxVisibleSpots; i++)
            {
                CreateEffect(i);
            }
        }

        private bool IsVisible(HarvestSpot spot)
        {
            if (!spot || !spot.isActiveAndEnabled || !spot.IsBound
                || (spot.Position - transform.position).sqrMagnitude > VisibleDistance * VisibleDistance)
            {
                return false;
            }

            short islandIndex = HarvestSpotIds.GetIslandIndex(spot.SpotId);
            int localIndex = HarvestSpotIds.GetLocalIndex(spot.SpotId);
            var islands = IslandStateNet.Active;
            for (int i = 0; i < islands.Count; i++)
            {
                IslandStateNet island = islands[i];
                if (island && island.IslandIndex == islandIndex)
                {
                    return localIndex < island.Nodes.Count && island.Nodes[localIndex].Remaining > 0;
                }
            }

            return false;
        }

        private void CreateEffect(int index)
        {
            if (!_effects[index])
            {
                GameObject effect = new GameObject("HarvestSpotSparkles");
                ParticleSystem particles = effect.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
                main.startColor = new Color(1.25f, 1.1f, 0.65f, 0.9f);
                main.maxParticles = 8;
                var emission = particles.emission;
                emission.rateOverTime = 4f;
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,
                    new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _effects[index] = particles;
            }
        }

        private void Assign(int index, HarvestSpot spot)
        {
            ParticleSystem effectParticles = _effects[index];
            Bounds bounds = new Bounds(spot.Position + Vector3.up * 0.6f, Vector3.one);
            Collider collider = spot.GetComponentInChildren<Collider>();
            if (collider)
            {
                bounds = collider.bounds;
            }

            Vector3 center = bounds.center;
            center.y = Mathf.Min(center.y, spot.Position.y + 1.2f);
            effectParticles.transform.position = center;
            var shape = effectParticles.shape;
            shape.shapeType = ParticleSystemShapeType.BoxShell;
            shape.scale = new Vector3(Mathf.Clamp(bounds.size.x, 0.5f, 2f),
                Mathf.Clamp(bounds.size.y, 0.5f, 1.4f), Mathf.Clamp(bounds.size.z, 0.5f, 2f));
            _spots[index] = spot;
            effectParticles.Play(true);
        }
    }
}
