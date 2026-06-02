# XR Shared Core

## Documentation

https://doc.photonengine.com/fusion/current/industries-samples/industries-addons/fusion-industries-addons-xrshared


## Version & Changelog

- Version 2.1.9: Fix bug in XRHandCollectableSkeletonDriverHelper when searching for the root transform
- Version 2.1.8:
	- add automatic locomotion setup options on RigLocomotion
	- fix typo in IFeedbackHandler
- Version 2.1.7:
	- add verification on HideForLocalUser
	- add material
- Version 2.1.6:
	- Compatibility with Fusion 2.1
	- Fix for AsyncTask on WebGL
	- Allow for other kind of rig part position modifiers (grabbed objects, ...)
	- Add AuthorityVisualization to debug state authority visualy with a material
	- Add physics grabbable and authroity transfer on collision for 2.1 forecast physics
	- Add new UI prefabs
- Version 2.1.5: Add canvasesToIgnore option to RigPartVisualizer
- Version 2.1.4: Add IColocalizationRoomProvider to add interoperability between addons in colocalization scenario
- Version 2.1.3: Various assembly tooling fixes, to handle edge cases (first install, ...)
- Version 2.1.2: Improve Fader shader presence in builds detection and warning message
- Version 2.1.1:
	- Add way to position automaticaly transforms to match wrist and index positions
	- Add method in LocalInputTracker to check if a button is pressed no matter on which controller
	- Fix to handle properly hardware rig detection when the build scene list is not properly configured
	- Allow RigPartVisualizer to adapt game objects active status alongside changing renderers visibility
	- Add RayPointer and NetworkedRayPointer to provide synchronized rays
- Version 2.1.0:
	- Add new locomotion system by grabbing the world
	- Add new DetermineNewRigPositionToMovePositionToTargetPosition() method to TransformManipulation class
	- Add some utility classes (NetworkVisibilty, RingHistory, DebugTools)
	- Grabbable : add pauseGrabbabilty option
	- Update & improve the automatic weaving of XR addons
	- Fix TouchableButton status not reinitialized OnDisable()
	- bug fixes to UI interaction (XSCInputModule)

- Version 2.0.1: Add shared design & UI prefabs
- Version 2.0.0: First release



