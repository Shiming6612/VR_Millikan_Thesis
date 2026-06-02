using UnityEngine;

public class EnableAfterDelay : MonoBehaviour
{
    public GameObject targetObject;
    public float delay = 11f;

    void Start()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
            Invoke(nameof(EnableObject), delay);
        }
    }

    void EnableObject()
    {
        targetObject.SetActive(true);
    }
}
