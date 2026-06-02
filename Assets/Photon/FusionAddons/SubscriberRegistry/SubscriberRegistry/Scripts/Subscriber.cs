using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.SubscriberRegistry
{
    public interface ISubscriber
    {
        public bool IsAvailable { get; }
    }

    /// <summary>
    /// Describe a subscriber that will register to a Register<T> when both of them are available (IsAvailable = true). Note, IsAvailalbe criteria needs to be synched
    /// All clients can receive notification of registration by overriding OnRegisterOn(IRegistry<T> registry) / OnUnregisterOn(IRegistry<T> registry)
    ///  or in another component by implementing RegistryListener<T> and using IRegisterListeners(RegistryListener<T> listener)
    /// 
    /// Typical usage for sub-classes:
    /// class SampleSubscriber : Subscriber<SampleSubscriber>
    /// {
    ///    // Custom availiblity criteria
    ///    [Networked]
    ///    bool CustomAvailabilityCriteriaMatched { get; set; } = false;
    ///
    ///    // Availability override. The data to compute it needs to be synched so that the availability triggers everywhere (and hence the registration)
    ///    public override bool IsAvailable => base.IsAvailable && CustomAvailabilityCriteriaMatched;
    /// }
    /// 
    /// </summary>
    public abstract class Subscriber<T> : NetworkBehaviour, IRegistrySubscriber<T>, ISubscriber where T : Subscriber<T>
    {


        public List<Registry<T>> registeredOnRegistries = new List<Registry<T>>();
        public List<Registry<T>> registriesToSubscribeToWhenAvailable = new List<Registry<T>>();
        public List<Registry<T>> unavailableRegistriesToSubscribeTo = new List<Registry<T>>();
        List<IRegistrationListener<T>> registeredListeners = new List<IRegistrationListener<T>>();
        protected bool spawned = true;

        // IsAvailable = true is required before the subscriber registers
        public virtual bool IsAvailable => Object != null && spawned;

        // If true, will automatically unregister if it becomes unavailable after having registered
        public virtual bool UnregisterWhenNotAvailable => true;
        // If true, will automatically unregister when the registry becomes unavailable
        protected virtual bool UnregisterOnUnavailableRegistry => false;

        bool wasAvailable = false;
        bool didUnregisterDueToNotAvailable = false;

        #region Callbacks
        // Called when registry is available, before checking if we are available to register
        protected virtual void OnAvailableRegistryFound(Registry<T> registry) {
            foreach (var l in registeredListeners)
            {
                if(l is IDetailedRegistrationListener<T> detailedListerner)
                {
                    detailedListerner.OnAvailableRegistryFound(registry, this);
                }
            }
        }
      
        protected virtual void OnRegisterOnRegistry(Registry<T> registry) {
            foreach (var l in registeredListeners) l.OnSubscriberRegistration(registry, this);
        }
        protected virtual void OnUnregisterOnRegistry(Registry<T> registry) {
            foreach (var l in registeredListeners) l.OnSubscriberUnregistration(registry, this);
        }
        #endregion

        #region IRegistrySubscriber

        protected virtual bool IsCompatibleRegistryType(Registry<T> registry)
        {
            var registryType = registry.GetType();
            return registryType == RegistryType() || registryType.IsSubclassOf(RegistryType());
        }

        public void OnRegistryAvailable(Registry<T> registry)
        {
            if (IsCompatibleRegistryType(registry))
            {
                OnRegistryFound(registry);
            }
        }

        public void OnRegistryUnavailable(Registry<T> registry, bool despawningRegistry)
        {
            bool shouldUnregister = despawningRegistry || UnregisterOnUnavailableRegistry;
            if (shouldUnregister && IsCompatibleRegistryType(registry) && registeredOnRegistries.Contains(registry))
            {
                UnregisterOnRegistry(registry);
            }
        }
        #endregion

        protected void OnRegistryFound(Registry<T> registry)
        {
            if (registry.IsAvailable)
            {
                OnAvailableRegistryFound(registry);
            }
            if (registry.IsAvailable && IsAvailable)
            {
                RegisterOnRegistry(registry);
            }
            else
            {
                // Delay registration
                if (registry.IsAvailable)
                {
                    if (registriesToSubscribeToWhenAvailable.Contains(registry) == false)
                    {
                        registriesToSubscribeToWhenAvailable.Add(registry);
                    }
                }
                else
                {
                    if (unavailableRegistriesToSubscribeTo.Contains(registry) == false)
                    {
                        unavailableRegistriesToSubscribeTo.Add(registry);
                    }
                }
            }
        }

        public virtual void RegisterOnRegistry(Registry<T> registry)
        {
            if(this is T subscriber && registeredOnRegistries.Contains(registry) == false)
            {
                registry.RegisterSubscriber(subscriber);
                registeredOnRegistries.Add(registry);
                OnRegisterOnRegistry(registry);
            }
        }

        protected virtual void UnregisterOnRegistry(Registry<T> registry, bool removeFromRegistries = true)
        {
            if (this is T subscriber) {
                registry.UnregisterSubscriber(subscriber);
                if (removeFromRegistries) registeredOnRegistries.Remove(registry);
                OnUnregisterOnRegistry(registry);
            }
        }
        
        protected virtual List<Registry<T>> FindAllRegistries()
        {
            var targets = new List<Registry<T>>();
            var t = RegistryType();
            var registries = FindObjectsByType(t,FindObjectsSortMode.None);
            foreach (var r in registries)
            {
                if (r is Registry<T> targetRegistry && targetRegistry.Runner == Runner)
                {
                    targets.Add(targetRegistry);
                }
            }
            return targets;
        }

        public virtual System.Type RegistryType()
        {
            return typeof(Registry<T>);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            if (!hasState)
            {
                // Called during shutdown, or before Spawn
                return; 
            }
            Unregister();
        }

        protected void Unregister()
        {
            foreach (var registry in registeredOnRegistries)
            {
                if ((Object)registry != null)
                {
                    UnregisterOnRegistry(registry, removeFromRegistries: false);
                }
            }
            registeredOnRegistries.Clear();
        }

        #region NetworkBehaviour
        public override void Spawned()
        {
            base.Spawned();
            spawned = true;
            LookForRegistries();
        }

        // Look for available registries. Called during spawn, but can be called manually to trigger registration if availability has switch to false then true
        protected void LookForRegistries()
        {
            foreach (var r in FindAllRegistries())
            {
                OnRegistryFound(r);
            }
        }

        public override void Render()
        {
            base.Render();
            CheckPendingRegistrations();
            CheckUnavailbilityUnregister();            
        }
        #endregion

        protected void CheckUnavailbilityUnregister()
        {
            if (UnregisterWhenNotAvailable)
            {
                if (IsAvailable)
                {
                    wasAvailable = true;
                }
                if (IsAvailable == false && wasAvailable)
                {
                    Unregister();
                    didUnregisterDueToNotAvailable = true;
                    wasAvailable = false;
                }
                if (didUnregisterDueToNotAvailable && IsAvailable)
                {
                    // We need to register again on the current registries: looking for them (similar to spawn logic)
                    LookForRegistries();
                    didUnregisterDueToNotAvailable = false;
                }
            }
        }

        protected virtual void BroadCastAvailability()
        {
            foreach (var r in FindAllRegistries())
            {
                OnRegistryFound(r);
            }
        }

        protected void CheckPendingRegistrations()
        {
            foreach (var r in unavailableRegistriesToSubscribeTo)
            {
                if (r.IsAvailable)
                {
                    OnAvailableRegistryFound(r);
                    registriesToSubscribeToWhenAvailable.Add(r);
                }
            }

            // Clear registries just added from unavailableRegistriesToSubscribeTo as they became available
            foreach (var r in registriesToSubscribeToWhenAvailable)
            {
                if (unavailableRegistriesToSubscribeTo.Contains(r))
                {
                    unavailableRegistriesToSubscribeTo.Remove(r);
                } 
                else if (r.IsAvailable == false)
                {
                    // While waiting for the subscriber to become available, the registry is now not anymore
                    unavailableRegistriesToSubscribeTo.Add(r);
                }
            }

            // Cleanup for registries having become unavailable
            foreach (var r in unavailableRegistriesToSubscribeTo)
            {
                if (r.IsAvailable == false)
                {
                    registriesToSubscribeToWhenAvailable.Remove(r);
                }
            }

            if (IsAvailable)
            {
                foreach (var r in registriesToSubscribeToWhenAvailable)
                {
                    OnRegistryAvailable(r);
                }
                registriesToSubscribeToWhenAvailable.Clear();
            }
        }

        #region Listeners registration
        public void RegisterListener(IRegistrationListener<T> listener, bool notifyOfExistingRegistration = false)
        {
            if (registeredListeners.Contains(listener) == false)
            {
                registeredListeners.Add(listener);
                if (notifyOfExistingRegistration)
                {
                    foreach(var r in registeredOnRegistries)
                    {
                        listener.OnSubscriberRegistration(r, this);
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

    public interface IDetailedRegistrationListener<T> : IRegistrationListener<T> where T : Subscriber<T>, IRegistrySubscriber<T>
    {
        public void OnAvailableRegistryFound(Registry<T> registry, Subscriber<T> subscriber);
    }
}
