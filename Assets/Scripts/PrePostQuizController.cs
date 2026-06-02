using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PrePostQuizController : MonoBehaviour
{
    [Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 5)] public string question;
        [TextArea(1, 3)] public string answerA;
        [TextArea(1, 3)] public string answerB;
        [TextArea(1, 3)] public string answerC;
        [Range(0, 2)] public int correctIndex;
    }

    public enum QuizMode
    {
        PreQuiz,
        PostQuiz,
        Welcome
    }

    [Header("Root")]
    public GameObject wallRoot;
    public GameObject quizGroup;
    public GameObject resultGroup;

    [Header("Texts")]
    public TMP_Text modeText;
    public TMP_Text questionText;
    public TMP_Text answerTextA;
    public TMP_Text answerTextB;
    public TMP_Text answerTextC;
    public TMP_Text scoreText;
    public TMP_Text resultText;
    public TMP_Text continueText;

    [Header("Buttons")]
    public Button answerButtonA;
    public Button answerButtonB;
    public Button answerButtonC;
    public Button continueButton;

    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public float postFeedbackSeconds = 0.8f;
    public bool keepInspectorHighlightColor = true;

    [Header("Flow")]
    public bool startPreQuizOnStart = true;

    private const string WelcomeText =
        "Willkommen im VR-Labor.\n\n" +
        "Gleich beginnt der Millikan-Versuch.\n\n" +
        "Teleportation:\n" +
        "Halte den rechten Joystick nach vorne gedrueckt.\n" +
        "Ziele auf die Stelle, auf die der gruene Pfeil zeigt.\n" +
        "Lasse den Joystick los, um dich dorthin zu teleportieren.";

    private readonly List<QuizQuestion> questions = new List<QuizQuestion>();
    private readonly List<int> preAnswers = new List<int>();
    private readonly List<int> postAnswers = new List<int>();

    private QuizMode currentMode;
    private int currentQuestionIndex;
    private int correctPostCount;

    private bool waitingForPostFeedback;
    private Coroutine feedbackRoutine;

    private Image imageA;
    private Image imageB;
    private Image imageC;

    public IReadOnlyList<int> PreAnswers => preAnswers;
    public IReadOnlyList<int> PostAnswers => postAnswers;
    public int CorrectPostCount => correctPostCount;
    public int QuestionCount => questions.Count;
    public QuizMode CurrentMode => currentMode;

    private void Awake()
    {
        if (wallRoot == null)
            wallRoot = gameObject;

        CacheButtonImages();
        SetupQuestions();
        SetupButtons();

        if (keepInspectorHighlightColor)
            KeepHighlightedColorFromInspector();
    }

    private void Start()
    {
        if (startPreQuizOnStart)
            StartPreQuiz();
        else
            HideWall();
    }

    private void CacheButtonImages()
    {
        if (answerButtonA != null)
            imageA = answerButtonA.GetComponent<Image>();

        if (answerButtonB != null)
            imageB = answerButtonB.GetComponent<Image>();

        if (answerButtonC != null)
            imageC = answerButtonC.GetComponent<Image>();
    }

    private void SetupButtons()
    {
        if (answerButtonA != null)
        {
            answerButtonA.onClick.RemoveAllListeners();
            answerButtonA.onClick.AddListener(() => SelectAnswer(0));
        }

        if (answerButtonB != null)
        {
            answerButtonB.onClick.RemoveAllListeners();
            answerButtonB.onClick.AddListener(() => SelectAnswer(1));
        }

        if (answerButtonC != null)
        {
            answerButtonC.onClick.RemoveAllListeners();
            answerButtonC.onClick.AddListener(() => SelectAnswer(2));
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ContinueButtonPressed);
        }
    }

    private void KeepHighlightedColorFromInspector()
    {
        KeepHighlight(answerButtonA);
        KeepHighlight(answerButtonB);
        KeepHighlight(answerButtonC);
        KeepHighlight(continueButton);
    }

    private void KeepHighlight(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    public void StartPreQuiz()
    {
        StopFeedbackRoutineIfNeeded();

        currentMode = QuizMode.PreQuiz;
        currentQuestionIndex = 0;
        correctPostCount = 0;
        waitingForPostFeedback = false;

        preAnswers.Clear();

        ShowWall();
        ShowQuizGroupOnly();
        ShowQuestion();
    }

    public void StartPostQuiz()
    {
        StopFeedbackRoutineIfNeeded();

        currentMode = QuizMode.PostQuiz;
        currentQuestionIndex = 0;
        correctPostCount = 0;
        waitingForPostFeedback = false;

        postAnswers.Clear();

        ShowWall();
        ShowQuizGroupOnly();
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (currentQuestionIndex >= questions.Count)
        {
            FinishQuiz();
            return;
        }

        ClearButtonSelection();
        ResetButtonColors();
        SetAnswerButtonsInteractable(true);
        ShowAnswerButtons(true);
        ShowContinueButton(false);

        QuizQuestion q = questions[currentQuestionIndex];

        if (modeText != null)
            modeText.text = currentMode == QuizMode.PreQuiz ? "Vorwissenstest" : "Nachtest";

        if (questionText != null)
            questionText.text = (currentQuestionIndex + 1) + ". " + q.question;

        if (answerTextA != null)
            answerTextA.text = "A. " + q.answerA;

        if (answerTextB != null)
            answerTextB.text = "B. " + q.answerB;

        if (answerTextC != null)
            answerTextC.text = "C. " + q.answerC;

        if (resultText != null)
            resultText.text = "";

        RefreshScoreText();
        Canvas.ForceUpdateCanvases();
    }

    private void SelectAnswer(int selectedIndex)
    {
        if (waitingForPostFeedback)
            return;

        if (currentQuestionIndex < 0 || currentQuestionIndex >= questions.Count)
            return;

        QuizQuestion q = questions[currentQuestionIndex];

        if (currentMode == QuizMode.PreQuiz)
        {
            preAnswers.Add(selectedIndex);
            currentQuestionIndex++;
            ShowQuestion();
            return;
        }

        if (currentMode != QuizMode.PostQuiz)
            return;

        postAnswers.Add(selectedIndex);

        if (selectedIndex == q.correctIndex)
            correctPostCount++;

        ShowPostFeedback(selectedIndex, q.correctIndex);
        RefreshScoreText();

        waitingForPostFeedback = true;
        SetAnswerButtonsInteractable(false);

        feedbackRoutine = StartCoroutine(NextPostQuestionAfterDelay());
    }

    private IEnumerator NextPostQuestionAfterDelay()
    {
        yield return new WaitForSeconds(postFeedbackSeconds);

        waitingForPostFeedback = false;
        feedbackRoutine = null;

        currentQuestionIndex++;
        ShowQuestion();
    }

    private void ShowPostFeedback(int selectedIndex, int correctIndex)
    {
        ResetButtonColors();
        SetButtonColor(correctIndex, correctColor);

        if (selectedIndex != correctIndex)
            SetButtonColor(selectedIndex, wrongColor);
    }

    private void FinishQuiz()
    {
        StopFeedbackRoutineIfNeeded();
        ClearButtonSelection();
        ResetButtonColors();
        SetAnswerButtonsInteractable(false);

        if (currentMode == QuizMode.PreQuiz)
        {
            ShowWelcomePage();
            return;
        }

        if (currentMode == QuizMode.PostQuiz)
            ShowPostResult();
    }

    private void ShowWelcomePage()
    {
        currentMode = QuizMode.Welcome;

        ShowWall();
        ShowQuizAndResultGroups();
        ShowAnswerButtons(false);
        ShowContinueButton(true);

        if (modeText != null)
            modeText.text = "Einfuehrung";

        if (questionText != null)
            questionText.text = WelcomeText;

        if (answerTextA != null)
            answerTextA.text = "";

        if (answerTextB != null)
            answerTextB.text = "";

        if (answerTextC != null)
            answerTextC.text = "";

        if (scoreText != null)
            scoreText.text = "";

        if (resultText != null)
            resultText.text = "";

        if (continueText != null)
            continueText.text = "Weiter";

        Canvas.ForceUpdateCanvases();
    }

    private void ShowPostResult()
    {
        ShowResultGroupOnly();

        if (modeText != null)
            modeText.text = "Nachtest";

        if (scoreText != null)
            scoreText.text = "Richtig: " + correctPostCount + "/" + questions.Count;

        if (resultText != null)
            resultText.text = "Ergebnis: " + correctPostCount + "/" + questions.Count + " richtig.";

        if (continueText != null)
            continueText.text = "Weiter";

        ShowContinueButton(true);
    }

    private void ContinueButtonPressed()
    {
        if (currentMode == QuizMode.Welcome)
        {
            HideWall();
            return;
        }

        if (currentMode == QuizMode.PostQuiz)
        {
            HideWall();
            return;
        }
    }

    private void RefreshScoreText()
    {
        if (scoreText == null)
            return;

        if (currentMode == QuizMode.PostQuiz)
            scoreText.text = "Richtig: " + correctPostCount + "/" + questions.Count;
        else
            scoreText.text = "";
    }

    private void ShowWall()
    {
        if (wallRoot != null)
            wallRoot.SetActive(true);
    }

    private void HideWall()
    {
        if (wallRoot != null)
            wallRoot.SetActive(false);
    }

    private void ShowQuizGroupOnly()
    {
        if (quizGroup != null)
            quizGroup.SetActive(true);

        if (resultGroup != null)
            resultGroup.SetActive(false);
    }

    private void ShowResultGroupOnly()
    {
        if (quizGroup != null)
            quizGroup.SetActive(false);

        if (resultGroup != null)
            resultGroup.SetActive(true);
    }

    private void ShowQuizAndResultGroups()
    {
        if (quizGroup != null)
            quizGroup.SetActive(true);

        if (resultGroup != null)
            resultGroup.SetActive(true);
    }

    private void ShowAnswerButtons(bool show)
    {
        if (answerButtonA != null)
            answerButtonA.gameObject.SetActive(show);

        if (answerButtonB != null)
            answerButtonB.gameObject.SetActive(show);

        if (answerButtonC != null)
            answerButtonC.gameObject.SetActive(show);
    }

    private void ShowContinueButton(bool show)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(show);
    }

    private void SetAnswerButtonsInteractable(bool interactable)
    {
        if (answerButtonA != null)
            answerButtonA.interactable = interactable;

        if (answerButtonB != null)
            answerButtonB.interactable = interactable;

        if (answerButtonC != null)
            answerButtonC.interactable = interactable;
    }

    private void ResetButtonColors()
    {
        if (imageA != null)
            imageA.color = normalColor;

        if (imageB != null)
            imageB.color = normalColor;

        if (imageC != null)
            imageC.color = normalColor;
    }

    private void SetButtonColor(int index, Color color)
    {
        if (index == 0 && imageA != null)
            imageA.color = color;
        else if (index == 1 && imageB != null)
            imageB.color = color;
        else if (index == 2 && imageC != null)
            imageC.color = color;
    }

    private void ClearButtonSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (answerButtonA != null)
            answerButtonA.OnDeselect(null);

        if (answerButtonB != null)
            answerButtonB.OnDeselect(null);

        if (answerButtonC != null)
            answerButtonC.OnDeselect(null);

        if (continueButton != null)
            continueButton.OnDeselect(null);
    }

    private void StopFeedbackRoutineIfNeeded()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        waitingForPostFeedback = false;
    }

    private void SetupQuestions()
    {
        questions.Clear();

        questions.Add(new QuizQuestion
        {
            question = "Wie war die zentrale Forschungsfrage, die Robert Millikan mit seinem Experiment beantworten wollte?",
            answerA = "Koennen Oeltroepfchen durch reine Lichtenergie in der Schwebe gehalten werden?",
            answerB = "Wie gross ist die Masse eines Elektrons im Vergleich zu einem Oeltroepfchen?",
            answerC = "Ist elektrische Ladung diskret in unteilbaren Einheiten aufgebaut oder kontinuierlich?",
            correctIndex = 2
        });

        questions.Add(new QuizQuestion
        {
            question = "Welche Beobachtung macht man beim Blick durch das Mikroskop auf die fallenden Troepfchen?",
            answerA = "Die Troepfchen scheinen nach oben zu steigen, da das Mikroskop das Bild spiegelt.",
            answerB = "Man kann die exakte Anzahl der Elektronen auf der Oberflaeche des Tropfens sehen.",
            answerC = "Die Troepfchen erscheinen als farbige Ringe aufgrund der Lichtbrechung.",
            correctIndex = 0
        });

        questions.Add(new QuizQuestion
        {
            question = "Warum schlug Millikans Doktorand Harvey Fletcher die Verwendung von Oel anstelle von Wasser vor?",
            answerA = "Oeltroepfchen bleiben stundenlang stabil und verdunsten nicht so schnell wie Wasser.",
            answerB = "Oel laesst sich durch Reibung wesentlich schneller aufladen als Wasser.",
            answerC = "Die Dichte von Oel entspricht exakt der Erdbeschleunigung g.",
            correctIndex = 0
        });

        questions.Add(new QuizQuestion
        {
            question = "Wie wird im Millikan-Versuch der Radius r eines Oeltroepfchens bestimmt?",
            answerA = "Aus der Fallgeschwindigkeit des Troepfchens bei ausgeschaltetem elektrischem Feld.",
            answerB = "Durch die Messung der Zeit, die das Troepfchen zum Schweben benoetigt.",
            answerC = "Durch das Ablesen an einer Skala direkt auf der Oberflaeche des Zerstaeubers.",
            correctIndex = 0
        });

        questions.Add(new QuizQuestion
        {
            question = "Welcher Zustand muss erreicht sein, damit ein Troepfchen in der Kammer schwebt?",
            answerA = "Der Zerstaeuber muss einen konstanten Luftstrom erzeugen, der das Troepfchen traegt.",
            answerB = "Die elektrische Kraft F_el muss die Gewichtskraft F_G exakt ausgleichen.",
            answerC = "Die Spannung muss auf den maximalen Wert der Spannungsquelle eingestellt sein.",
            correctIndex = 1
        });

        questions.Add(new QuizQuestion
        {
            question = "Was besagt das Prinzip der Ladungsquantisierung?",
            answerA = "Elektrische Ladung kann in beliebig kleine Bruchstuecke unterteilt werden.",
            answerB = "Jede gemessene Ladung ist immer ein ganzzahliges Vielfaches der Elementarladung e.",
            answerC = "Die Ladung eines Troepfchens nimmt stetig ab, je laenger es in der Kammer schwebt.",
            correctIndex = 1
        });

        questions.Add(new QuizQuestion
        {
            question = "Warum schloss Millikan bestimmte Messwerte aus seinen Veroeffentlichungen aus, wie in seinen Notizbuechern entdeckt wurde?",
            answerA = "Er wollte die Ergebnisse seines Doktoranden Harvey Fletcher absichtlich faelschen.",
            answerB = "Er erkannte technische Fehler wie Luftzuege oder Erschuetterungen waehrend dieser Messungen.",
            answerC = "Die ausgeschlossenen Werte waren mathematisch nicht berechenbar.",
            correctIndex = 1
        });

        questions.Add(new QuizQuestion
        {
            question = "Welcher physikalische Parameter war fuer die leichte Abweichung von Millikans Wert (1,592 * 10^-19 C) zum heutigen Standardwert verantwortlich?",
            answerA = "Schwankungen im Magnetfeld der Erde in Chicago.",
            answerB = "Ein ungenauer Literaturwert fuer die Luftviskositaet eta.",
            answerC = "Die fehlerhafte Zaehlung der Troepfchen im Histogramm.",
            correctIndex = 1
        });

        questions.Add(new QuizQuestion
        {
            question = "Welche Rolle spielte Harvey Fletcher im Zusammenhang mit dem Nobelpreis von 1923?",
            answerA = "Er war der schaerfste Kritiker der Schwebemethode und versuchte den Versuch zu verhindern.",
            answerB = "Er verzichtete vertraglich auf die Autorenschaft und wurde daher nicht mit dem Nobelpreis ausgezeichnet.",
            answerC = "Er erhielt den Nobelpreis gemeinsam mit Millikan fuer die Entdeckung des Elektrons.",
            correctIndex = 1
        });

        questions.Add(new QuizQuestion
        {
            question = "In welcher Einheit wird die Elementarladung e typischerweise angegeben?",
            answerA = "Volt pro Meter (V/m).",
            answerB = "Newton pro Kilogramm (N/kg).",
            answerC = "Coulomb (C).",
            correctIndex = 2
        });

        questions.Add(new QuizQuestion
        {
            question = "Welcher physikalische Zusammenhang wird durch das Stokes'sche Gesetz im Experiment genutzt?",
            answerA = "Die elektrische Kraft auf ein Teilchen nimmt quadratisch mit der Entfernung zum Kondensator ab.",
            answerB = "Die Masse eines Troepfchens verringert sich proportional zu seiner Fallzeit.",
            answerC = "Die Reibungskraft der Luft auf eine Kugel haengt direkt von deren Radius ab.",
            correctIndex = 2
        });
    }
}