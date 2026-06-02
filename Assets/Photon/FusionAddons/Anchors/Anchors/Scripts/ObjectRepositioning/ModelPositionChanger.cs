using Fusion;
using UnityEngine;

/// <summary>
/// ModelPositionChanger is a simple class to change the position & rotation during FUN.
/// </summary>
public class ModelPositionChanger : NetworkBehaviour
{
    [SerializeField] Vector3 targetPosition;
    [SerializeField] Quaternion targetRotation;

    bool modelPositionChangeRequested = false;

    public void ChangeModelPosition(Vector3 modelPosition, Quaternion modelRotation)
    {
        targetPosition = modelPosition;
        targetRotation = modelRotation;
        modelPositionChangeRequested = true;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (modelPositionChangeRequested)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            modelPositionChangeRequested = false;
        }
    }
}
