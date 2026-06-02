using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion.Addons.SubscriberRegistry;
using UnityEngine.Events;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.Reconnection {
    public interface IReconnectionHandlerListener : IRegistrationListener<ReconnectionHandler> { }

    /// <summary>
    /// Placed on the spawned user object, check if a reconnection occured, by using the player userId (uniquely generated, or based on authentication if it is enabled)
    /// Requires a network object with a ReconnectionManager in the scene, that keep track of all the previously connected users in a session.
    /// Will trigger a recovery on Recoverable objects (will take back authority on them, then trigger OnRecovered, overridable here and available in the Recoverable listeners)
    /// </summary>
    public class ReconnectionHandler : Subscriber<ReconnectionHandler>, IRecoverRequester
    {
        [Header("ReconnectionHandler")]
        [HideInInspector]
        // Used to access UserId during OnDestroy
        public string cachedUserId;
        // Used to access Id during OnDestroy
        public NetworkBehaviourId cachedId;

        [Networked, OnChangedRender(nameof(OnUserIdChange))]
        public NetworkString<_64> UserId { get; set; } 

        const string CACHED_USERID_KEY = "CACHED_USERID";

        public UnityEvent<Recoverable> onRecovery;
        public UnityEvent<ReconnectionHandler> willReconnectWithPreviousHandlerStillInPlace;

        public enum ReconnectionStatus
        {
            CheckingReconnection,
            ReconnectedUser,
            NewUser,
            Disabled
        }

        public enum UserIdCollisionHandling {
            // Consider the collision user id comes from a reconnecting user that is still in the room because its disonnection was not detected (under timeout and no clean quit)
            AlwaysConsiderReconnectionUnderTimeout,
            // Consider user id collision is just a collision, calling OnUserIdCollision (this handler won't take its Recoverable back)
            AlwaysConsiderCollision,
            // Check if LastReceivedTick evolves after collisionCheckLastReceivedTickEvolutionWindow seconds, to choose betwene reconnection (value does not evolve - probably handled by our previous disconnecting user) and collision (value is evolving). Mostly relevant in XR, where we are sure that the user always send data
            CheckIfLastReceivedTickEvolves,
        }
        public UserIdCollisionHandling userIdCollisionHandling = ReconnectionHandler.UserIdCollisionHandling.AlwaysConsiderReconnectionUnderTimeout;
        [DrawIf(nameof(userIdCollisionHandling), (long)UserIdCollisionHandling.CheckIfLastReceivedTickEvolves, CompareOperator.Equal, Hide = true)]
        public float collisionCheckLastReceivedTickEvolutionWindow = 2;

        [Networked, OnChangedRender(nameof(OnStatusChange))]
        public ReconnectionStatus Status { get; set; } = ReconnectionStatus.CheckingReconnection;

        #region Subscriber overrides
        public override bool IsAvailable => base.IsAvailable && Status != ReconnectionStatus.CheckingReconnection && Status != ReconnectionStatus.Disabled && UserId != "";
        public override bool UnregisterWhenNotAvailable => true;

        protected override void OnAvailableRegistryFound(Registry<ReconnectionHandler> registry)
        {
            base.OnAvailableRegistryFound(registry);
            if (Object.HasStateAuthority && Status == ReconnectionStatus.CheckingReconnection && registry is ReconnectionManager reconnectionManager)
            {
                var previouslyStoredUserId = PreviouslyStoredUserId();
                bool isNewUser = true;
                foreach (var user in reconnectionManager.SessionUsers)
                {
                    if (user.userId == previouslyStoredUserId)
                    {
                        isNewUser = false;
                        if (user.reconnectionHandlerBehaviourId != NetworkBehaviourId.None && Runner.TryFindBehaviour<ReconnectionHandler>(user.reconnectionHandlerBehaviourId, out var existingHandler) && Runner.IsPlayerValid(existingHandler.Object.StateAuthority))
                        {
                            // UserId already used by a connected user: unexpected collision (issue in user id generation logic override ?)
                            Debug.LogError($"Collision: Previous user.reconnectionHandlerBehaviourId: {user.reconnectionHandlerBehaviourId} " +
                                $"existingHandler state auth: {existingHandler.Object.StateAuthority} " +
                                $"state auth is valid: {Runner.IsPlayerValid(existingHandler.Object.StateAuthority)} " +
                                $"");
                            switch (userIdCollisionHandling)
                            {
                                case UserIdCollisionHandling.AlwaysConsiderCollision:
                                    OnUserIdCollision();
                                    break;
                                case UserIdCollisionHandling.AlwaysConsiderReconnectionUnderTimeout:
                                    OnIsReconnectingWithPreviousHandlerStillInPlace(existingHandler);
                                    break;
                                case UserIdCollisionHandling.CheckIfLastReceivedTickEvolves:
                                    CheckCollision(existingHandler);
                                    break;
                            }
                        } 
                        else
                        {
                            OnIsReconnecting();
                        }
                        break;
                    }
                }
                if (isNewUser)
                {
                    // We generate a new user id
                    OnIsNewPlayer();
                }
            }
        }
        
        public async void CheckCollision(ReconnectionHandler existingHandler)
        {
            var lastReceivedTick = existingHandler.Object.LastReceiveTick;
            await AsyncTask.Delay((int)(1000 * collisionCheckLastReceivedTickEvolutionWindow));
            if (existingHandler == null || existingHandler.Object == null || lastReceivedTick == existingHandler.Object.LastReceiveTick)
            {
                // Probably a short disconnection, with the previous ReconnectionHandler still not yet destroyed (timeout did not trigger yet)
                // We ignore the collision and take the "previous" handle slot
                OnIsReconnectingWithPreviousHandlerStillInPlace(existingHandler);
            }
            else
            {
                Debug.LogError("Collision result: actual user collision");
                OnUserIdCollision();
            }
        }

        public override System.Type RegistryType()
        {
            return typeof(ReconnectionManager);
        }

        protected override List<Registry<ReconnectionHandler>> FindAllRegistries()
        {
            var registries = base.FindAllRegistries();
#if DEBUG
            if(registries.Count == 0)
            {
                // The ReconnectionManager registry might be spawning. But it might also be missing. Prepare to warn the user
                StartCoroutine(CheckRegistryPresence());
            }
#endif
            return registries;
        }
        #endregion

        IEnumerator CheckRegistryPresence()
        {
            yield return new WaitForSeconds(3);
            var registries = base.FindAllRegistries();
            if (registries.Count == 0)
            {
                Debug.LogError("[Error] No ReconnectionManager in the scene: needed for reconnection logic");
            }
        }
        #region IRecoverRequester
        public virtual void OnRecovered(Recoverable recovered) {
            if (onRecovery != null)
            {
                onRecovery.Invoke(recovered);
            }
        }
        #endregion

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                if (string.IsNullOrEmpty(PreviouslyStoredUserId()) == false)
                {
                    // We have a previous user ID: possibly reconnecting, we check if we left a RrconnectionInfo before reconnecting
                    OnCheckReconnection();
                }
                else
                {
                    OnIsNewPlayer();
                }
            } 
            OnUserIdChange();
            cachedId = Id;
            base.Spawned();
        }

        void OnCheckReconnection()
        {
            Status = ReconnectionStatus.CheckingReconnection;
        }

        protected virtual void OnIsReconnectingWithPreviousHandlerStillInPlace(ReconnectionHandler existingHandler)
        {
            Debug.LogError("Collision result: was a short disconnection, under timeout");
            if (willReconnectWithPreviousHandlerStillInPlace != null)
            {
                willReconnectWithPreviousHandlerStillInPlace.Invoke(existingHandler);
            }
            OnIsReconnecting();
        }

        protected virtual void OnIsReconnecting()
        {
            UserId = PreviouslyStoredUserId();
            Status = ReconnectionStatus.ReconnectedUser;
            Recoverable.RequestAuthorityOnRecoverablesWithUserId(UserId.ToString(), requester: this);
        }

        protected virtual void OnIsNewPlayer()
        {
            UserId = GenerateUserId();
            Status = ReconnectionStatus.NewUser;
        }

        protected virtual void OnUserIdCollision()
        {
            Debug.LogError("[Error] UserId already connected: unexpected collision");
            OnIsNewPlayer();
        }

        #region UserId handling
        protected virtual string UserIdKey()
        {
            // We use the path hashcode in the key, in case of multiple installs (relevant mostly in the editor, while testing with several instances)
            return $"{CACHED_USERID_KEY}-{Application.dataPath.GetHashCode()}";
        }

        protected virtual string PreviouslyStoredUserId()
        {
            if (Object.HasStateAuthority == false) throw new System.Exception("Should be called by local user only");

            var previouslyStoredUserId = PlayerPrefs.GetString(UserIdKey());
            return previouslyStoredUserId;
        }

        protected virtual string GenerateUserId()
        {
            var userId = Runner.GetPlayerUserId(Runner.LocalPlayer);
            if (userId.Length > 64) throw new System.Exception("User id too long: extend UserId capacity and change this check");
            PlayerPrefs.SetString(UserIdKey(), userId);
            return userId;
        }

        protected virtual void OnUserIdChange()
        {
            cachedUserId = UserId.ToString();
        }
        #endregion

        #region Manual enable/disable reconnection
        [EditorButton("DisableReconnection")]
        public void DisableReconnection()
        {
            if (Object.HasStateAuthority == false)
            {
                throw new System.Exception("[Error] DisableReconnection should be called on state authority");
            }
            UserId = null;
            Status = ReconnectionStatus.Disabled;
        }

        [EditorButton("ReenableReconnection")]
        public void ReenableReconnection()
        {
            if (Object.HasStateAuthority == false)
            {
                throw new System.Exception("[Error] DisableReconnection should be called on state authority");
            }
            if (Status != ReconnectionStatus.Disabled)
            {
                return;
            }
            OnCheckReconnection();
            // We look for registries, to trigger the check of the RegistryManager userId, to fill our own userId again in OnAvailableRegistryFound
            LookForRegistries();
        }

        protected virtual void OnStatusChange(NetworkBehaviourBuffer previousBuffer)
        {
        }
        #endregion
    }
}
