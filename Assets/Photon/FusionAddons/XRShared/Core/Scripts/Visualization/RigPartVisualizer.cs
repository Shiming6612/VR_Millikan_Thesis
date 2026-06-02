using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Fusion.XR.Shared.Core
{
    // Interface to allow components to customize the logic of any parent or child RigPartVisualizer, to either ignore a renderer or add addition constraints before showing it 
    public interface IRigPartVisualizerCustomizer
    {
        public bool ShouldIgnoreRenderer(Renderer r);
        public bool ShouldCustomizeRendererShouldDisplay(Renderer r, out bool shouldDisplay);
    }

    // Interface to track to automatically add game object to objects to adapt
    public interface IRigPartVisualizerGameObjectToAdapt : IUnityBehaviour { }

    /// <summary>
    /// Will display/hide all renderers in renderersToAdapt, accordingly to display mode (when offline, when offline, never, always)
    /// Can replace the material, instead of hidden the renderer, when ShouldNotDisplayMaterial is set.
    /// 
    /// If adaptRenderersDuringUpdate is set to false, another component can customize the adaptation, by calling AdaptRenderers(shouldDisplay) manually.
    ///  This component can use ShouldDisplay() as a starting value, and then customize it
    ///  
    /// The renderer enable adaption is done in Update. To override it in another component, use LateUpdate
    /// </summary>

    [DefaultExecutionOrder(100_000)]
    public class RigPartVisualizer : MonoBehaviour
    {
        [Header("Renderer adaptation configuraiton")]
        [Tooltip("If true, automatically fills renderersToAdapt (unless renderersToAdapt is not empty)")]
        public bool autofillRenderersToAdapt = true;
        [Tooltip("Automatically filled with all children renderers if empty and autofillRendererToAdapt is true")]
        public List<Renderer> renderersToAdapt = new List<Renderer>();
        [Tooltip("Renderer that should not be included in renderersToAdapt (mostly useful when it is automatically filled)")]
        public List<Renderer> renderersToIgnore = new List<Renderer>();
        [Tooltip("If not null, instead of being hidden, a renderer that should not be displayed will receive this material. " +
            "Useful for transparent material, used when a animator should still run to animate bones in order to have a position" +
            " - on Android, a disabled renderer would not animate the skeleton")]
        public Material materialWhileShouldNotDisplay;
        protected Dictionary<Renderer, Material> overridenRendererInitialMaterial = new Dictionary<Renderer, Material>();

        [Header("Canvas adaptation configuraiton")]
        [Tooltip("If true, automatically fills canvasesToAdapt (unless canvasesToAdapt is not empty)")]
        public bool autofillCanvasesToAdapt = true;
        [Tooltip("Automatically filled with all children canvases if empty and autofillCanvasesToAdapt is true")]
        public List<Canvas> canvasesToAdapt = new List<Canvas>();
        [Tooltip("Canvas that should not be included in canvasesToAdapt (mostly useful when it is automatically filled)")]
        public List<Canvas> canvasesToIgnore = new List<Canvas>();

        [Header("GameObject adaptation configuraiton")]
        [Tooltip("Game Object that should be included in renderersToAdapt")]
        public List<GameObject> gameObjectsToAdapt = new List<GameObject>();

        [Header("Options")]

        public IRigPart rigPart;
        [Tooltip("Set it to false to stop automatic adaptation. relavant if another component calls AdaptRenderers(shouldDisplay) manually (it can use ShouldDisplay() as a starting value, and then customize it)")]
        public bool adaptRenderersDuringUpdate = true;
        public List<IRigPartVisualizerCustomizer> customizers = new List<IRigPartVisualizerCustomizer>();


        [System.Flags]
        public enum Mode
        {
            NeverDisplay = 0,
            DisplayWhileOnline = 1,
            DisplayWhileOffline = 2,
            DisplayAlways = 1 | 2
        }

        public Mode mode = Mode.DisplayAlways;


        protected virtual void Awake()
        {
            if ((renderersToAdapt == null || renderersToAdapt.Count == 0) && autofillRenderersToAdapt)
            {
                renderersToAdapt = new List<Renderer>(GetComponentsInChildren<Renderer>());
            }
            if ((canvasesToAdapt == null || canvasesToAdapt.Count == 0) && autofillCanvasesToAdapt)
            {
                canvasesToAdapt = new List<Canvas>(GetComponentsInChildren<Canvas>());
            }
            foreach (var customizer in GetComponentsInChildren<IRigPartVisualizerCustomizer>())
            {
                if (customizers.Contains(customizer) == false) customizers.Add(customizer);
            }
            foreach (var customizer in GetComponentsInParent<IRigPartVisualizerCustomizer>())
            {
                if (customizers.Contains(customizer) == false) customizers.Add(customizer);
            }
            foreach(var c in GetComponentsInChildren<IRigPartVisualizerGameObjectToAdapt>())
            {
                if(gameObjectsToAdapt.Contains(c.gameObject) == false)
                {
                    gameObjectsToAdapt.Add(c.gameObject);
                }
            }
        }

        private void Update()
        {
            if (adaptRenderersDuringUpdate)
            {
                bool shouldDisplay = ShouldDisplay();
                Adapt(shouldDisplay);
            }
        }

        public void Adapt(bool shouldDisplay)
        {
            AdaptRenderers(shouldDisplay);
            AdaptGameObjects(shouldDisplay);
            AdaptCanvases(shouldDisplay);
        }

        void AdaptRenderers(bool shouldDisplay)
        {
            foreach (var r in renderersToAdapt)
            {
                if (renderersToIgnore.Contains(r)) continue;
                bool shouldBeIgnoredDuetoCustomizer = false;
                foreach (var customizer in customizers)
                {
                    if (customizer.ShouldIgnoreRenderer(r))
                    {
                        shouldBeIgnoredDuetoCustomizer = true;
                        break;
                    }
                    if (customizer.ShouldCustomizeRendererShouldDisplay(r, out bool customizerShouldDisplay))
                    {
                        shouldDisplay = shouldDisplay && customizerShouldDisplay;
                    }
                }
                if (shouldBeIgnoredDuetoCustomizer) continue;

                if (materialWhileShouldNotDisplay != null)
                {
                    // Adat renderer material to shouldDisplay
                    if (shouldDisplay == false && overridenRendererInitialMaterial.ContainsKey(r) == false)
                    {
                        overridenRendererInitialMaterial[r] = r.material;
                        r.material = materialWhileShouldNotDisplay;
                    }
                    if (shouldDisplay && overridenRendererInitialMaterial.ContainsKey(r))
                    {
                        r.material = overridenRendererInitialMaterial[r];
                        overridenRendererInitialMaterial.Remove(r);
                    }
                }
                else
                {
                    // Adapt renderer enabled to shouldDisplay
                    if (r.enabled != shouldDisplay)
                    {
                        r.enabled = shouldDisplay;
                    }
                }
            }

        }

        void AdaptGameObjects(bool shouldDisplay)
        {
            foreach (var gameObjectToAdapt in gameObjectsToAdapt)
            {
                if (gameObjectToAdapt.activeInHierarchy != shouldDisplay)
                {
                    gameObjectToAdapt.SetActive(shouldDisplay);
                }
            }
        }

        void AdaptCanvases(bool shouldDisplay)
        {
            foreach (var c in canvasesToAdapt)
            {
                if (canvasesToIgnore.Contains(c)) continue;
                
                // Adapt canvas enabled to shouldDisplay
                if (c.enabled != shouldDisplay)
                {
                    c.enabled = shouldDisplay;
                }
            }
        }

        public bool ShouldDisplay()
        {
            if (rigPart == null)
            {
                rigPart = GetComponentInParent<IRigPart>();
            }

            bool isOnline = rigPart != null && rigPart.IsOnline();

            bool shouldDisplay = ShouldDisplay(isOnline);
            if(rigPart is IHardwareRigPart hardwareRigPart)
            {
                shouldDisplay = shouldDisplay && hardwareRigPart.TrackingStatus == RigPartTrackingstatus.Tracked;
            }
            return shouldDisplay;
        }

        public bool ShouldDisplay(bool isOnline)
        {
            bool shouldDisplay;
            if (isOnline)
            {
                shouldDisplay = (mode & Mode.DisplayWhileOnline) == Mode.DisplayWhileOnline;
            }
            else
            {
                shouldDisplay = (mode & Mode.DisplayWhileOffline) == Mode.DisplayWhileOffline;
            }
            return shouldDisplay;
        }
    }
}
