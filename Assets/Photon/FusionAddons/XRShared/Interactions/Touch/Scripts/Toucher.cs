using UnityEngine;

namespace Fusion.XR.Shared.Core.Touch
{
    public interface ITouchable
    {
        public void OnToucherContactStart(Toucher toucher);
        public void OnToucherStay(Toucher toucher);
        public void OnToucherContactEnd(Toucher toucher);
    }

    /**
     * Allow to detect ITouchable components in contact.
     * 
     * While Toucher should be put on the hardware rig for better performances (no collider event triggered by remote rigs), it theorically can be placed on a network rig. In this case, set onlyTriggerCallbacksWhenStateAuthority accordingly
     */
    public class Toucher : MonoBehaviour
    {
        [HideInInspector]
        public IRigPart rigPart;
        [Header("Callback options")]
        [SerializeField]
        protected bool lookForTouchableInColliderParent = true;

        [Header("Network context usage")]
        public bool onlyTriggerCallbacksWhenStateAuthority = true;

        protected virtual void Awake()
        {
            rigPart = GetComponentInParent<IRigPart>();
        }

        protected Collider lastCheckCollider = null;
        ITouchable lastCheckedTouchable = null;
        ITouchable LookForTouchable(Collider other)
        {
            if (other != lastCheckCollider)
            {
                CheckCollider(other);
            }
            return lastCheckedTouchable;
        }

        protected virtual void CheckCollider(Collider other)
        {
            lastCheckCollider = other;
            if (lookForTouchableInColliderParent)
            {
                lastCheckedTouchable = other.GetComponentInParent<ITouchable>();
            }
            else
            {
                lastCheckedTouchable = other.GetComponent<ITouchable>();
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (ShouldIgnoreTouch())
            {
                return;
            }
            ITouchable otherGameObjectTouchable = LookForTouchable(other);
            if (otherGameObjectTouchable != null)
            {
                otherGameObjectTouchable.OnToucherContactStart(this);
            }
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            if (ShouldIgnoreTouch())
            {
                return;
            }
            ITouchable otherGameObjectTouchable = LookForTouchable(other);
            if (otherGameObjectTouchable != null)
            {
                otherGameObjectTouchable.OnToucherStay(this);
            }
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (ShouldIgnoreTouch())
            {
                return;
            }
            ITouchable otherGameObjectTouchable = LookForTouchable(other);
            if (otherGameObjectTouchable != null)
            {
                otherGameObjectTouchable.OnToucherContactEnd(this);
            }
        }

        protected virtual bool ShouldIgnoreTouch()
        {
            return onlyTriggerCallbacksWhenStateAuthority && rigPart is INetworkRigPart networkRigPart && networkRigPart.Object && networkRigPart.Object.HasStateAuthority == false;
        }
    }

}

