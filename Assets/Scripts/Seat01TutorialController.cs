using System.Collections;
using UnityEngine;

public class Seat01TutorialController : MonoBehaviour
{
    [Header("References")]
    public Transform playerRoot;
    public Transform seat01Point;
    public BottomTutorialController tutorialController;
    public GameObject millikanObject;

    [Header("Seat Detection")]
    public float controlRadius = 0.9f;

    [Header("Millikan Visibility")]
    public bool hideMillikanAtStart = true;
    public bool keepMillikanVisibleAfterAppearing = true;

    private bool wasInsideSeat01 = false;
    private bool millikanHasAppeared = false;
    private bool tutorialStartPending = false;

    private void Start()
    {
        if (millikanObject != null && hideMillikanAtStart)
            millikanObject.SetActive(false);
    }

    private void Update()
    {
        if (playerRoot == null || seat01Point == null || tutorialController == null)
            return;

        float distance = Vector3.Distance(playerRoot.position, seat01Point.position);
        bool isInsideSeat01 = distance <= controlRadius;

        if (isInsideSeat01 && !wasInsideSeat01)
        {
            wasInsideSeat01 = true;
            ShowMillikan();
            StartTutorialAfterMillikanIsReady();
        }
        else if (!isInsideSeat01 && wasInsideSeat01)
        {
            wasInsideSeat01 = false;

            if (!keepMillikanVisibleAfterAppearing && millikanObject != null)
                millikanObject.SetActive(false);
        }
    }

    private void ShowMillikan()
    {
        if (millikanObject == null)
            return;

        if (millikanHasAppeared && keepMillikanVisibleAfterAppearing)
            return;

        millikanObject.SetActive(true);
        millikanHasAppeared = true;
    }

    private void StartTutorialAfterMillikanIsReady()
    {
        if (tutorialStartPending)
            return;

        StartCoroutine(StartTutorialNextFrame());
    }

    private IEnumerator StartTutorialNextFrame()
    {
        tutorialStartPending = true;

        yield return null;

        if (tutorialController != null)
            tutorialController.BeginTutorialSession();

        tutorialStartPending = false;
    }
}