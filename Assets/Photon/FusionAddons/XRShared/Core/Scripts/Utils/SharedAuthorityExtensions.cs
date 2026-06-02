using Fusion;
using System.Threading.Tasks;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public static class SharedAuthorityExtensions
    {
        /**
         * Request state authority and wait for it to be received
         * Relevant in shared topology only
         */
        public static async Task<bool> WaitForStateAuthority(this NetworkObject o, float maxWaitTime = 8, bool request = true)
        {
            if (o == null)
            {
                Debug.LogError("Null network object");
                return false;
            }
            if (o.HasStateAuthority)
            {
                return true;
            }

            float waitStartTime = Time.time;
            if (request)
            {
                o.RequestStateAuthority();
            }
            while (!o.HasStateAuthority && (Time.time - waitStartTime) < maxWaitTime)
            {
                await AsyncTask.Delay(1);
            }
            return o.HasStateAuthority;
        }

        // If the object has no state authority, request it if we are the prefered user to do (the one with the lowest player ref)
        //  Should be called on all clients for the selected one to take the authority
        public static void AffectStateAuthorityIfNone(this NetworkObject o)
        {
            if (o == null)  return;
            if (o.StateAuthority == PlayerRef.None || o.Runner.IsPlayerValid(o.StateAuthority) == false)
            {
                o.LaunchEnsureHasStateAuthority();
            }
        }

        public async static void LaunchEnsureHasStateAuthority(this NetworkObject o)
        {
            await o.EnsureHasStateAuthority();
        }

        public async static Task EnsureHasStateAuthority(this NetworkObject o)
        {
            await o.EnsureHasStateAuthority(PlayerRef.None);
        }

        public static bool IsStateAuthorityPresent(this NetworkObject o)
        {
            return o != null && o.StateAuthority != PlayerRef.None && o.Runner.IsPlayerValid(o.StateAuthority);
        }

        public async static Task EnsureHasStateAuthority(this NetworkObject o, PlayerRef originalOwner)
        {
            int i = 0;
            const int maxAttempts = 1_000;
            while (i < maxAttempts)
            {
                if (o == null || o.Runner == null)
                {
                    // Object destroyed
                    return;
                }
                
                if (o != null && o.IsStateAuthorityPresent() && o.StateAuthority != originalOwner)
                {
                    // A new owner has been found
                    break;
                }
                int playerCount = 0;
                PlayerRef lowestPlayerRef = PlayerRef.None;
                foreach (var activePlayer in o.Runner.ActivePlayers)
                {
                    playerCount++;
                    if (lowestPlayerRef == PlayerRef.None || lowestPlayerRef.PlayerId > activePlayer.PlayerId)
                    {
                        lowestPlayerRef = activePlayer;
                    }
                }
                if (lowestPlayerRef == o.Runner.LocalPlayer)
                {
                    o.RequestStateAuthority();
                }
                await AsyncTask.Delay(100);
                i++;
            }
        }



        /// <summary>
        /// Return false is the requested options are not set. Use null to avoid checking an option
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool CheckNetworkOjectConfiguration(this NetworkObject networkObject, bool? expectedAllowStateAuthorityOverride = null, bool? expectedDestroyWhenStateAuthorityLeaves = null, bool? expectedIsMasterClientObject = null)
        {
            if(networkObject == null)
            {
                Debug.LogError("[CheckNetworkOjectConfiguration] null network object");
                return false;
            }
            if (expectedIsMasterClientObject != null)
            {
                bool isMasterClientObject = (networkObject.Flags & NetworkObjectFlags.MasterClientObject) != 0;
                if (expectedIsMasterClientObject != isMasterClientObject)
                {
                    return false;

                }
            }
            if (expectedDestroyWhenStateAuthorityLeaves != null)
            {
                bool deleteOnStateAuthorityLeave = (networkObject.Flags & NetworkObjectFlags.DestroyWhenStateAuthorityLeaves) != 0;
                if (expectedDestroyWhenStateAuthorityLeaves != deleteOnStateAuthorityLeave)
                {
                    return false;

                }
            }
            if (expectedAllowStateAuthorityOverride != null)
            {
                bool allowStateAuthorityOverride = (networkObject.Flags & NetworkObjectFlags.AllowStateAuthorityOverride) != 0;
                if (expectedAllowStateAuthorityOverride != allowStateAuthorityOverride)
                {
                    return false;

                }
            }
            return true;
        }

        public static bool IsObjectWithTransferableAuthority(this NetworkObject networkObject, bool? expectedDestroyWhenStateAuthorityLeaves = false)
        {
            return networkObject.CheckNetworkOjectConfiguration(expectedAllowStateAuthorityOverride: true, expectedDestroyWhenStateAuthorityLeaves: expectedDestroyWhenStateAuthorityLeaves, expectedIsMasterClientObject: false);
        }
    }
}

