using System.Collections.Generic;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
using UnityEngine;


namespace Fusion.Addons.VisionOsHelpers
{

    /**
     * 
     * SpatialGrabber is used to simulate an hand and enable spatial grabbing.
     * 
     **/

    public class SpatialGrabber : Grabber
    {
        public string polyspatialIgnoredLayer = "PolySpatialIgnored";

        List<Collider> colliders = new List<Collider>();
        List<Renderer> renderers = new List<Renderer>();
        bool wasRequired = false;
        protected override void Awake()
        {
            base.Awake();
            foreach (var c in GetComponentsInChildren<Collider>()) if (c.enabled) colliders.Add(c);
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled) renderers.Add(r);

            Required(false, force: true);

            ConfigureLayerForPolyspatial();
        }

        public void OnValidate()
        {
            // Settings layer during runtime with Polyspatail might not be taken into account. So we make sure to set the layer in editor mode
#if UNITY_EDITOR
            ValidationUtils.SceneEditionValidate(gameObject, () => {
                ConfigureLayerForPolyspatial();
            });
#endif
        }

        void ConfigureLayerForPolyspatial()
        {
            int layer = LayerMask.NameToLayer(polyspatialIgnoredLayer);
            if (layer == -1)
            {
                Debug.LogError($"The layer '{polyspatialIgnoredLayer}' does not exists. Create it add remove it from the 'Collider object layer mask' in 'Project settings>Polyspatial'");
            }
            else
            {
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                {
                    if (collider.gameObject.layer != layer)
                    {
                        Debug.LogError("[SpatialGrabber] Adapting layer of " + collider.gameObject.name + " (" + polyspatialIgnoredLayer + ")");
                        collider.gameObject.layer = layer;
#if UNITY_EDITOR
                        if (Application.IsPlaying(gameObject) == false)
                        {
                            UnityEditor.EditorUtility.SetDirty(collider.gameObject);
                        }
#endif
                    }
                }
            }
        }

        // Required is used to enable/disable the spatialGrabber colliders & renderers 
        public void Required(bool isRequired, bool force = false)
        {
            if (force == false && isRequired == wasRequired) return;
            foreach (var c in colliders) c.enabled = isRequired;
            foreach (var r in renderers) r.enabled = isRequired;
            wasRequired = isRequired;
        }

        private void LateUpdate()
        {
            Required(isRequired: IsGrabbing);
        }

        private void OnDisable()
        {
            if (grabbedObject)
            {
                Ungrab(grabbedObject);
            }
        }
    }
}
