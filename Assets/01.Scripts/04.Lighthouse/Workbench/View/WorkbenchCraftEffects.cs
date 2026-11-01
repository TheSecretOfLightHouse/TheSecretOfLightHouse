using System.Collections;
using Lighthouse.Systems.Audio;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Workbench.View
{
    // Sparks, dust and hammer blows on the workbench model when a module is finished (2026-10-27). Driven by the finish
    // call every machine gets; who crafted is not sent. The sound is 3D and fades with distance, and nothing plays for
    // a player farther away than the visible distance, so only those near the bench see and hear it.
    [DisallowMultipleComponent]
    public sealed class WorkbenchCraftEffects : MonoBehaviour
    {
        private const int HammerRepeatCount = 3;

        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private ParticleSystem _sparks;
        [SerializeField]
        private ParticleSystem _dust;
        [SerializeField]
        private SoundManager _soundManagerPrefab;
        [SerializeField]
        private AudioClip _hammerClip;
        [SerializeField, Range(0f, 1f)]
        private float _hammerVolume = 1f;
        [SerializeField, Min(1f)]
        private float _visibleDistance = 30f;

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.RecipeCompleted += HandleRecipeCompleted;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            if (_workbench)
            {
                _workbench.RecipeCompleted -= HandleRecipeCompleted;
            }
        }

        private void HandleRecipeCompleted(int recipeId)
        {
            if (!IsLocalPlayerNear())
            {
                return;
            }

            if (_sparks)
            {
                _sparks.Play(true);
            }

            if (_dust)
            {
                _dust.Play(true);
            }

            if (_hammerClip)
            {
                StartCoroutine(PlayHammerSounds());
            }
        }

        private IEnumerator PlayHammerSounds()
        {
            if (!SoundManager.Instance && _soundManagerPrefab)
            {
                Instantiate(_soundManagerPrefab);
            }

            var interval = new WaitForSecondsRealtime(_hammerClip.length);
            for (int i = 0; i < HammerRepeatCount; i++)
            {
                if (!SoundManager.Instance || !IsLocalPlayerNear())
                {
                    yield break;
                }

                SoundManager.Instance.PlayEffectAt(_hammerClip, transform.position, _hammerVolume);
                if (i < HammerRepeatCount - 1)
                {
                    yield return interval;
                }
            }
        }

        private bool IsLocalPlayerNear()
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            return player && (player.transform.position - transform.position).sqrMagnitude <= _visibleDistance * _visibleDistance;
        }
    }
}
