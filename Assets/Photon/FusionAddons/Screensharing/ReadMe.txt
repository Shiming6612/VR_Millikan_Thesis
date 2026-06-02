# Screen sharing

## Documentation

https://doc.photonengine.com/fusion/current/industries-samples/industries-addons/fusion-industries-addons-screensharing


## Version & Changelog

- version 2.4.3: Move Meta Camera sample asset to MetaCoreIntegration addons
- version 2.4.2: Change asmdef to allow usage of Screensharing.MetaWebcam when the Photon video SDK is not installed 
- version 2.4.1: Change asmdef to avoid error on prefabs when the video SDK is not present

- version 2.4.0: 
	- Update the IEmitterController interface (add OnStopEmitting() & OnStartEmitting())
	- ScreensharingReceiver : update to support the registration process on a list of voice client
	- ScreeSharingTextureProjection : update to take into account the user's scale (for giant mode) for the screenRenderTextureCamera position
	
- version 2.3.0: Update to support new XRShared architecture

- Version 2.2.1: 
    - Ensure compatibility with video SDK 2.59
    - Allow automatic reconnection of the screensharing emitter on desktop id change. 
    - Fix screen sharing demo scene preview
    - Add the SubsampledLayoutDesactivation editor script, to automatically disable the MetaXRSubsampledLayout option, that is only compatible with Vulkan, which cannot be used with the Photon Video SDK
    - Removed sample Texture encoder (not relevant for android target, nor compatible with visio SDK 2.59)
	
- Version 2.2.0: 
    - use Video SDK preview in WebcamEmitter (in conjonction with a target ScreenSharingScreen) 
    - Rename ScreenSharingLODHandler to ScreeSharingTextureProjection
    - Add a automaticallyAddTextureProjection option on ScreenSharingScreen to automatically add a ScreeSharingTextureProjection on Android
    - Improve ScreeSharingTextureProjection automatic setup (camera, projection renderer, ...)
    - Move most of special shader handling logic to ScreenSharingScreen
	
- Version 2.1.0: 
	- Update QuestVideoTextureExt3D shader to support scene with passthrough 
	- ScreensharingReceiver refactoring 
	- Update ScreenSharingScreen : add IScreenSharingScreenListener interface + change EnablePlayback signature (playerId & userData parameters added)
	- Update ScreenSharingScreenLODHandler to help scene integration
	- Support Webcam video streaming

- Version 2.0.4: Update for PhotonVoice Video SDK 2.57
- Version 2.0.3: Fix localVoiceVideo not set to null in DisconnectScreenSharing()
- Version 2.0.2: Change codec settings in the demo scene (VP8 instead of VP9)
- Version 2.0.1: Add verification before register voice client
- Version 2.0.0: Fusion 2.0 support
- Version 1.0.3: Update for PhotonVoice Video SDK 2.53
- Version 1.0.2: Move QuestVideoTextureExt3D shader in Resources directory
- Version 1.0.1: ScreenShare renamed to screensharing & cleanup + add namespace
- Version 1.0.0: First release 

## License

Some code and materials included in this addon have been developed by Meta and are subject to the following copyright notice :
"Copyright © Meta Platform Technologies, LLC and its affiliates. All rights reserved;"