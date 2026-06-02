using Fusion;
using Fusion.Addons.SubscriberRegistry;
using UnityEngine;

public class SampleSubscriber : Subscriber<SampleSubscriber>
{
    [Networked]
    [SerializeField]
    public bool AvailabilityAllowed { get; set; } = true;

    [SerializeField]
    float delayBeforeAvailable = 0;

    float availabilityTime = -1;

    public override bool UnregisterWhenNotAvailable => true;

    [SerializeField]
    bool unregisterOnUnavailableRegistry = false;
    protected override bool UnregisterOnUnavailableRegistry => unregisterOnUnavailableRegistry;

    // Custom availiblity criteria. The data to compute it needs to be synched so that the availability triggers everywhere (and hence the registration)
    public override bool IsAvailable => base.IsAvailable && AvailabilityAllowed && DelayExpired;

    [Networked]
    bool DelayExpired { get; set; } = false;

    public override void Spawned()
    {
        // Availaiblity is also analysed during spawn, so the availaiblity criteria should be ready before calling base.Spawned()
        if(delayBeforeAvailable > 0)
        {
            availabilityTime = Time.time + delayBeforeAvailable;
        } 
        else if(Object.HasStateAuthority)
        {
            DelayExpired = true;
        }
        base.Spawned();
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (DelayExpired == false)
        {
            if (availabilityTime != -1)
            {
                DelayExpired = Time.time > availabilityTime;
            }
        }
    }
}
