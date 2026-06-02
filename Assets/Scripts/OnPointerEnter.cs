using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonDebug : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Image img;
    private Color originalColor;

    private void Awake()
    {
        img = GetComponent<Image>();
        if (img != null)
            originalColor = img.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Weiter: POINTER ENTER");
        if (img != null) img.color = Color.yellow;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("Weiter: POINTER EXIT");
        if (img != null) img.color = originalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Weiter: POINTER CLICK");
        if (img != null) img.color = Color.green;
    }
}