using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XRShared.GrabbableMagnet
{
    /*
     * Make sure that:
     * - attractable under its hierarchy are not attracted by any magnet under its hierarchy
     * - when ungrab, prevent all attractable to be attracted, and select only the "best" one to be attracted (the one with closest proximity to its target magnet)
     */
    [DefaultExecutionOrder(AttractableMagnet.EXECUTION_ORDER)]
    public class MagnetCoordinator : NetworkBehaviour
    {
        INetworkGrabbable networkGrabbable;
        public bool overrideMagnetRadius = true;
        public float magnetRadius = 0.1f;

        List<IMagnet> magnets = new List<IMagnet>();
        private void Awake()
        {
            networkGrabbable = GetComponentInParent<INetworkGrabbable>();
            if (networkGrabbable != null) 
                networkGrabbable.OnUngrab.AddListener(OnDidUngrab);

            magnets = new List<IMagnet>(GetComponentsInChildren<IMagnet>());
            foreach (var magnet in magnets)
            {
                if(magnet is IAttractableMagnet attracktableMagnet)
                    attracktableMagnet.CheckOnUngrab = false;
            }
        }

        private void OnDidUngrab()
        {
            if (overrideMagnetRadius)
            {
                foreach (var magnet in magnets)
                {
                    if (magnet is IAttractableMagnet attracktableMagnet)
                        attracktableMagnet.MagnetRadius = magnetRadius;
                }
            }
            CheckMagnetProximity();
        }

        [ContextMenu("CheckMagnetProximity")]
        public void CheckMagnetProximity()
        {
            if (Object && Object.HasStateAuthority && networkGrabbable.IsGrabbed == false)
            {
                float minDistance = float.PositiveInfinity;
                IAttractableMagnet closestLocalMagnet = null;
                IAttractorMagnet closestRemoteMagnet = null;
                foreach (var magnet in magnets)
                {
                    if (magnet is IAttractableMagnet attracktableMagnet && attracktableMagnet.TryFindClosestMagnetInRange(out var remoteMagnet, out var distance))
                    {
                        if (distance < minDistance)
                        {
                            closestLocalMagnet = attracktableMagnet;
                            closestRemoteMagnet = remoteMagnet;
                            minDistance = distance;
                        }
                    }
                }
                if (closestLocalMagnet != null)
                {
                    closestLocalMagnet.SnapToMagnet(closestRemoteMagnet);
                }
            }
        }
    }

}
