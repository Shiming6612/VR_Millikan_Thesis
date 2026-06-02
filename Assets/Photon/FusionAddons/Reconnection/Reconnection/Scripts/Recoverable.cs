using System.Collections.Generic;
using UnityEngine;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.Reconnection
{
    public interface IRecoverRequester
    {
        void OnRecovered(Recoverable recovered);
    }

    public interface IRecoverableListener
    {
        void OnRecovered(Recoverable recovered, IRecoverRequester requester);
    }

    public class Recoverable : NetworkBehaviour
    {
        [Networked]
        public NetworkString<_64> UserId { get; set; }

        // Store the potential requests before this object spawns
        Dictionary<string, IRecoverRequester> recoverRequestsForUserId = new Dictionary<string, IRecoverRequester>();
        public List<IRecoverableListener> listeners = new List<IRecoverableListener>();

        [SerializeField] bool debug = false;

        private void Awake()
        {
            foreach (var l in GetComponentsInParent<IRecoverableListener>())
            {
                listeners.Add(l);
            }
        }

        public async void RecoverForUserId(string userId, IRecoverRequester requester)
        {
            if (Object && Object.IsValid == true)
            {
                if (UserId == userId)
                {
                    if (debug) Debug.LogError($"[Debug] recovering object {name}");
                    await Object.WaitForStateAuthority();
                    requester.OnRecovered(this);
                    foreach (var l in listeners)
                    {
                        l.OnRecovered(this, requester);
                    }
                }
            }
            else
            {
                // Not yet spawned
                if (recoverRequestsForUserId.ContainsKey(userId) == false)
                    recoverRequestsForUserId.Add(userId, requester);
            }
        }

        public override void Spawned()
        {
            base.Spawned();
            foreach (var requestEntry in recoverRequestsForUserId)
            {
                RecoverForUserId(requestEntry.Key, requestEntry.Value);
            }
            recoverRequestsForUserId.Clear();
        }

        public static void RequestAuthorityOnRecoverablesWithUserId(string userId, IRecoverRequester requester)
        {
            foreach (var recoverable in FindObjectsByType<Recoverable>(FindObjectsSortMode.None))
            {
                recoverable.RecoverForUserId(userId, requester);
            }
        }

        public async void DespawnForUserId(string userId)
        {
            if (Object && Object.IsValid == true)
            {
                if (UserId == userId)
                {
                    await Object.WaitForStateAuthority();
                    if(Runner != null)
                    {
                        Runner.Despawn(Object);
                    }
                }
            }
        }
    }
}
