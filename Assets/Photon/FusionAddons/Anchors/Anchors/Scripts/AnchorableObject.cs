using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon
{
    public class AnchorableObject : MonoBehaviour
    {
        public List<Transform> localAnchorTransforms = new List<Transform>();
        public List<Transform> worldAnchorTransforms = new List<Transform>();

        public bool useTripletAlgorithm = false;

        public AnchorsManipulation.AverageAnchorAlgorithmSettings averagesettings = AnchorsManipulation.AverageAnchorAlgorithmSettings.DefaultSettings;

        public bool triggerRelocation = false;
        [Header("Debug")]
        public Vector3 randomWorldPositionErrorMagnitude = Vector3.zero;
        public Vector3 randomWorldEulerRotationErrorMagnitude = Vector3.zero;

        private void Update()
        {
            if (triggerRelocation)
            {
                triggerRelocation = false;
                var worldAnchors = new List<Pose>();
                foreach(var t in worldAnchorTransforms)
                {
                    var worldAnchor = new Pose(t.position, t.rotation);
                    if (randomWorldPositionErrorMagnitude != Vector3.zero)
                    {
                        var xOffset = Random.Range(-randomWorldPositionErrorMagnitude.x, randomWorldPositionErrorMagnitude.x);
                        var yOffset = Random.Range(-randomWorldPositionErrorMagnitude.y, randomWorldPositionErrorMagnitude.y);
                        var zOffset = Random.Range(-randomWorldPositionErrorMagnitude.z, randomWorldPositionErrorMagnitude.z);
                        worldAnchor.position += new Vector3(xOffset, yOffset, zOffset);
                    }
                    if (randomWorldPositionErrorMagnitude != Vector3.zero)
                    {
                        var xOffset = Random.Range(-randomWorldEulerRotationErrorMagnitude.x, randomWorldEulerRotationErrorMagnitude.x);
                        var yOffset = Random.Range(-randomWorldEulerRotationErrorMagnitude.y, randomWorldEulerRotationErrorMagnitude.y);
                        var zOffset = Random.Range(-randomWorldEulerRotationErrorMagnitude.z, randomWorldEulerRotationErrorMagnitude.z);
                        worldAnchor.rotation *= Quaternion.Euler(xOffset, yOffset, zOffset);
                    }
                    worldAnchors.Add(worldAnchor);
                }
                (var pos, var rot) = AnchorsManipulation.ObjectPositionToMoveRelativeReferenceTransformsToTargetAbsolutePoses(transform, localAnchorTransforms, worldAnchors, useTripletAlgorithm: useTripletAlgorithm);
                transform.position = pos;
                transform.rotation = rot;
            }
        }
    }
}

