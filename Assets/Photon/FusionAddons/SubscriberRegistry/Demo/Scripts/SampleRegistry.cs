using UnityEngine;
using Fusion.Addons.SubscriberRegistry;

public class SampleRegistry : Registry<SampleSubscriber>
{
    protected override void OnSubscriberRegistration(SampleSubscriber subscriber)
    {
        base.OnSubscriberRegistration(subscriber);
        Debug.Log($"[{name}] Registration of {subscriber}");
    }

    protected override void OnSubscriberUnregistration(SampleSubscriber subscriber)
    {
        base.OnSubscriberUnregistration(subscriber);
        Debug.Log($"[{name}] Unregistration of {subscriber}");
    }
}
