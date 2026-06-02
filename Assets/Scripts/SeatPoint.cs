using UnityEngine;

public class SeatPoint : MonoBehaviour
{
    [Header("Seat Settings")]
    public int seatId = 1;
    public bool isTutorialSeat = false;

    [Header("Runtime State")]
    public bool isOccupied = false;
    public Transform occupantTransform;

    [Header("Optional Visuals")]
    public GameObject freeVisual;
    public GameObject occupiedVisual;
    public GameObject hoveredVisual;

    public bool IsAvailable => !isOccupied;

    private void Start()
    {
        RefreshVisual(false);
    }

    public bool CanBeOccupied()
    {
        return !isOccupied;
    }

    public void Occupy(Transform occupant)
    {
        isOccupied = true;
        occupantTransform = occupant;
        RefreshVisual(false);
    }

    public void Release()
    {
        isOccupied = false;
        occupantTransform = null;
        RefreshVisual(false);
    }

    public void SetHovered(bool hovered)
    {
        RefreshVisual(hovered);
    }

    private void RefreshVisual(bool hovered)
    {
        if (freeVisual != null)
            freeVisual.SetActive(!isOccupied && !hovered);

        if (occupiedVisual != null)
            occupiedVisual.SetActive(isOccupied);

        if (hoveredVisual != null)
            hoveredVisual.SetActive(!isOccupied && hovered);
    }
}