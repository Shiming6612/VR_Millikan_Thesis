using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion.Addons.InteractiveMenuAddon;
using Fusion.XR.Shared.Core.Tools;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.Drawing
{
    /***
     * 
     *  ColorSelection is in charged to sync the pen color modification.
     *  CheckColorModification() method is called during FUN to check if the local user used the button to change the pen color.
     *  In this case, ChangePenColor() updates the networked variable PenColor. So, OnColorChanged() is called on all players.
     *  Then, UpdatePenMaterials() updates the pen's material & UpdateDrawColor() end the previous drawing and start a new drawing with the new color
     *          
     ***/
    public class ColorSelection : NetworkBehaviour, IColorProvider
    {
        [Networked]
        public Color CurrentColor { get; set; }
        private Color previousColor;
        public List<Color> colorList;
        public List<MeshRenderer> meshList;
        private int colorIndex = 0;
        [SerializeField] private float changeColorCoolDown = 1f;
        private float lastColorChangedTime = 0f;

        [SerializeField] private Drawer drawer;
        [SerializeField] private Draw draw;

        IFeedbackHandler feedback;
        public string ColorChanged = "OnMenuItemSelected";

        public bool useInput = true;

        public INetworkGrabbable grabbable;
        public InputActionProperty leftControllerChangeColorAction;
        public InputActionProperty rightControllerChangeColorAction;
        public InputActionProperty ChangeColorAction => grabbable != null && grabbable.IsGrabbed && grabbable.CurrentGrabber.RigPart != null && grabbable.CurrentGrabberSide() == RigPartSide.Left ? leftControllerChangeColorAction : rightControllerChangeColorAction;

        public bool IsGrabbed => grabbable != null && grabbable.IsGrabbed;
        public bool IsGrabbedByLocalPLayer => IsGrabbed && grabbable.CurrentGrabber.Object.StateAuthority == Runner.LocalPlayer;

        float minAction = 0.05f;
        public float changeColorInputThreshold = 0.5f;

        private InteractiveMenu interactiveMenu;
        [SerializeField] int nbOfColorModificationBeforeDisablingInteractiveMenu = 2;

        ChangeDetector changeDetector;

        public bool IsUsed
        {
            get
            {
                return ChangeColorAction.action.ReadValue<float>() > minAction;
            }
        }

        private void Awake()
        {
            // update pen meshes
            foreach (MeshRenderer mesh in meshList)
            {
                mesh.material.color = colorList[0];
            }

            grabbable = GetComponent<INetworkGrabbable>();
            interactiveMenu = GetComponent<InteractiveMenu>();
            feedback = GetComponent<IFeedbackHandler>();

            var controllersBindings = new List<string> { "joystick" };
            var keyboardBindings = new List<string> { "composite||2DVector||Up||<Keyboard>/C" };

            leftControllerChangeColorAction.EnableWithDefaultXRBindings(bindings: keyboardBindings, leftBindings: controllersBindings);
            rightControllerChangeColorAction.EnableWithDefaultXRBindings(rightBindings: controllersBindings);

            if (!drawer)
                drawer = GetComponent<Drawer>();

            if (drawer)
            {
                drawer.color = colorList[0];
            }
            else
                Debug.LogError("Drawer not found !");
        }

        public override void Spawned()
        {
            base.Spawned();
            // Set the default color
            if (Object.HasStateAuthority)
            {
                CurrentColor = colorList[0];
            }
            changeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);
            OnColorChanged();
        }

        public override void Render()
        {
            base.Render();
            foreach(var changedVar in changeDetector.DetectChanges(this))
            {
                if (changedVar == nameof(CurrentColor))
                {
                    OnColorChanged();
                }
            }
        }

        // update the pen meshes colors when the pen color has been changed
        void OnColorChanged()
        {
            UpdatePenMaterials();
        }

        // Update the pen meshes material 
        private void UpdatePenMaterials()
        {
            foreach (MeshRenderer mesh in meshList)
            {
                mesh.material.color = CurrentColor;
            }

            UpdateDrawColor();
        }

        private void UpdateDrawColor()
        {
            if (CurrentColor == previousColor) return;

            // Update the drawer color
            drawer.color = CurrentColor;
            previousColor = CurrentColor;

            // Update the current draw: end the previous drawing, making sure its color is stored
            if (drawer.status == Drawer.Status.Drawing)
            {
                drawer.PauseDrawing();
                drawer.ResumeDrawing();
            }
        }

        // Update the networked PenColor & local draw color
        private void ChangePenColor()
        {
            // check color index
            if (colorIndex >= colorList.Count)
                colorIndex = 0;
            if (colorIndex < 0)
                colorIndex = colorList.Count - 1;

            // change the networked color to inform remote players and update the local draw color
            CurrentColor = colorList[colorIndex];
            UpdateDrawColor();
        }

        public void ChangePenColor(int index)
        {
            if (index >= colorList.Count || index < 0)
                Debug.LogError("Index color out of range");
            else
            {
                colorIndex = index;
                ChangePenColor();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Object == null || Object.HasStateAuthority == false) return;

            CheckColorModification();
        }

        private void CheckColorModification()
        {
            // Check if the the local player press the color modification button
            if (IsGrabbedByLocalPLayer && (lastColorChangedTime + changeColorCoolDown < Time.time))
            {
                var stick = ChangeColorAction.action.ReadValue<Vector2>().y;
                if (Mathf.Abs(stick) > changeColorInputThreshold)
                {
                    // button has been used, change the color index
                    lastColorChangedTime = Time.time;

                    if (stick < 0)
                        colorIndex--;
                    else
                        colorIndex++;

                    // Apply color update
                    ChangePenColor();

                    // Update the optional interactive menu to avoid the tips to be always displayed
                    nbOfColorModificationBeforeDisablingInteractiveMenu--;
                    if (nbOfColorModificationBeforeDisablingInteractiveMenu < 0)
                    {
                        if (interactiveMenu)
                            interactiveMenu.EnableInteractiveMenu(false);
                    }

                    // Haptic feedback
                    feedback.PlayAudioAndHapticFeedback(ColorChanged);
                }
            }
        }
    }
}
