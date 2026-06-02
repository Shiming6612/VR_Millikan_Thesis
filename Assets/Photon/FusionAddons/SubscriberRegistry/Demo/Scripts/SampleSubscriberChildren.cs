using System;

public class SampleSubscriberChildren : SampleSubscriber
{
    public override Type RegistryType()
    {
        // Force a specific registry type (instead of allowing all subclasses of Subscriber<SampleSubscriber>)
        return typeof(SampleRegistryChildren);
    }
}
