using System.Collections;
using System.Collections.Generic;
using Fusion.Addons.HandsSync;
using Fusion.Addons.XRHandsSync;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.Addons.VisionOsHelpers
{

    /**
    * 
    * VisionOSHandsConfiguration allows a quick automatic configuration of the hands for the Polyspatial vision OS platform.
    * 
    * The script : 
    *   - ensures that even if the hands are not detected for a short duration, the grabbing triggered by finger detection in FingerDrivenControllerInput still continues
    *   - uses LineMesh to display the beam used by a RayBeamer component
    *   - applies a specific layer to all collider in hands, that should be removed from Polyspatial handled colliders, to be sure that the grabbing/touching collider are not spatial touched by visionOS (which is probably not desired)
    *   - ensures that in case of no detection of the hands, the hand representation components do not try to fallback to controller, as there are no hand controller on visionOS
    * 
    **/
    [DefaultExecutionOrder(-1_000)]
    public class VisionOSHandsConfiguration : MonoBehaviour
    {

        [Header("Configuration options")]
        [Tooltip("If true, uses LineMesh to display the beam used by a RayBeamer component")]
        public bool useLineMeshForRayBeamers = true;
        [Tooltip("If null, while useLineMeshForRayBeamers is true, the Material used for the beamer ray will use the LineSGMaterial shader")]
        public Material rayBeamerMaterial = null;
        [Tooltip("If true, applies a specific layer to all collider in hands, that should be removed from Polyspatial handled colliders, to be sure that the grabbing/touching collider are not spatial touched by visionOS (which is probably not desired)")]
        public bool ignoreHandColliderInPolyspatial = true;
        public string polyspatialIgnoredLayer = "PolySpatialIgnored";

        bool ignoreHandColliderInPolyspatialApplied = false;
        bool useLineMeshForRayBeamersApplied = false;
        IHardwareHand hardwareHand;

        private void Awake() {
            ignoreHandColliderInPolyspatialApplied = false;
#if UNITY_VISIONOS
            VisionOsConfiguration();
#endif
        }
        private void Update()
        {
#if UNITY_VISIONOS
            VisionOsConfiguration();
#endif
        }

        public void OnValidate()
        {
            // Settings layer during runtime with Polyspatail might not be taken into account. So we make sure to set the layer in editor mode
#if UNITY_EDITOR
            ValidationUtils.SceneEditionValidate(gameObject, () => {
                IgnoreHandColliderInPolyspatial();
            });
#endif
        }

        void IgnoreHandColliderInPolyspatial()
        {
            if (ignoreHandColliderInPolyspatial && ignoreHandColliderInPolyspatialApplied == false)
            {
                ignoreHandColliderInPolyspatialApplied = true;
                int layer = LayerMask.NameToLayer(polyspatialIgnoredLayer);
                if (layer == -1)
                {
                    Debug.LogError($"The layer '{polyspatialIgnoredLayer}' does not exists. Create it add remove it from the 'Collider object layer mask' in 'Project settings>Polyspatial'");
                }
                else
                {
                    foreach (var collider in GetComponentsInChildren<Collider>(true))
                    {
                        if (collider.gameObject.layer != layer) {
                            Debug.LogError("[VisionOSHandsConfiguration] Adapting layer of " + collider.gameObject.name + " ("+ polyspatialIgnoredLayer + ")");
                            collider.gameObject.layer = layer;
#if UNITY_EDITOR
                            if(Application.IsPlaying(gameObject) == false)
                            {
                                 UnityEditor.EditorUtility.SetDirty(collider.gameObject);
                            }
#endif
                        }
                    }
                }
            }
        }

        public void VisionOsConfiguration()
        {
            if (hardwareHand == null) hardwareHand = GetComponentInParent<IHardwareHand>();

            // Hand colliders
            IgnoreHandColliderInPolyspatial();

            // Ray beamer
            // Line renderers are not yet available on polyspatial: placing a LineRendererToLineMesh to replace them
            if (useLineMeshForRayBeamers && useLineMeshForRayBeamersApplied == false) {
                useLineMeshForRayBeamersApplied = true;
                var beamer = GetComponentInChildren<RayBeamer>(true);
                if (beamer)
                {
                    var lineRendererObject = beamer.gameObject;
                    if (beamer.lineRenderer) lineRendererObject = beamer.lineRenderer.gameObject;
                    var meshRenderer = lineRendererObject.AddComponent<MeshRenderer>();
                    if (rayBeamerMaterial)
                    {
                        meshRenderer.material = rayBeamerMaterial;
                    }
                    else
                    {
                        meshRenderer.material = Resources.Load<Material>("LineSGMaterial");
                    }
                    lineRendererObject.AddComponent<MeshFilter>();
                    var lineRendererToLineMesh = lineRendererObject.AddComponent<LineRendererToLineMesh>();
                    lineRendererToLineMesh.checkPositionsEveryFrame = true;
                    lineRendererToLineMesh.replicateLineRendererEnabledStatus = true;
                }
            }
        }

    }

}
