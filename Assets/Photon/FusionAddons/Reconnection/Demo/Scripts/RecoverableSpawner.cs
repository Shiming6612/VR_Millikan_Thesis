using Fusion;
using Fusion.Addons.Reconnection;
using Fusion.Addons.SubscriberRegistry;
using UnityEngine;

public class RecoverableSpawner : NetworkBehaviour, IReconnectionHandlerListener
{
    public Recoverable recoverablePrefab;
    [SerializeField]
    bool automaticallySpawnOnUserIdSetForNewUser = true;
    [SerializeField]
    bool respawnOnReregister = false;
    ReconnectionHandler reconnectionHandler;
    bool recoverableSpawnedOnRegister = false;   
    public override void Spawned()
    {
        base.Spawned();
        reconnectionHandler = GetComponent<ReconnectionHandler>();
        reconnectionHandler.RegisterListener(this, notifyOfExistingRegistration: true);
    }

    [EditorButton("Spawn recoverable")]
    public void SpawnRecoverable()
    {
        if (Object.HasStateAuthority && recoverablePrefab)
        {
            var recoverable = Runner.Spawn(recoverablePrefab);
            recoverable.UserId = reconnectionHandler.UserId;
            
        }
    }

    #region IRegistrationListener<ReconnectionHandler>
    public void OnSubscriberRegistration(Registry<ReconnectionHandler> registry, Subscriber<ReconnectionHandler> subscriber)
    {
        if (subscriber != reconnectionHandler) return;

        // When a ReconnectionHandler is registered, its userId should be already set (it is in its criteria for IsAvailable, required for registration).
        if (automaticallySpawnOnUserIdSetForNewUser && Object.HasStateAuthority)
        {
            // We only spawn the object if we are not reconnecting
            if (reconnectionHandler.Status == ReconnectionHandler.ReconnectionStatus.NewUser && (respawnOnReregister == true || recoverableSpawnedOnRegister == false))
            {
                SpawnRecoverable();
                recoverableSpawnedOnRegister = true;
            }
        }
    }

    public void OnSubscriberUnregistration(Registry<ReconnectionHandler> registry, Subscriber<ReconnectionHandler> subscriber)
    {
    }
    #endregion

}
