using UnityEngine;
using Fusion.Addons.SubscriberRegistry;

public class SampleDutyRegistry : DutyRegistry<SampleSubscriber>
{
    [SerializeField]
    bool warnSubscribersOfUnavailability = true;
    protected override bool WarnSubscribersOfUnavailability => warnSubscribersOfUnavailability;

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
