using UnityEngine;

public class DisableSampleSubscriberAfterDelay : MonoBehaviour
{
    public SampleSubscriber targetSampleSubscriber;
    public float delay = 13f;
     

    void Start()
    {
        if (targetSampleSubscriber != null)
        {
            Invoke(nameof(DisableAvailability), delay);
        }
    }

    void DisableAvailability()
    {
        targetSampleSubscriber.AvailabilityAllowed = false;
    }
}
