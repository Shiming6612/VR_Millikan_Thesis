using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.Events;
using Fusion.XR.Shared.Core;

#if UNITY_VISIONOS
#if POLYSPATIAL_SDK_AVAILABLE
using Unity.PolySpatial;
#endif
#endif

namespace Fusion.Addons.VisionOsHelpers
{
    /**
    * 
    * Script to hide avatar parts based on mode (renderers should be hidden in bounded volume as the user position is undefined).
    *  
    **/
    public class VisionOsVolumeCameraModeSync : NetworkBehaviour, IRigPartVisualizerCustomizer
    {
        public enum VisionOsMode
        {
            Undefined,
            VisionOSBoundedVolume,
            VisionOSUnboundedVolume
        }

        [Networked]
        public VisionOsMode CurrentMode { get; set; }

        VisionOsMode appliedMode = VisionOsMode.Undefined;

        [Header("Renderers to adapt")]
        [SerializeField]
        bool hideAllChildRenderersInBoundedVolume = true;
        [SerializeField]
        bool hideRigPartsInboundedMode = true;
        [SerializeField]
        List<Renderer> hiddenRenderersForBoundedVolume = new List<Renderer>();

        public UnityEvent onModeChange;

#if UNITY_VISIONOS
#if POLYSPATIAL_SDK_AVAILABLE
        VolumeCamera localVolumeCamera;
#endif
#endif

        public override void Spawned()
        {
            base.Spawned();
            if (hideAllChildRenderersInBoundedVolume)
            {
                hiddenRenderersForBoundedVolume = new List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled) hiddenRenderersForBoundedVolume.Add(r);
                }
            }
            if (hideRigPartsInboundedMode)
            {
                foreach(var rigPart in GetComponentsInChildren<IRigPart>(true))
                {
                    foreach(var r in rigPart.gameObject.GetComponentsInChildren<Renderer>())
                    {
                        if(hiddenRenderersForBoundedVolume.Contains(r) == false)
                        {
                            hiddenRenderersForBoundedVolume.Add(r);
                        }
                    }
                }
            }

            if (Object.HasStateAuthority)
            {
                DetectVolumeCamera();
            }

            if (GetComponentInChildren<RigPartVisualizer>() == null && GetComponentInParent<RigPartVisualizer>() == null)
            {
                // As a IRigPartVisualizerCustomizer, the renderer will be only be adapted by us through a rigPartVisualizer. We add one if there is none
                var rigPartVisualizer = gameObject.AddComponent<RigPartVisualizer>();
                rigPartVisualizer.mode = RigPartVisualizer.Mode.DisplayWhileOnline;
                rigPartVisualizer.autofillRenderersToAdapt = false;
                rigPartVisualizer.renderersToAdapt.Clear();
            }
            CheckMode();
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (Object.HasStateAuthority)
            {
                DetectVolumeCamera();
            }
        }

        public override void Render()
        {
            base.Render();
            CheckMode();
        }

        void DetectVolumeCamera()
        {
            if (Object.HasStateAuthority)
            {
                var mode = VisionOsMode.Undefined;
#if UNITY_VISIONOS
#if POLYSPATIAL_SDK_AVAILABLE
                if (localVolumeCamera == null) localVolumeCamera = FindAnyObjectByType<VolumeCamera>();
                if (localVolumeCamera) {
                    if (localVolumeCamera.WindowConfiguration.Mode == VolumeCamera.PolySpatialVolumeCameraMode.Bounded)
                    {
                        mode = VisionOsMode.VisionOSBoundedVolume;
                    }
                    if (localVolumeCamera.WindowConfiguration.Mode == VolumeCamera.PolySpatialVolumeCameraMode.Unbounded)
                    {
                        mode = VisionOsMode.VisionOSUnboundedVolume;
                    }
                }
#endif
#endif
                if (mode != CurrentMode)
                {
                    Debug.Log("Detected local VolumeCamera Mode change " + mode);
                    CurrentMode = mode;
                }
            }
        }

        void CheckMode()
        {
            if (appliedMode != CurrentMode)
            {
                appliedMode = CurrentMode;
                if (onModeChange != null) onModeChange.Invoke();
            }
        }

        #region IRigPartVisualizerCustomizer
        public bool ShouldIgnoreRenderer(Renderer r)
        {
            return false;
        }

        public bool ShouldCustomizeRendererShouldDisplay(Renderer r, out bool shouldDisplay)
        {
            shouldDisplay = true;
            if (Object == null || Object.IsValid == false || Runner == null) return false;
            if(CurrentMode != VisionOsMode.VisionOSBoundedVolume)
            {
                // We only adapt rendering in bounded mode
                return false;
            }
            if(hiddenRenderersForBoundedVolume.Contains(r) == false)
            {
                return false;
            }
            shouldDisplay = false;
            return true;
        }
        #endregion
    }
}
