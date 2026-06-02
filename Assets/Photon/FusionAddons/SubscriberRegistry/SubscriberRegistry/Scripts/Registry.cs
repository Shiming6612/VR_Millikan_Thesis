using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.SubscriberRegistry
{
    public interface IRegistrationListener<T> where T: Subscriber<T>, IRegistrySubscriber<T> 
    {
        public void OnSubscriberRegistration(Registry<T> registry, Subscriber<T> subscriber);
        public void OnSubscriberUnregistration(Registry<T> registry, Subscriber<T> subscriber);

    }

    public interface IRegistrySubscriber<T> where T : Subscriber<T>, IRegistrySubscriber<T>
    {
        public void OnRegistryAvailable(Registry<T> registry);
        public void OnRegistryUnavailable(Registry<T> registry, bool despawningRegistry);
#pragma warning disable IDE1006 // Naming Styles
        public GameObject gameObject { get; }
#pragma warning restore IDE1006 // Naming Styles
        public NetworkRunner Runner { get; }
    }

    public interface IRegistry
    {
        public bool IsAvailable { get; }
        public NetworkObject Object { get; }
    }

    /// <summary>
    /// Describe a registry that will register a subscriber T, when both of them are available (IsAvailable = true)
    /// All clients can receive notification of registration, either by overriding OnSubscriberRegistration/OnSubscriberUnregistration,
    ///  or in another component by implementing RegistryListener<T> and using IRegisterListeners(RegistryListener<T> listener)
    ///
    /// Typical usage for sub-classes:
    /// class SampleRegistry : Registry<SampleSubscriber> {}
    /// </summary>
    public class Registry<T> : NetworkBehaviour, IRegistry where T: Subscriber<T>, IRegistrySubscriber<T>
    {
        public List<T> registeredSubscribers = new List<T>();
        List<IRegistrationListener<T>> registeredListeners = new List<IRegistrationListener<T>>();

        // If true, will automatically warn potential subscriber of its presence, once available
        protected virtual bool BroadcastAvailabilityToAllSubscribers => true;
        // If true, will automatically warn subscribers if it becomes unavailable after they have registered
        protected virtual bool WarnSubscribersOfUnavailability => true;

        protected bool availabilityBroadcasted = false;
        protected bool unavailabilityBroadcasted = false;
        protected bool spawned = true;

        bool isDestroying = false;

        #region Availability
        // IsAvailable = true is required before a subscriber can register
        public virtual bool IsAvailable => spawned;

        protected void CheckBroadCastAvailability()
        {
            // Broadcast availability if IsAvailable = true
            if (BroadcastAvailabilityToAllSubscribers)
            {
                if (IsAvailable)
                {
                    if (availabilityBroadcasted == false)
                    {
                        BroadCastAvailability();
                        availabilityBroadcasted = true;
                    }
                } 
                else if(availabilityBroadcasted)
                {
                    availabilityBroadcasted = false;
                }

            }
            if (WarnSubscribersOfUnavailability)
            {
                if (IsAvailable == false)
                {
                    if (unavailabilityBroadcasted == false)
                    {
                        BroadCastUnavailabilityToRegisteredSubscribers();
                        unavailabilityBroadcasted = true;
                    }
                } 
                else if(unavailabilityBroadcasted)
                {
                    unavailabilityBroadcasted = false;
                }
            }
        }

        protected virtual void BroadCastAvailability()
        {
            foreach (var s in FindAllPotentialSubscribers())
            {
                s.OnRegistryAvailable(this);
            }
        }

        protected virtual void BroadCastUnavailabilityToRegisteredSubscribers(bool despawningRegistry = false)
        {
            foreach (var r in registeredSubscribers)
            {
                if (r != null)
                {
                    r.OnRegistryUnavailable(this, despawningRegistry);
                }
            }
        }

        protected virtual List<T> FindAllPotentialSubscribers()
        {
            var targets = new List<T>();
            foreach (var s in FindObjectsByType(SubscriberType(),FindObjectsSortMode.None))
            {
                if (s is T targetSubscriber && targetSubscriber.Runner == Runner)
                {
                    targets.Add(targetSubscriber);
                }
            }
            return targets;
        }

        public virtual System.Type SubscriberType()
        {
            return typeof(T);
        }
        #endregion

        #region NetworkBehaviour
        public override void Spawned()
        {
            base.Spawned();
            spawned = true;
            CheckBroadCastAvailability();
        }

        public override void Render()
        {
            base.Render();
            CheckBroadCastAvailability();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            if (!hasState)
            {
                // Called during shutdown, or before Spawn
                return;
            }
            isDestroying = true;
            BroadCastUnavailabilityToRegisteredSubscribers(despawningRegistry: true);
        }
        #endregion

        #region Subscribers registration
        public void RegisterSubscriber(T subscriber)
        {
            if (registeredSubscribers.Contains(subscriber) == false)
            {
                registeredSubscribers.Add(subscriber);
                OnSubscriberRegistration(subscriber);
            }
        }

        public void UnregisterSubscriber(T subscriber)
        {
            if (registeredSubscribers.Contains(subscriber))
            {
                // We don't edit our registeredSubscribers list while destroying (this unsubscribe is due to our current destroy, and would edit a list being enumerated)
                if (isDestroying == false)
                {
                    registeredSubscribers.Remove(subscriber);
                }
                OnSubscriberUnregistration(subscriber);
            }
        }

        protected virtual void OnSubscriberRegistration(T subscriber)
        {
            foreach (var l in registeredListeners) l.OnSubscriberRegistration(this, subscriber);
        }

        protected virtual void OnSubscriberUnregistration(T subscriber)
        {
            foreach (var l in registeredListeners) l.OnSubscriberUnregistration(this, subscriber);
        }
        #endregion

        #region Listeners registration
        public void RegisterListener(IRegistrationListener<T> listener, bool notifyOfExistingRegistration = false)
        {
            if (registeredListeners.Contains(listener) == false)
            {
                registeredListeners.Add(listener);
                if (notifyOfExistingRegistration)
                {
                    foreach (var s in registeredSubscribers)
                    {
                        listener.OnSubscriberRegistration(this, s);
                    }
                }
            }
        }

        public void UnregisterListener(IRegistrationListener<T> listeners)
        {
            if (registeredListeners.Contains(listeners))
            {
                registeredListeners.Remove(listeners);
            }
        }
        #endregion

    }

}
