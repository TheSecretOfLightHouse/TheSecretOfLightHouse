using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.Spinocroc.Server;
using Lighthouse.Monsters.Spinocroc.Rules;
using Lighthouse.Systems.Audio;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class MonsterBehaviourAudio : NetworkBehaviour
    {
        [SerializeField] private AudioClip[] _clips;
        [SerializeField] private float[] _volumes;
        [SerializeField] private SoundManager _soundManagerPrefab;

        private SpinocrocPreparation _preparation;
        private SpinocrocCharge _charge;

        private void Awake()
        {
            _preparation = GetComponent<SpinocrocPreparation>();
            _charge = GetComponent<SpinocrocCharge>();
        }

        public override void OnStartServer()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged += HandlePhaseChanged;
            }
            if (_charge != null)
            {
                _charge.TargetDamaged += HandleTargetDamaged;
            }
        }

        public override void OnStopServer()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged -= HandlePhaseChanged;
            }
            if (_charge != null)
            {
                _charge.TargetDamaged -= HandleTargetDamaged;
            }
        }

        [Server]
        public void PlayCue(int index, Vector3 position)
        {
            if (isActiveAndEnabled)
            {
                RpcPlayCue(index, position, NetworkTime.time);
            }
        }

        [ClientRpc]
        private void RpcPlayCue(int index, Vector3 position, double startedAt)
        {
            if (!isActiveAndEnabled || _clips == null || _volumes == null
                || index < 0 || index >= _clips.Length || index >= _volumes.Length)
            {
                return;
            }
            AudioClip clip = _clips[index];
            if (clip == null || NetworkTime.time - startedAt >= clip.length)
            {
                return;
            }
            if (SoundManager.Instance == null && _soundManagerPrefab != null)
            {
                Instantiate(_soundManagerPrefab);
            }
            SoundManager manager = SoundManager.Instance;
            if (manager != null)
            {
                manager.PlayEffectAt(clip, position, Mathf.Clamp01(_volumes[index]));
            }
        }

        private void HandlePhaseChanged(SpinocrocPhase phase)
        {
            if (phase == SpinocrocPhase.Diving)
            {
                PlayCue(1, transform.position);
            }
            else if (phase == SpinocrocPhase.Charging)
            {
                PlayCue(2, transform.position);
            }
        }

        private void HandleTargetDamaged(ulong attackId, MonsterBoatTarget target)
        {
            if (target != null)
            {
                PlayCue(0, target.transform.position);
            }
        }
    }
}
