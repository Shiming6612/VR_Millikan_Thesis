using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.Addons.SocialDistancingAddon
{
    /**
     * 
     * SocialDistancing is in charge to check that players can not be too close.
     * ForbiddenArea is deleted during the Spawned() for the local networg rig to avoid auto collision detection
     * 
     **/
    public class SocialDistancing : NetworkBehaviour, ILocomotionValidator
    {
        public INetworkRig networkRig;

        [SerializeField] private GameObject forbiddenArea;
        public SocialDistancingVisualFeeback socialDistancingVisualFeeback;
        [SerializeField] private LayerMask forbiddenLocomotionLayerMask;

        private void Awake()
        {
            networkRig = GetComponent<NetworkRig>();
            if (networkRig == null)
                Debug.LogError("NetworkRig not found");
        }
        public override void Spawned()
        {
            base.Spawned();

            if (!forbiddenArea)
                Debug.LogError("forbiddenArea is not set !");

            // delete forbiddenArea for local player
            if (networkRig != null && networkRig.Object.HasStateAuthority)
            { 
                Destroy(forbiddenArea);
            }
        }

        private void Update()
        {
            if (forbiddenArea == null)
                return;
            forbiddenArea.transform.position = new Vector3(networkRig.Headset.transform.position.x, networkRig.transform.position.y, networkRig.Headset.transform.position.z);

            if (socialDistancingVisualFeeback)
                socialDistancingVisualFeeback.transform.position = new Vector3(networkRig.Headset.transform.position.x, networkRig.transform.position.y + 0.1f, networkRig.Headset.transform.position.z);
        }


        // CanMoveHeadset method checks if the player's head is above the `ForbiddenArea` by performing a raycast towards the ground.
        public bool CanMoveHeadset(Vector3 headsetNewPosition)
        {
            var ray = new Ray(headsetNewPosition, -transform.up);
            if (UnityEngine.Physics.Raycast(ray, out var hit, 5f, forbiddenLocomotionLayerMask))
            {
                if (networkRig != null && networkRig.Object.HasStateAuthority)
                {
                    var headsetRay = new Ray(networkRig.Headset.transform.position, -transform.up);
                    if (UnityEngine.Physics.Raycast(headsetRay, out var headsetHit, 5f, forbiddenLocomotionLayerMask))
                    {
                        //The local user is already stuck in a forbidden zone.We allow them to move to go out
                        return true;
                    }
                }

                var hitObject = hit.collider.gameObject;
                SocialDistancing socialDistancing = hitObject.GetComponentInParent<SocialDistancing>();
                SocialDistancingVisualFeeback visualFeedback = socialDistancing.socialDistancingVisualFeeback;
                if (visualFeedback)
                {
                    StartCoroutine(visualFeedback.DisplayForbiddenArea());
                }

                // The user is going to collide with another player. So he must NOT move.
                return false;
            }
            else
                // The user is not going to collide. He can move
                return true;
        }
    }
}
