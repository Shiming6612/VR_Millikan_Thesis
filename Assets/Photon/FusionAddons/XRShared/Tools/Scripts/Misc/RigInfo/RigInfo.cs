using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Tools.RigSimulation;
using UnityEngine;

namespace Fusion.XR.Shared.Rig
{
    
    /***
     * 
     * RigInfo registers & centralizes information about HardwareRig & NetworkRig.
     * In this way, other classes can easily retrieve this information. 
     * 
     ***/
    public class RigInfo : MonoBehaviour
    {
        public enum RigKind
        {
            Undefined,
            VR,
            Desktop
        }

        public RigKind localHardwareRigKind = RigKind.Undefined;
        public ISimulatedRigComponent simulatedRig;

        [Header("Local rigs")]
        public IHardwareRig localHardwareRig;
        public INetworkRig localNetworkedRig;

        public void RegisterNetworkRig(INetworkRig networkRig)
        {
            localNetworkedRig = networkRig;
        }

        public void RegisterHardwareRig(IHardwareRig hardwareRig)
        {
            localHardwareRig = hardwareRig;
            if (hardwareRig != null)
            {
                simulatedRig = hardwareRig.gameObject.GetComponentInChildren<ISimulatedRigComponent>();
                if (simulatedRig != null)
                {
                    localHardwareRigKind = RigKind.Desktop;
                } else
                {
                    localHardwareRigKind = RigKind.VR;
                }
            }
        }
      

        /**
         * Look for a RigInfo, under the runner hierarchy
         */
        public static RigInfo FindRigInfo(NetworkRunner runner = null, bool allowSceneSearch = false)
        {
            RigInfo rigInfo = null;
            if (runner != null) rigInfo = runner.GetComponentInChildren<RigInfo>();
            if (rigInfo == null && allowSceneSearch) rigInfo = FindAnyObjectByType<RigInfo>(FindObjectsInactive.Include);
            if (rigInfo == null)
            {
                Debug.LogWarning("Unable to find RigInfo: it should be stored under the runner hierarchy");
            }
            return rigInfo;
        }
    }
}

