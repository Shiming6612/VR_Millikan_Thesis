# SubscriberRegistry

A registry notifying on all clients when a subscriber registers. Both need to be "available" (overridable definition, including notably to be spawned) before registration.

## Documentation

https://doc.photonengine.com/fusion/current/industries-samples/industries-addons/fusion-industries-addons-subscriber-registry

## Dependencies

- XRShared > 2.0.5

## Version & Changelog

- version 2.1.0: Update to support new XRShared architecture
- Version 2.0.2: Update for Unity 6 compatibility (replace deprecated methods like FindObjectOfType, etc.)
- Version 2.0.1:
    - Add DutyRegistry subclass of Registry, requiring an active state authority to be considered available
    - Add Registry.WarnSubscribersOfUnavailability option (true by default)
    - Add Subscriber.UnregisterOnUnavailableRegistry option (false by default)
- Version 2.0.0: First release 
