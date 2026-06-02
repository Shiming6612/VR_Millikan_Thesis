using UnityEngine;

public class TriggerDebugger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("[PumpZone] TRIGGER ENTER by: " + other.name, this);
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("[PumpZone] TRIGGER EXIT by: " + other.name, this);
    }
}
