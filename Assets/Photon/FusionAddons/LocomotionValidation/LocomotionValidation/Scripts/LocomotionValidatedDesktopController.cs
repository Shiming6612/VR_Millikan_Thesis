using UnityEngine;
using Fusion.XR.Shared.Locomotion;
using UnityEngine.AI;
using Fusion.XR.Shared.Desktop;


namespace Fusion.Addons.LocomotionValidation
{
    /***
     * 
     * LocomotionValidatedDesktopController override the Move() method of the DesktopController in order to check if the new position is valid.
     * The new position is verified by :
     *  - the HardwareLocomotionValidation (both hardware rig and local user networked rig are used as a source of locomotion validation)
     *  - checking if the potential new head position is valid (by checking if it would be in a non trigger collider or if it will be above a navmesh point)
     *  The move is accepted if the player is currently in a not valid position
     *  
     ***/
    public class LocomotionValidatedDesktopController : DesktopController
    {
        HardwareLocomotionValidation hardwareLocomotionValidation;
        NavMeshAgent agent;
        public float navMeshDistanceTolerance = 0.5f;

        private void Awake()
        {
            hardwareLocomotionValidation = GetComponent<HardwareLocomotionValidation>();
            agent = GetComponent<NavMeshAgent>();
            locomotion = GetComponentInChildren<RigLocomotion>();
            if (agent)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
        }

        public override void Move(Vector3 newPosition)
        {
            var move = newPosition - rig.transform.position;
            var newHeadsetPosition = rig.Headset.transform.position + move;
            // Check if the validators accept this new rig position
            if (!hardwareLocomotionValidation.CanMoveHeadset(newHeadsetPosition)) return;

            var isCurrentPositionValid = IsValidHeadPosition(rig.Headset.transform.position);
            // check if DesktopController validation is ok for this new position too
            if (IsValidHeadPosition(newHeadsetPosition) || !isCurrentPositionValid)
            {
                base.Move(newPosition);
            }
        }

        /**
         * Determine if a head position is valid, by checking:
         * - if it would be in a non trigger collider
         * - if it will be above a navmesh point
         */
        public bool IsValidHeadPosition(Vector3 targetPos)
        {
            // Check if it would be in a non trigger collider
            Collider[] headColliders = UnityEngine.Physics.OverlapBox(targetPos, 0.2f * Vector3.one, Quaternion.identity);
            foreach (var c in headColliders)
            {
                if (c.isTrigger == false)
                {
                    return false;
                }
            }

            // Check if it will be above a navmesh point
            if (agent)
            {
                var ray = new Ray(targetPos, -transform.up);
                if (UnityEngine.Physics.Raycast(ray, out var hit, 100f, locomotion.locomotionLayerMask))
                {
                    if (NavMesh.SamplePosition(hit.point + transform.up * 0.1f, out var navMeshHit, navMeshDistanceTolerance, NavMesh.AllAreas))
                    {
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return true;
        }
    }

}
