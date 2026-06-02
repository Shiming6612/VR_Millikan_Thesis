using Fusion.Addons.SubscriberRegistry;
using Fusion.XR.Shared.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Reconnection
{
    [System.Serializable]
    public struct SessionUser : INetworkStruct
    {
        public NetworkBehaviourId reconnectionHandlerBehaviourId;
        public NetworkString<_64> userId;
    }

    public class ReconnectionManager : Registry<ReconnectionHandler>, IStateAuthorityChanged
    {
        const int MAX_USER_HISTORY = 50;
        [Networked, OnChangedRender(nameof(OnSessionUsersChange))]
        [Capacity(MAX_USER_HISTORY)]
        [UnitySerializeField]
        public NetworkArray<SessionUser> SessionUsers { get; }

        public enum TimeoutLogic {
            RemovePlayerHistoryOnNewUserAlways,
            RemovePlayerHistoryOnNewUserWithNoSlotAvailable,
        }
        public TimeoutLogic timeoutLogic = TimeoutLogic.RemovePlayerHistoryOnNewUserWithNoSlotAvailable;

        float maxReconnectionTime = 120;

        Dictionary<string, float> lastConnectionTimeByUserId = new Dictionary<string, float>();

        #region Registry overrides
        protected override void OnSubscriberRegistration(ReconnectionHandler handler)
        {
            base.OnSubscriberRegistration(handler);
            UpdateSessionUsersWithReconnectionHandler(handler);
        }

        void UpdateSessionUsersWithReconnectionHandler(ReconnectionHandler handler)
        {
            if (handler.UserId != "")
            {
                lastConnectionTimeByUserId[handler.UserId.ToString()] = Time.time;
            }
            if (Object.HasStateAuthority)
            {
                var newUser = true;
                for (int i = 0; i < SessionUsers.Length; i++)
                {
                    if (SessionUsers[i].userId == handler.UserId)
                    {
                        newUser = false;
                        UpdateReconnectionHandlerInfo(newHandler: handler, index: i);
                        break;
                    }
                }
                if (newUser)
                {
                    StoreNewReconnectionHandlerInfo(handler);
                }
            }
        }

        protected override void OnSubscriberUnregistration(ReconnectionHandler subscriber)
        {
            base.OnSubscriberUnregistration(subscriber);
            // We use the cached versions, as the subscriber might be being destroyed
            var userId = subscriber.UserId;
            var subscriberId = subscriber.Id;

            if (userId != "")
            {
                lastConnectionTimeByUserId[userId.ToString()] = Time.time;
            }
            if (Object == null) return;
            for (int i = 0; i < SessionUsers.Length; i++)
            {
                if (SessionUsers[i].reconnectionHandlerBehaviourId == subscriberId)
                {
                    if (userId == SessionUsers[i].userId)
                    {
                        // Removing the behaviour info, keep the user id for recovery
                        UpdateReconnectionHandlerInfo(newHandler: null, index: i);
                    } 
                    else
                    {
                        // User id has been changed (probably set to ""): remove the entry
                        RemoveReconnectionHandlerInfo(i);
                    }
                }
            }
        }

        public override Type SubscriberType()
        {
            return typeof(ReconnectionHandler);
        }
        #endregion

        public override void Render()
        {
            base.Render();
            Object.AffectStateAuthorityIfNone();
        }

        protected virtual void UpdateReconnectionHandlerInfo(ReconnectionHandler newHandler, int index)
        {
            var newSessionInfo = SessionUsers[index];
            newSessionInfo.reconnectionHandlerBehaviourId = newHandler == null ? NetworkBehaviourId.None : newHandler.Id;
            SessionUsers.Set(index, newSessionInfo);
        }

        protected virtual void RemoveReconnectionHandlerInfo(int index)
        {
            var newSessionInfo = new SessionUser();
            SessionUsers.Set(index, newSessionInfo);
        }

        protected virtual void StoreNewReconnectionHandlerInfo(ReconnectionHandler handler)
        {
            var newSessionInfo = new SessionUser();
            newSessionInfo.reconnectionHandlerBehaviourId = handler.Id;
            newSessionInfo.userId = handler.UserId;
            int availableSlot = -1;
            int timoutSlot = -1;
            for (int i = 0; i < MAX_USER_HISTORY; i++)
            {
                var existingUserId = SessionUsers[i].userId;
                if (existingUserId == "" )
                {
                    availableSlot = i;
                    break;
                }
                else if (SessionUsers[i].reconnectionHandlerBehaviourId == NetworkBehaviourId.None)
                {
                    // Can we reuse this slot of a disconencted user ?
                    if (lastConnectionTimeByUserId.ContainsKey(existingUserId.ToString()) == false)
                    {
                        // We release this entry (missing timing data)
                        lastConnectionTimeByUserId.Remove(existingUserId.ToString());
                        availableSlot = i;
                        break;
                    }
                    else if (lastConnectionTimeByUserId.ContainsKey(existingUserId.ToString()) == false || (Time.time - lastConnectionTimeByUserId[existingUserId.ToString()]) > maxReconnectionTime)
                    {
                        if(timeoutLogic == TimeoutLogic.RemovePlayerHistoryOnNewUserAlways)
                        {
                            // We release this entry (timeout)
                            OnUserTimeout(existingUserId);
                            availableSlot = i;
                            break;
                        } 
                        else if(timoutSlot == -1)
                        {
                            timoutSlot = i;
                        }
                    }
                }

            }
            if (availableSlot == -1 && timoutSlot != -1)
            {
                OnUserTimeout(SessionUsers[timoutSlot].userId);
                availableSlot = timoutSlot;
            }
            if (availableSlot == -1)
            {
                throw new Exception($"[Error] Max user history count ({MAX_USER_HISTORY}) reached for ReconnectionManager " + this);
            }
            SessionUsers.Set(availableSlot, newSessionInfo);
        }

        protected virtual void OnUserTimeout(NetworkString<_64> existingUserId)
        {
            lastConnectionTimeByUserId.Remove(existingUserId.ToString());
        }

        protected virtual void OnSessionUsersChange(NetworkBehaviourBuffer previousBuffer)
        {
        }

        #region IStateAuthorityChanged
        public virtual void StateAuthorityChanged()
        {
            if (Object.HasStateAuthority)
            {
                // Clean the reconnectionHandlerBehaviourId (typically for the previous state authority of the reconnectionManager, as it was not able to clean its entry itself on disconnecting)
                for (int i = 0; i < SessionUsers.Length; i++)
                {
                    var userInfo = SessionUsers[i];
                    if(userInfo.userId == "")
                    {
                        continue;
                    }

                    if (Runner.TryFindBehaviour<ReconnectionHandler>(userInfo.reconnectionHandlerBehaviourId, out _) == false)
                    {
                        // Missing ReconnectionHandler in the room: clearing it
                        userInfo.reconnectionHandlerBehaviourId = NetworkBehaviourId.None;
                        SessionUsers.Set(i, userInfo);
                    }

                    // We reset the last connection time, as we may have connected while this player was already away
                    lastConnectionTimeByUserId[userInfo.userId.ToString()] = Time.time;
                }

                // Check that during the state authority disconnection, some registration occured, but where not stored in the networked linked list
                foreach (var handler in registeredSubscribers)
                {
                    bool found = false;
                    foreach(var userInfo in SessionUsers)
                    {
                        if(userInfo.reconnectionHandlerBehaviourId == handler.Id)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == false)
                    {
                        Debug.LogError("[Debug] Recover missing handler info (due to previous state auth disconnecting while this user connected)");
                        UpdateSessionUsersWithReconnectionHandler(handler);
                    }
                }
            }
        }
        #endregion
    }
}
