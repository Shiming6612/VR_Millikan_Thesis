using TMPro;
using UnityEngine;

public class StoryPlaceholderController : MonoBehaviour
{
    [Header("Story UI")]
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text taskTitleText;
    public TMP_Text taskBodyText;
    public TMP_Text stepText;
    public TMP_Text continueHintText;

    [Header("Arrow UI")]
    public RectTransform arrowRect;

    [Header("Panel Root")]
    public GameObject panelRoot;

    private int currentStep = 0;
    private const int totalSteps = 3;

    private void Start()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        ShowStep(currentStep);
    }

    private void Update()
    {
        // Quest right controller A button
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            NextStep();
        }
    }

    public void NextStep()
    {
        currentStep++;

        if (currentStep >= totalSteps)
        {
            EndTutorial();
            return;
        }

        ShowStep(currentStep);
    }

    private void ShowStep(int step)
    {
        stepText.text = $"Schritt {step + 1} / {totalSteps}";
        continueHintText.text = "Drücke A, um fortzufahren";

        switch (step)
        {
            case 0:
                titleText.text = "Akt II – Aufbau des Experiments";
                bodyText.text = "Dies ist ein Platzhalter für die Erzählsequenz. Später kann hier ein Video, Audio oder eine Erklärung eingefügt werden.";
                taskTitleText.text = "Aktuelle Aufgabe";
                taskBodyText.text = "Sieh dir die Teile des Aufbaus nacheinander an.";
                arrowRect.anchoredPosition = new Vector2(-220f, -150f);
                break;

            case 1:
                titleText.text = "Akt III – Schwerkraft";
                bodyText.text = "Dies ist ein Platzhalter für die Einführung zur Schwerkraft und zum fallenden Tröpfchen.";
                taskTitleText.text = "Aktuelle Aufgabe";
                taskBodyText.text = "Beobachte das Tröpfchen ohne elektrisches Feld.";
                arrowRect.anchoredPosition = new Vector2(40f, -40f);
                break;

            case 2:
                titleText.text = "Akt IV – Schweben";
                bodyText.text = "Dies ist ein Platzhalter für die Einführung zur Schwebemethode und zur elektrischen Kraft.";
                taskTitleText.text = "Aktuelle Aufgabe";
                taskBodyText.text = "Versuche, das Tröpfchen zum Schweben zu bringen.";
                arrowRect.anchoredPosition = new Vector2(180f, 40f);
                break;
        }
    }

    private void EndTutorial()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}