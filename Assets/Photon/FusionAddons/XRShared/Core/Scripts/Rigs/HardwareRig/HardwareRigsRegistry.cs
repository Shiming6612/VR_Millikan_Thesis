using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public static class HardwareRigsRegistry
    {
        #region Hardware rig discoverability
        // When enabled, when using Fusion.XR.Shared.Core's NetworkRig and NetworkRigPart subclasses,
        //  an IHardwareRig is expected to register with HardwareRigsRegistry.RegisterAvailableHardwareRig(this), and unregister when not with HardwareRigsRegistry.UnregisterAvailableHardwareRig(this)
        static List<IHardwareRig> AvailableHardwareRigs = new List<IHardwareRig>();

        public static void RegisterAvailableHardwareRig(IHardwareRig hardwareRig)
        {
            if (AvailableHardwareRigs.Contains(hardwareRig) == false)
            {
                AvailableHardwareRigs.Add(hardwareRig);
            }
        }

        public static void UnregisterAvailableHardwareRig(IHardwareRig hardwareRig)
        {
            AvailableHardwareRigs.Remove(hardwareRig);
        }

        public static List<IHardwareRig> GetAvailableHardwareRigs()
        {
            return AvailableHardwareRigs;
        }
        
        public static IHardwareRig GetHardwareRig(NetworkRunner runner = null)
        {
            if(runner == null)
            {
                if (AvailableHardwareRigs.Count > 0)
                {
                    return AvailableHardwareRigs[0];
                }
            } 
            else
            {
                IHardwareRig fallbackHardwareRig = null;
                foreach(var hardwareRig in AvailableHardwareRigs)
                {
                    // If several rig are present (multi peer scenario), we use the runner to differenciate
                    if (hardwareRig.Runner == runner || AvailableHardwareRigs.Count == 1)
                    {
                        return hardwareRig;
                    }
                    else if (hardwareRig.Runner == null && fallbackHardwareRig == null)
                    {
                        fallbackHardwareRig = hardwareRig;
                    }
                }
                if (fallbackHardwareRig != null)
                {
                    Debug.LogError($"[Error] Several hardware rig (without predefined runner) found. Should not happen, using a fallback solution. Possible causes:\n"
                        + "- multiple scenes with an hardware rig have been loaded. This can happen if clients do not have the same scenes checked in the build settings, or if an hardware rig has a don't destroy on load during a scene change to a scene having already an hardware rig\n"
                        + "- in a multipeer setup, the runner associated to the hardware rig should be defined.");
                    return fallbackHardwareRig;
                }
            }
            return null;
        }
        #endregion
    }
}

