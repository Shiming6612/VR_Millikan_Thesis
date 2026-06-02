# Anchors

## Documentation

https://doc.photonengine.com/fusion/current/industries-samples/industries-addons/fusion-industries-addons-anchors


## Version & Changelog

- Version 2.0.4: Update scene to use the new WebCamTextureManager Prefab (to avoid guid collision with Meta SDKs V81)

- Version 2.0.3:
    - Add visualization option on IRLRoomManager for room associated parts (display just for one member for the remote rooms, all members of remote rooms, all rooms, ...)
    - Bug fix when the last member of a room leaves it (anchor removal, room cleanup, ...)
    
- Version 2.0.2:
    - Prefabs refactoring
    - Compatibility with Fusion 2.1
    - [Beta preview] Possibility to move remote room in colocalization scenario, with NetworkIRLRoomMoveRequester components

- Version 2.0.1:
    - Ensure that anchors whose state authority has disconnected receive a new state authority (to continue moving on room merges)
    - Bug fix: colocalization could fail in certain scenario (when a member was following an anchor, that it had created, during a room merge triggered by another player) due to an unneeded state authority check 
- Version 2.0.0: First release 