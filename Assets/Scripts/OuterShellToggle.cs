using UnityEngine;

public class OuterShellToggle : MonoBehaviour
{
    public GameObject opaqueShell;   // Mesh
    public GameObject cutawayShell;  // Mesh_Cutaway

    // Ä¬ÈÏ£º²»ÆÊÃæ
    void Awake()
    {
        SetCutaway(false);
    }

    public void SetCutaway(bool cutaway)
    {
        opaqueShell.SetActive(!cutaway);
        cutawayShell.SetActive(cutaway);
    }
}
