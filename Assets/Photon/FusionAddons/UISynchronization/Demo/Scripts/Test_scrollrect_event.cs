using UnityEngine;
using UnityEngine.UI;

public class Test_scrollrect_event : MonoBehaviour
{

    [SerializeField] private ScrollRect networked_scrollrect;

    private void Awake()
    {
        if (networked_scrollrect == null)
        {
            networked_scrollrect = GetComponent<ScrollRect>();
        }

        if (networked_scrollrect == null)
        {
            Debug.LogError("ScrollRect not found");
        }
        networked_scrollrect.onValueChanged.AddListener(OnScrollRectValueChanged);
    }

    private void OnScrollRectValueChanged(Vector2 normalizedPosition)
    {
        Debug.Log("ScrollRect change : " + normalizedPosition);
    }


}
