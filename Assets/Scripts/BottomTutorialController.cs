using TMPro;
using UnityEngine;

public class BottomTutorialController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text dialogueText;
    public TMP_Text buttonHintText;
    public GameObject dialogueRoot;

    [Header("Guide UI")]
    public GameObject guideUIRoot;
    public TMP_Text guideUIText;

    [Header("Histogram UI")]
    public HistogramPanel histogramPanel;

    [Header("Panels")]
    public GameObject parameterPanelRoot;
    public GameObject forceArrowsRoot;

    [Header("Scene Objects")]
    public GameObject notebookObject;
    public GameObject startTeleportArrow;

    [Header("References")]
    public SpraySpawner spraySpawner;
    public RadiusSliderController radiusSliderController;
    public DropSelectionManager selectionManager;
    public StoryMeasurementRecorder measurementRecorder;
    public VoltageKnobInput voltageKnobInput;
    public PrePostQuizController quizController;

    [Header("Return Point")]
    public Transform playerRoot;
    public Transform storyReturnPoint;

    [Header("Arrows")]
    public GameObject arrowSetup;
    public GameObject arrowSprayer;
    public GameObject arrowSelectDrop;
    public GameObject arrowLight;
    public GameObject arrowCapacitor;
    public GameObject arrowVoltageKnob;

    [Header("Radius Task")]
    public float radiusTargetTolerance = 0.08f;

    [Header("Measurement Task")]
    public int requiredFloatingDroplets = 5;

    [Header("Voice")]
    public AudioSource voiceSource;
    public AudioClip[] stepVoiceClips;
    public bool stopVoiceWhenStepChanges = true;

    [Header("Sounds")]
    public AudioSource taskCompleteSfxSource;
    public AudioClip taskCompleteSfx;
    public AudioSource pageSfxSource;
    public AudioClip bookPageSfx;

    [Header("Disable These During Tutorial")]
    public Behaviour[] componentsToDisableDuringTutorial;

    public static bool TutorialInputLocked { get; private set; }

    private bool tutorialSessionActive;
    private int currentStep;

    private int measurementsCompleted;
    private bool firstMeasurementExplanationShown;

    private int radiusTaskIndex = -1;
    private bool radiusTaskRadiusCorrect;
    private bool radiusTaskSprayed;
    private bool radiusTaskDropEnteredField;
    private bool currentRadiusTaskDone;

    private readonly float[] radiusTargets = { 0.5f, 1.0f, 1.5f };

    private const int LastStepIndex = 66;

    private void Start()
    {
        if (dialogueRoot == null)
            dialogueRoot = gameObject;

        ResetTutorialProgress();

        tutorialSessionActive = false;
        TutorialInputLocked = false;

        SetTutorialInputComponentsEnabled(true);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    private void Update()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
            UpdateRadiusTask();

        if (OVRInput.GetDown(OVRInput.Button.One))
            TryAdvanceWithAButton();

        if (OVRInput.GetDown(OVRInput.Button.Two))
            TrySkipSectionWithBButton();
    }

    public void BeginTutorialSession()
    {
        ResetTutorialProgress();

        tutorialSessionActive = true;
        TutorialInputLocked = true;

        if (startTeleportArrow != null)
            startTeleportArrow.SetActive(false);

        SetTutorialInputComponentsEnabled(false);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        ShowStep();
    }

    private void EndTutorialSession()
    {
        tutorialSessionActive = false;
        TutorialInputLocked = false;

        SetTutorialInputComponentsEnabled(true);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        ReturnToStoryStart();
        StartPostQuiz();

        ResetTutorialProgress();
    }

    private void ResetTutorialProgress()
    {
        currentStep = 0;
        measurementsCompleted = 0;
        firstMeasurementExplanationShown = false;

        radiusTaskIndex = -1;
        radiusTaskRadiusCorrect = false;
        radiusTaskSprayed = false;
        radiusTaskDropEnteredField = false;
        currentRadiusTaskDone = false;

        if (measurementRecorder != null)
            measurementRecorder.ClearMeasurements();
    }

    private void TryAdvanceWithAButton()
    {
        if (IsBlockingStep(currentStep))
        {
            RefreshCurrentText();
            return;
        }

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            if (currentRadiusTaskDone)
                NextStep();
            else
                RefreshCurrentText();

            return;
        }

        if (currentStep == 40)
        {
            if (measurementsCompleted < requiredFloatingDroplets)
            {
                currentStep = 33;
                ShowStep();
            }
            else
            {
                NextStep();
            }

            return;
        }

        NextStep();
    }

    private void TrySkipSectionWithBButton()
    {
        if (!IsSkipStep(currentStep))
            return;

        if (currentStep == 4)
            currentStep = 17;
        else if (currentStep == 28)
            currentStep = 33;
        else if (currentStep == 47)
            currentStep = 58;
        else if (currentStep == 58)
            currentStep = 66;

        ShowStep();
    }

    private bool IsSkipStep(int step)
    {
        return step == 4 || step == 28 || step == 47 || step == 58;
    }

    private void NextStep()
    {
        if (currentStep < LastStepIndex)
        {
            currentStep++;
            ShowStep();
        }
        else
        {
            EndTutorialSession();
        }
    }

    private void ShowStep()
    {
        ApplyStepSideEffects(currentStep);
        RefreshCurrentText();
        UpdateArrowForStep(currentStep);
        PlayVoiceForCurrentStep();
    }

    private void RefreshCurrentText()
    {
        if (dialogueText != null)
            dialogueText.text = GetDialogueForCurrentStep();

        if (buttonHintText != null)
            buttonHintText.text = GetButtonHintForCurrentStep();
    }

    private bool IsBlockingStep(int step)
    {
        return step == 17 || step == 33 || step == 34 || step == 35;
    }

    private void ApplyStepSideEffects(int step)
    {
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideForceArrows();
        HideGuideUI();
        HideHistogramUI();
        HideAllArrows();

        if (step >= 23 && step <= 40)
            ShowParameterPanel();
        else
            HideParameterPanel();

        switch (step)
        {
            case 17:
                if (spraySpawner != null)
                    spraySpawner.ReturnToRandomModeAndClearDrops();

                ShowGuideUI(GetGuideSprayer());
                break;

            case 22:
                ShowGuideUI(GetGuideGravityFormula());
                break;

            case 23:
                ShowParameterPanel();

                if (radiusSliderController != null)
                    radiusSliderController.StartRadiusTask();

                if (spraySpawner != null)
                    spraySpawner.EnableTutorialRadiusMode();

                ShowGuideUI(GetGuideRadiusSlider());
                break;

            case 24:
                ShowGuideUI(GetGuideRadiusSlider());
                break;

            case 25:
                StartRadiusTask(0);
                ShowGuideUI(GetGuideRadiusTask("0.5", "sehr langsam fallend"));
                break;

            case 26:
                StartRadiusTask(1);
                ShowGuideUI(GetGuideRadiusTask("1.0", "mittlere Geschwindigkeit"));
                break;

            case 27:
                StartRadiusTask(2);
                ShowGuideUI(GetGuideRadiusTask("1.5", "schnell fallend"));
                break;

            case 28:
                if (radiusSliderController != null)
                    radiusSliderController.EndRadiusTask();

                if (spraySpawner != null)
                    spraySpawner.ReturnToRandomModeAndClearDrops();

                HideGuideUI();
                break;

            case 33:
                StartNewMeasurement();
                ShowGuideUI(GetGuideMeasurementSpray());
                break;

            case 34:
                SetSelectionInteraction(true);
                ShowGuideUI(GetGuideMeasurementSelect());
                break;

            case 35:
                SetVoltageInteraction(true);
                ShowForceArrows();
                ShowGuideUI(GetGuideVoltageTask());
                break;

            case 37:
            case 38:
                ShowForceArrows();
                ShowGuideUI(GetGuideElectricFormula());
                break;

            case 39:
                ShowGuideUI(GetGuideParameterExplanation());
                break;

            case 41:
                SetVoltageInteraction(false);
                SetSelectionInteraction(false);

                if (spraySpawner != null)
                    spraySpawner.ResetAllDrops();

                ShowHistogramGuide();
                break;

            case 42:
            case 43:
            case 44:
            case 45:
            case 46:
                ShowHistogramGuide();
                break;

            case 47:
                if (notebookObject != null)
                    notebookObject.SetActive(true);

                PlayBookSound();
                HideGuideUI();
                break;

            case 48:
            case 49:
            case 50:
            case 51:
            case 52:
            case 53:
            case 54:
            case 55:
            case 56:
            case 57:
                ShowGuideUI(GetGuideNotebook());
                break;

            case 58:
                if (notebookObject != null)
                    notebookObject.SetActive(false);

                HideAllTemporaryUI();
                break;
        }
    }

    private void StartNewMeasurement()
    {
        ShowParameterPanel();
        HideForceArrows();
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);

        if (spraySpawner != null)
            spraySpawner.ReturnToRandomModeAndClearDrops();

        if (selectionManager != null)
            selectionManager.ClearSelectionAndHover();

        ResetVoltageToZero();
    }

    private void StartRadiusTask(int index)
    {
        radiusTaskIndex = index;
        radiusTaskRadiusCorrect = false;
        radiusTaskSprayed = false;
        radiusTaskDropEnteredField = false;
        currentRadiusTaskDone = false;

        ShowParameterPanel();

        if (radiusSliderController != null)
            radiusSliderController.StartRadiusTask();

        if (spraySpawner != null)
            spraySpawner.EnableTutorialRadiusMode();
    }

    private void UpdateRadiusTask()
    {
        if (currentRadiusTaskDone || radiusSliderController == null)
            return;

        float target = radiusTargets[radiusTaskIndex];
        float current = radiusSliderController.GetCurrentRadiusMicrometer();

        radiusTaskRadiusCorrect = Mathf.Abs(current - target) <= radiusTargetTolerance;

        if (radiusTaskRadiusCorrect && radiusTaskSprayed && radiusTaskDropEnteredField)
        {
            currentRadiusTaskDone = true;
            PlayTaskCompleteSound();
            RefreshCurrentText();
        }
    }

    public void NotifyDropletTriggered()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 17)
        {
            PlayTaskCompleteSound();
            NextStep();
            return;
        }

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            radiusTaskSprayed = true;
            RefreshCurrentText();
            return;
        }

        if (currentStep == 33)
        {
            PlayTaskCompleteSound();
            NextStep();
        }
    }

    public void NotifyDropEnteredField()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            radiusTaskDropEnteredField = true;
            RefreshCurrentText();
        }
    }

    public void NotifyDropSelected()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 34)
        {
            PlayTaskCompleteSound();
            NextStep();
        }
    }

    public void NotifyVoltageSolved()
    {
        if (!tutorialSessionActive || currentStep != 35)
            return;

        measurementsCompleted++;

        if (measurementRecorder != null)
            measurementRecorder.RecordSelectedDrop(selectionManager);

        PlayTaskCompleteSound();
        SetVoltageInteraction(false);

        if (measurementsCompleted == 1 && !firstMeasurementExplanationShown)
        {
            firstMeasurementExplanationShown = true;
            currentStep = 36;
            ShowStep();
            return;
        }

        if (measurementsCompleted < requiredFloatingDroplets)
        {
            currentStep = 33;
            ShowStep();
            return;
        }

        currentStep = 41;
        ShowStep();
    }

    private void PlayVoiceForCurrentStep()
    {
        if (voiceSource == null)
            return;

        if (stopVoiceWhenStepChanges)
            voiceSource.Stop();

        if (stepVoiceClips == null)
            return;

        if (currentStep < 0 || currentStep >= stepVoiceClips.Length)
            return;

        AudioClip clip = stepVoiceClips[currentStep];

        if (clip == null)
            return;

        voiceSource.clip = clip;
        voiceSource.Play();
    }

    private void StopVoice()
    {
        if (voiceSource != null)
            voiceSource.Stop();
    }

    private string GetDialogueForCurrentStep()
    {
        switch (currentStep)
        {
            case 0:
                return "Ah. Ein Klassenzimmer. Gut. Das kenne ich.\nMein Name ist Robert Andrews Millikan.\nIch war Physikprofessor in Chicago und später am California Institute of Technology.";

            case 1:
                return "Ich habe eine Frage — und ich brauche jemanden, der mir hilft.\nIst elektrische Ladung unteilbar?";

            case 2:
                return "Gibt es ein kleinstes elektrisches Paket?\nOder fließt Elektrizität kontinuierlich, wie Wasser durch einen Schlauch?";

            case 3:
                return "Ich habe ein Experiment gebaut, das diese Frage beantworten kann.\nAber ich kann es nicht alleine durchführen.\nDazu brauche ich einen Assistenten — wie dich.";

            case 4:
                return "Ausgezeichnet. Dann legen wir los.\nKomm zum Experiment — ich zeige dir, womit wir es zu tun haben.";

            case 5:
                return "Ich bin 1868 in Morrison, Illinois geboren.\nPhysik hat mich schon immer fasziniert:\nWas ist Materie? Was ist Elektrizität?";

            case 6:
                return "1909 haben Harvey Fletcher und ich begonnen,\ndiesen Apparat zu entwickeln.";

            case 7:
                return "Fletcher hatte die entscheidende Idee:\nStatt Wasser verwenden wir Öl.\nÖltröpfchen bleiben viel länger stabil.";

            case 8:
                return "J. J. Thomson hatte gezeigt, dass es Elektronen gibt.\nAber wie groß ist ihre Ladung?\nGenau das wollte ich messen.";

            case 9:
                return "Heute erfährst du,\nwie dieses Experiment funktioniert\nund wie man damit die Elementarladung bestimmt.";

            case 10:
                return "Hier ist mein Apparat.\nFünf Dinge arbeiten zusammen.\nIch erkläre dir jede Komponente.";

            case 11:
                return "Der Zerstäuber ist der Anfang.\nEr erzeugt viele kleine Öltröpfchen.\nDurch Reibung werden einige davon elektrisch geladen.";

            case 12:
                return "Die Tröpfchen sind sehr klein.\nDas Mikroskop macht sie sichtbar.\nAber es spiegelt das Bild.";

            case 13:
                return "In dieser Simulation sehen wir direkt,\nob die Öltröpfchen sinken oder steigen.";

            case 14:
                return "Das Licht kommt von der Seite.\nDie Tröpfchen streuen das Licht\nund werden dadurch sichtbar.";

            case 15:
                return "Das Herzstück sind zwei Metallplatten.\nSie sind 6 Millimeter voneinander entfernt.\nZwischen ihnen entsteht ein elektrisches Feld.";

            case 16:
                return "Mit dem Spannungsregler veränderst du das Feld.\nWenn die Spannung steigt,\nwird die elektrische Kraft stärker.";

            case 17:
                return "Aufgabe: Zerstäuber benutzen\n\nZiele mit dem rechten Controller auf den Zerstäuber.\nDrücke den rechten Trigger, um Öltröpfchen zu erzeugen.";

            case 18:
                return "Die Tröpfchen fallen.\nLangsam — aber sie fallen.\nDie Schwerkraft zieht sie nach unten.";

            case 19:
                return "Aus der Fallgeschwindigkeit können wir den Radius r bestimmen.\nGrößere Tröpfchen fallen schneller.";

            case 20:
                return "Die Dichte des Öls kennen wir.\nDie Erdbeschleunigung kennen wir auch.\nWas fehlt, ist der Radius.";

            case 21:
                return "Den Radius messen wir aus der Fallbewegung.\nDas ist der erste wichtige Schritt.";

            case 22:
                return "Schau auf die Formel im GuideUI.\nDort siehst du die bekannten Werte.";

            case 23:
                return "Ein Slider erscheint.\nEr steuert die Tröpfchengröße r.\nLinks: 0.3 µm. Rechts: 2.0 µm.";

            case 24:
                return "Bewege den Slider.\nKleinere Tröpfchen fallen langsamer.\nGrößere Tröpfchen fallen schneller.";

            case 25:
                return GetRadiusTaskText("0.5", "sehr langsam fallend");

            case 26:
                return GetRadiusTaskText("1.0", "mittlere Geschwindigkeit");

            case 27:
                return GetRadiusTaskText("1.5", "schnell fallend");

            case 28:
                return "Gut. Du siehst jetzt:\nDer Radius bestimmt die Fallgeschwindigkeit.\nJetzt kommt der eigentliche Messschritt.";

            case 29:
                return "Nun betrachten wir das elektrische Feld.\nWenn die Spannung steigt,\nwirkt eine elektrische Kraft auf das geladene Tröpfchen.";

            case 30:
                return "Diese Kraft heißt Coulomb-Kraft.\nJe höher die Spannung,\ndesto stärker wird sie.";

            case 31:
                return "Der grüne Pfeil zeigt die elektrische Kraft.\nWenn die Spannung steigt,\nwächst dieser Pfeil.";

            case 32:
                return "Wenn die Spannung passend ist,\nkann das Tröpfchen schweben.\nDann gleichen sich die Kräfte aus.";

            case 33:
                return "Aufgabe: Neues Öltröpfchen erzeugen\n\nZiele mit dem rechten Controller auf den Zerstäuber.\nDrücke den rechten Trigger.\n\nMessung: " + measurementsCompleted + "/" + requiredFloatingDroplets;

            case 34:
                return "Aufgabe: Öltröpfchen auswählen\n\nZiele mit dem roten Strahl des rechten Controllers auf ein Tröpfchen.\nDrücke den rechten Trigger.\n\nMessung: " + measurementsCompleted + "/" + requiredFloatingDroplets;

            case 35:
                return "Aufgabe: Tröpfchen zum Schweben bringen\n\nGreife den Spannungsregler mit dem linken Controller.\nBewege die Hand langsam nach links oder rechts,\nbis das Tröpfchen weder deutlich steigt noch fällt.\n\nMessung: " + measurementsCompleted + "/" + requiredFloatingDroplets;

            case 36:
                return "Das war deine erste Ladungsmessung.\nAber eine Messung ist nur ein Datenpunkt.\nWir brauchen ein Muster.";

            case 37:
                return "Schau auf das GuideUI.\nDort siehst du das Kräftegleichgewicht.";

            case 38:
                return "Wenn das Tröpfchen schwebt,\nist die elektrische Kraft gleich der Gewichtskraft.\nDamit berechnen wir die Ladung q.";

            case 39:
                return "Auf dem Parameterpanel siehst du die Größen,\ndie wir für die Berechnung brauchen.";

            case 40:
                return "Wir brauchen mehrere Datenpunkte.\nBringe noch vier weitere Tröpfchen nacheinander zum Schweben.";

            case 41:
                return "Alle fünf Messungen sind abgeschlossen.\nSchau dir nun das Histogramm an.";

            case 42:
                return "Ich habe nicht nur ein Tröpfchen gemessen.\nIch habe viele gemessen — über Monate.";

            case 43:
                return "Die Ladungen waren nicht zufällig verteilt.\nSie lagen immer nahe bei denselben Werten.";

            case 44:
                return "Diese Werte sind Vielfache einer kleinsten Einheit.\nDas ist die Elementarladung e.";

            case 45:
                return "Deine Messwerte zeigen dasselbe Muster:\nDie Ladungen häufen sich bei ganzzahligen Vielfachen.";

            case 46:
                return "Das ist Ladungsquantisierung.\nElektrische Ladung kommt in Paketen.";

            case 47:
                return "Ich muss dir etwas zeigen.\n1978 entdeckte Gerald Holton meine Original-Notizbücher.";

            case 48:
                return "Er fand heraus,\ndass ich mehr Tröpfchen gemessen hatte,\nals ich veröffentlicht habe.";

            case 49:
                return "Neben manchen Datenpunkten standen Anmerkungen:\n'Won't work', 'Schiefe Messung', 'Error — discard'.";

            case 50:
                return "War das falsch?\nIch glaube: Nein.\nIch habe Messungen mit technischen Fehlern ausgeschlossen.";

            case 51:
                return "Das ist kein Betrug.\nDas ist Urteilsvermögen.";

            case 52:
                return "Allan Franklin zeigte 1981:\nDie weggelassenen Daten hätten den Endwert kaum verändert.";

            case 53:
                return "Die statistische Unsicherheit wäre größer geworden,\naber das Ergebnis wäre fast gleich geblieben.";

            case 54:
                return "Die Selektion verbesserte die Präzision,\naber nicht das Ergebnis.";

            case 55:
                return "Datenselektion beschäftigt Wissenschaftler bis heute.\nEine klare Anforderung bleibt: Transparenz.";

            case 56:
                return "Was ausgeschlossen wird — und warum —\nmuss dokumentiert sein.";

            case 57:
                return "Damit hast du gesehen:\nNicht nur der Messwert zählt,\nsondern auch der Umgang mit Daten.";

            case 58:
                return "1913 veröffentlichte ich meinen Endwert:\ne = 1.592 * 10^-19 Coulomb.\nUnsicherheit: 0.2 Prozent.";

            case 59:
                return "Das war damals die genaueste Messung der Elementarladung.\nDer heutige Wert ist 1.602 * 10^-19 Coulomb.";

            case 60:
                return "Die Abweichung lag an einem leicht falschen Wert\nfür die Luftviskosität.\nNicht an der Methode.";

            case 61:
                return "Der eigentliche Beitrag ist nicht nur die Zahl.\nEs ist das Prinzip:\nElektrische Ladung ist gequantelt.";

            case 62:
                return "Es gibt keine halbe Elementarladung.\nKeine viertel Elementarladung.\nDie Natur zählt in ganzen Zahlen.";

            case 63:
                return "Das ist eine fundamentale Struktur der Materie.";

            case 64:
                return "Seit 1995 wurden über hundert Millionen Öltropfen vermessen.\nEs gab keinen Hinweis auf Bruchladungen.";

            case 65:
                return "Auch deine Messungen zeigen dieses Muster:\nLadungen treten als Vielfache von e auf.";

            case 66:
                return "Danke für deine Hilfe.\nJetzt folgt derselbe Wissenstest noch einmal.\nDieses Mal bekommst du Feedback zu deinen Antworten.";

            default:
                return "";
        }
    }

    private string GetRadiusTaskText(string target, string label)
    {
        if (currentRadiusTaskDone)
        {
            return "Aufgabe abgeschlossen:\n\n" +
                   "<b><color=#00AA00>r = " + target + " µm → " + label + "</color></b>\n\n" +
                   "Drücke A, um fortzufahren.";
        }

        return "Aufgabe:\n\n" +
               "r = " + target + " µm → " + label + "\n\n" +
               "Stelle den Radius mit dem linken Controller ein.\n" +
               "Benutze den linken Trigger am Slider.\n" +
               "Erzeuge danach Öltröpfchen mit dem rechten Trigger am Zerstäuber.";
    }

    private string GetButtonHintForCurrentStep()
    {
        if (currentStep == 3)
            return "A: Ja, ich helfe dir!";

        if (IsSkipStep(currentStep))
            return "A: Weiter    B: Abschnitt überspringen";

        if (IsBlockingStep(currentStep))
            return "";

        if ((currentStep == 25 || currentStep == 26 || currentStep == 27) && !currentRadiusTaskDone)
            return "";

        return "A: Weiter";
    }

    private string GetGuideSprayer()
    {
        return "Zerstäuber\n\nController: rechter Controller\nTaste: rechter Trigger\nAktion: Öltröpfchen erzeugen";
    }

    private string GetGuideGravityFormula()
    {
        return "Schwerkraft und Radius\n\nF_G = m * g\nm = ρ_Öl * (4/3) * π * r^3\n\nρ_Öl = 875 kg/m^3\ng = 9.81 m/s^2";
    }

    private string GetGuideRadiusSlider()
    {
        return "Tröpfchengröße r\n\nSlider: 0.3 µm bis 2.0 µm\n\nController: linker Controller\nTaste: linker Trigger am Slider\nAktion: Radius-Slider bewegen";
    }

    private string GetGuideRadiusTask(string target, string label)
    {
        return "Radius-Aufgabe\n\nZiel: r = " + target + " µm\n" + label +
               "\n\n1. Linker Trigger: Slider halten\n2. Rechter Trigger: Öltröpfchen erzeugen\n3. Tropfen muss in den Kondensator gelangen.";
    }

    private string GetGuideMeasurementSpray()
    {
        return "Messung starten\n\nController: rechter Controller\nTaste: rechter Trigger\nAktion: Öltröpfchen erzeugen";
    }

    private string GetGuideMeasurementSelect()
    {
        return "Tröpfchen auswählen\n\nController: rechter Controller\nTaste: rechter Trigger\nAktion: Mit dem roten Strahl ein Öltröpfchen auswählen.";
    }

    private string GetGuideVoltageTask()
    {
        return "Schwebezustand\n\nController: linker Controller\nTaste: mittlere Griff-Taste\nAktion: Hand langsam nach links oder rechts bewegen\nFeineinstellung: zusätzlich X-Taste gedrückt halten\n\nZiel: Tröpfchen soll weder steigen noch fallen.";
    }

    private string GetGuideElectricFormula()
    {
        return "Kräftegleichgewicht\n\nF_el = F_G\nF_el = q * E\nE = U / d\nq = m * g * d / U";
    }

    private string GetGuideParameterExplanation()
    {
        return "Messgrößen\n\nq = Ladung [C]\nU = Spannung [V]\nd = 6.00 mm\nm = Masse [kg]\ng = 9.81 m/s^2\nE = U / d";
    }

    private string GetGuideNotebook()
    {
        return "Millikan Notebook 1911\n\n" +
               "#47 q = 1.613 * 10^-19 C OK\n" +
               "#48 q = 1.21 * 10^-19 C Won't work\n" +
               "#49 q = 3.204 * 10^-19 C x2 OK\n" +
               "#50 q = 0.94 * 10^-19 C Schiefe Messung\n" +
               "#51 q = 4.836 * 10^-19 C x3 OK\n" +
               "#52 Tropfen verloren Error discard\n" +
               "#53 q = 1.598 * 10^-19 C OK";
    }

    private string GetGuideHistogramText()
    {
        return "Ladungsverteilung\n\nDie Balken zeigen, bei welchen Vielfachen von e deine Messwerte liegen.";
    }

    private void ShowHistogramGuide()
    {
        ShowGuideUI(GetGuideHistogramText());

        if (histogramPanel != null)
            histogramPanel.Show();
    }

    private void HideAllTemporaryUI()
    {
        HideParameterPanel();
        HideGuideUI();
        HideHistogramUI();
        HideForceArrows();
        HideAllArrows();

        if (notebookObject != null)
            notebookObject.SetActive(false);
    }

    private void ShowParameterPanel()
    {
        if (parameterPanelRoot != null)
            parameterPanelRoot.SetActive(true);
    }

    private void HideParameterPanel()
    {
        if (parameterPanelRoot != null)
            parameterPanelRoot.SetActive(false);
    }

    private void ShowGuideUI(string text)
    {
        if (guideUIRoot != null)
            guideUIRoot.SetActive(true);

        if (guideUIText != null)
            guideUIText.text = text;
    }

    private void HideGuideUI()
    {
        if (guideUIRoot != null)
            guideUIRoot.SetActive(false);
    }

    private void HideHistogramUI()
    {
        if (histogramPanel != null)
            histogramPanel.Hide();
    }

    private void ShowForceArrows()
    {
        if (forceArrowsRoot != null)
            forceArrowsRoot.SetActive(true);
    }

    private void HideForceArrows()
    {
        if (forceArrowsRoot != null)
            forceArrowsRoot.SetActive(false);
    }

    private void HideAllArrows()
    {
        if (arrowSetup != null) arrowSetup.SetActive(false);
        if (arrowSprayer != null) arrowSprayer.SetActive(false);
        if (arrowSelectDrop != null) arrowSelectDrop.SetActive(false);
        if (arrowLight != null) arrowLight.SetActive(false);
        if (arrowCapacitor != null) arrowCapacitor.SetActive(false);
        if (arrowVoltageKnob != null) arrowVoltageKnob.SetActive(false);
    }

    private void UpdateArrowForStep(int step)
    {
        HideAllArrows();

        switch (step)
        {
            case 10:
                if (arrowSetup != null) arrowSetup.SetActive(true);
                break;

            case 11:
            case 17:
            case 33:
                if (arrowSprayer != null) arrowSprayer.SetActive(true);
                break;

            case 14:
                if (arrowLight != null) arrowLight.SetActive(true);
                break;

            case 15:
            case 29:
            case 30:
            case 31:
            case 32:
            case 37:
            case 38:
                if (arrowCapacitor != null) arrowCapacitor.SetActive(true);
                break;

            case 34:
                if (arrowSelectDrop != null) arrowSelectDrop.SetActive(true);
                break;

            case 35:
                if (arrowVoltageKnob != null) arrowVoltageKnob.SetActive(true);
                break;
        }
    }

    private void SetVoltageInteraction(bool enabled)
    {
        if (voltageKnobInput != null)
            voltageKnobInput.SetInteractionEnabled(enabled);
    }

    private void SetSelectionInteraction(bool enabled)
    {
        if (selectionManager != null)
            selectionManager.SetSelectionEnabled(enabled);
    }

    private void ResetVoltageToZero()
    {
        if (voltageKnobInput != null)
            voltageKnobInput.ResetVoltageToZero();
    }

    private void ReturnToStoryStart()
    {
        if (playerRoot != null && storyReturnPoint != null)
        {
            playerRoot.position = storyReturnPoint.position;
            playerRoot.rotation = storyReturnPoint.rotation;
        }
    }

    private void StartPostQuiz()
    {
        if (quizController != null)
            quizController.StartPostQuiz();
    }

    private void PlayBookSound()
    {
        if (pageSfxSource != null && bookPageSfx != null)
            pageSfxSource.PlayOneShot(bookPageSfx);
    }

    private void PlayTaskCompleteSound()
    {
        if (taskCompleteSfxSource != null && taskCompleteSfx != null)
            taskCompleteSfxSource.PlayOneShot(taskCompleteSfx);
    }

    private void SetTutorialInputComponentsEnabled(bool enabled)
    {
        if (componentsToDisableDuringTutorial == null)
            return;

        for (int i = 0; i < componentsToDisableDuringTutorial.Length; i++)
        {
            if (componentsToDisableDuringTutorial[i] != null)
                componentsToDisableDuringTutorial[i].enabled = enabled;
        }
    }
}