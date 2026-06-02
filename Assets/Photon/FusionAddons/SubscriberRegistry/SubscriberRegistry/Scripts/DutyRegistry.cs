using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.Addons.SubscriberRegistry
{
    /// <summary>
    /// Registry that is only considered available for subscriber registratrion when the state authority is still present.
    /// Use this kind of registry if you require a centralised action by the state authority registry upon subscriber registration 
    /// (for instance, editing a network variable based on this registration)
    /// This is usefull for instance on the Meta Quest, where an user quitting the application does not disconnect immediatly, so the state authority player might seem to be still conencted,
    /// while in fact they are disconnecting
    /// 
    /// Note: usually, you want to have a way to ensure that someone has the state authority on those registry, 
    ///  either by setting MaintainPermanence to true, by setting their IsMasterCLient on their NetworkObject, or by using Object.AffectStateAuthorityIfNone() in its Render()
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DutyRegistry<T> : Registry<T> where T : Subscriber<T>, IRegistrySubscriber<T>
    {
        [Networked, OnChangedRender(nameof(OnPermanenceCheckChange))]
        byte PermanenceCheck { get; set; } = 0;

        float lastStateAuthorityPresenceDetected = -1;

        [Tooltip("A DutyRegistry is considered available only if a state authority activity was detected in this recent seconds")]
        public float maxStateAuthorityUpdateForRegistryPermanence = 1;

        [Tooltip("If not 0, the state authority will notify other user that it is still present every permanenceRefreshDelay seconds (should be large under maxStateAuthorityUpdateForRegistryPermanence)")]
        public float permanenceRefreshDelay = 0;
        float lastPermanenceRefresh = -1;

        public override bool IsAvailable => base.IsAvailable && (Time.time - lastStateAuthorityPresenceDetected) < maxStateAuthorityUpdateForRegistryPermanence;

        public virtual  bool MaintainDuty => true;

        void OnPermanenceCheckChange()
        {
            lastStateAuthorityPresenceDetected = Time.time;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (permanenceRefreshDelay == 0 || lastPermanenceRefresh == -1 || (Time.time - lastPermanenceRefresh) > permanenceRefreshDelay)
            {
                lastPermanenceRefresh = Time.time;
                PermanenceCheck++;
            }
        }

        public override void Render()
        {
            base.Render();
            if (MaintainDuty)
            {
                Object.AffectStateAuthorityIfNone();
            }
        }
    }
}

