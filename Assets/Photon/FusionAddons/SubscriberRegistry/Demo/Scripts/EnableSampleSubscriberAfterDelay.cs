using UnityEngine;

public class EnableSampleSubscriberAfterDelay : MonoBehaviour
{
    public SampleSubscriber targetSampleSubscriber;
    public float delay = 15f;
     

    void Start()
    {
        if (targetSampleSubscriber != null)
        {
            Invoke(nameof(EnableAvailability), delay);
        }
    }

    void EnableAvailability()
    {
        targetSampleSubscriber.AvailabilityAllowed = true;
    }
}
