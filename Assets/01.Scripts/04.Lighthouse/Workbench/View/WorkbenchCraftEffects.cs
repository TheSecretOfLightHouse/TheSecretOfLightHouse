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
        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private ParticleSystem _sparks;
        [SerializeField]
        private ParticleSystem _dust;
        [Tooltip("3D source; its max distance should match the visible distance.")]
        [SerializeField]
        private AudioSource _audio;
        [Tooltip("Hammer blows. None chosen yet (2026-10-27); left empty, only the particles play.")]
        [SerializeField]
        private AudioClip _hammerClip;
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

            if (_audio && _hammerClip)
            {
                _audio.PlayOneShot(_hammerClip);
            }
        }

        private bool IsLocalPlayerNear()
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            return player && (player.transform.position - transform.position).sqrMagnitude <= _visibleDistance * _visibleDistance;
        }
    }
}
