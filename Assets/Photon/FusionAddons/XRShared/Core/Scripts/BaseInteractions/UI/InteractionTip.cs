using System.Runtime.CompilerServices;
using UnityEngine;

namespace Fusion.XR.Shared.Core.Interaction
{
    public interface IInteractionTip : IUnityBehaviour
    {
        /// <summary>
        /// Max distance with an UI element that can trigger starting an interaction
        /// </summary>
        public float MaxStartInteractionDistance { get; }
        /// <summary>
        /// Distance at which the proximity of an UI element is detected 
        /// </summary>
        public float MaxInteractionScanDistance { get; }
        public float MaxMaintainInteractionDepth { get; }
        public IRigPart RigPart { get; }
        public bool CanInteract { get; }
        public bool IsSelecting { get; }
        public Vector3 Origin { get; }
        public Quaternion Rotation { get; }
        public Vector2 ScrollDelta { get; }
        public IInteractionDetailsProvider LastInteractionDetailProvider { get; set; }
    }

    public static class IInteractionTipExtension
    {
        public static bool CanMaintainInteraction(this IInteractionTip tip)
        {
            return tip.CanInteract && tip.MaxMaintainInteractionDepth > 0;
        }
    }

    public interface IInteractionDetailsProvider
    {
        public Vector3 LastInteractionWorldPosition { get; }
        public bool IsMaintainedInteraction { get; }
        public float MaintainDepth { get; }
        public GameObject Target { get; }
    }

    public interface IInteractionTipListener
    {
        public void OnDidInteract(IInteractionDetailsProvider detailsProvider);
    }
}
