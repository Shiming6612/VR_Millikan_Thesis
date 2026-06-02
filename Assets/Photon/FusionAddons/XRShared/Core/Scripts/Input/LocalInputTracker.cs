using UnityEngine;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Fusion.XR.Shared.Core
{
    public class LocalInputTracker
    {
        protected ILateralizedRigPart referenceRigPart;
#if ENABLE_INPUT_SYSTEM
        protected InputActionProperty leftAction;
        protected InputActionProperty rightAction;
#endif
        protected bool areActionsEnabled = false;

        protected List<string> trackedActions = new List<string>();

        public InputAction overrideInputAction = null;

        /// <summary>
        /// Create a simple binding tracker. The binding path should be complete (include {LeftHand}/ or {RightHand}/ if needed)
        /// </summary>
        /// <param name="explicitBinding"></param>
        /// <param name="type"></param>
        public LocalInputTracker(string explicitBinding, InputActionType type = InputActionType.Value)
        {
            var action = new InputAction(type: type);
            action.AddBinding(explicitBinding);
            this.overrideInputAction = action;
            EnableActions();
        }

        public LocalInputTracker(InputAction explicitInputAction)
        {
            this.overrideInputAction = explicitInputAction;
            EnableActions();
        }

        /// <summary>
        /// XR bindings should be provided without their {LeftHand}/ or {RightHand}/ prefixes
        // A reference rig part has to be provided through ChangeReferenceRigPart
        /// </summary>
        public LocalInputTracker(List<string> xrBindings) {
            LoadXRBindings(xrBindings);
        }

        /// <summary>
        /// XR bindings should be provided without their {LeftHand}/ or {RightHand}/ prefixes
        /// </summary>
        public LocalInputTracker(List<string> xrBindings, ILateralizedRigPart referenceRigPart) {
            LoadXRBindings(xrBindings);
            ChangeReferenceRigPart(referenceRigPart);
        }

        /// <summary>
        /// XR bindings should be provided without their {LeftHand}/ or {RightHand}/ prefixes
        /// </summary>
        public void LoadXRBindings(List<string> xrBindings)
        {
            leftAction = new InputActionProperty(new InputAction());
            rightAction = new InputActionProperty(new InputAction());
            this.trackedActions = xrBindings;

        }

        public virtual void EnableActions()
        {
            if (areActionsEnabled) return;

            areActionsEnabled = true;
#if ENABLE_INPUT_SYSTEM
            if (overrideInputAction != null)
            {
                overrideInputAction.Enable();
            }
            if (leftAction != null)
            {
                leftAction.EnableWithDefaultXRBindings(side: RigPartSide.Left, trackedActions);
            }
            if (rightAction != null)
            {
                rightAction.EnableWithDefaultXRBindings(side: RigPartSide.Right, trackedActions);
            }
#endif
        }

        public virtual void ChangeReferenceRigPart(ILateralizedRigPart rigPart)
        {
            referenceRigPart = rigPart;
            EnableActions();
        }

        ILateralizedHardwareRigPart InputRigPart
        {
            get
            {
                var relatedHardwareRigPart = referenceRigPart.RelatedLocalHardwareRigPart();
                if (relatedHardwareRigPart is ILateralizedHardwareRigPart hardwareRigPart)
                {
                    return hardwareRigPart;
                }
                return null;
            }
        }

        public virtual bool IsInputAvailable => InputRigPart != null;

        public virtual InputAction Action {
            get {
                if (overrideInputAction != null)
                {
                    return overrideInputAction;
                }
                var part = InputRigPart;
                if (part != null)
                {
                    var actionProperty = (part.Side == RigPartSide.Left) ? leftAction : rightAction;
                    return actionProperty.action;
                }
                return null;
            }
        }
        public virtual T? ReadValue<T>() where T : struct
        {
            EnableActions();
#if ENABLE_INPUT_SYSTEM
            var action = Action;
            if(Action != null){
                return action.ReadValue<T>();
            }
#endif
            return null;
        }

        /// <summary>
        /// Read both side input (unless an explicit action has been provide), add return the max result
        /// </summary>
        /// <returns></returns>
        public float? ReadMaxFloat()
        {
            EnableActions();
            if (overrideInputAction != null)
            {
                return overrideInputAction.ReadValue<float>();
            }
            else
            {
                var leftValue = leftAction.action.ReadValue<float>();
                var rightValue = rightAction.action.ReadValue<float>();
                return leftValue > rightValue ? leftValue : rightValue;
            }
        }

        /// <summary>
        /// Read both side input (unless an explicit action has been provide), add return true if any is true
        /// </summary>
        /// <returns></returns>
        public bool? ReadAnyBool()
        {
            EnableActions();
            if (overrideInputAction != null)
            {
                return overrideInputAction.ReadValue<bool>();
            }
            else
            {
                var leftValue = leftAction.action.ReadValue<bool>();
                var rightValue = rightAction.action.ReadValue<bool>();
                return leftValue || rightValue;
            }
        }
    }

    public class LocalTriggerTracker : LocalInputTracker
    {
        public LocalTriggerTracker() : base(xrBindings: new List<string> { "trigger" }) { }

        public LocalTriggerTracker(ILateralizedRigPart referenceRigPart) : base(xrBindings: new List<string> { "trigger" }, referenceRigPart) { }
    }

    public class LocalGripTracker : LocalInputTracker
    {
        public LocalGripTracker() : base(xrBindings: new List<string> { "grip" }) { }

        public LocalGripTracker(ILateralizedRigPart referenceRigPart) : base(xrBindings: new List<string> { "grip" }, referenceRigPart) { }
    }

    public class LocalControllerPresenceTracker : LocalInputTracker
    {
        public LocalControllerPresenceTracker() : base(xrBindings: new List<string> { "isTracked" }) { }

        public LocalControllerPresenceTracker(ILateralizedRigPart referenceRigPart) : base(xrBindings: new List<string> { "isTracked" }, referenceRigPart) { }
    }
    
    public class LocalJoystickTracker : LocalInputTracker
    {
        public LocalJoystickTracker() : base(xrBindings: new List<string> { "joystick" }) { }

        public LocalJoystickTracker(ILateralizedRigPart referenceRigPart) : base(xrBindings: new List<string> { "joystick" }, referenceRigPart) { }

        public float? ReadXAxis()
        {
            return ReadValue<Vector2>()?.x;
        }
        public float? ReadYAxis()
        {
            return ReadValue<Vector2>()?.y;
        }

        /// <summary>
        /// Read both side input (unless an explicit action has been provide), add return the max magnittude result
        /// </summary>
        /// <returns></returns>
        public Vector2? ReadMaxSideAxis()
        {
            EnableActions();
            if (overrideInputAction != null)
            {
                return overrideInputAction.ReadValue<Vector2>();
            }
            else
            {
                var leftValue = leftAction.action.ReadValue<Vector2>();
                var rightValue = rightAction.action.ReadValue<Vector2>();
                return leftValue.magnitude > rightValue.magnitude ? leftValue : rightValue;
            }
        }
    }
}