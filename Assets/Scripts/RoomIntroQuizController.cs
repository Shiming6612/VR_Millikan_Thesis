using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomIntroQuizController : MonoBehaviour
{
    [System.Serializable]
    public class QuestionPage
    {
        [Header("Page Root")]
        public GameObject pageRoot;

        [Header("UI")]
        public TMP_Text dialogueText;

        public Button optionAButton;
        public Button optionBButton;
        public Button optionCButton;
        public Button optionDButton;

        [Header("Question Content")]
        [TextArea(2, 4)] public string questionText;
        public string optionAText;
        public string optionBText;
        public string optionCText;
        public string optionDText;

        [Tooltip("0=A, 1=B, 2=C, 3=D")]
        public int correctOptionIndex = 0;
    }

    [Header("Pages")]
    public GameObject pageWelcome;
    public TMP_Text welcomeDialogueText;

    public GameObject pageOverview;
    public TMP_Text overviewDialogueText;

    public List<QuestionPage> questionPages = new List<QuestionPage>();

    [Header("Texts")]
    [TextArea(2, 4)] public string welcomeText = "Welcome placeholder";
    [TextArea(2, 4)] public string overviewText = "Overview placeholder";

    [Header("Flow")]
    public float correctAnswerDelay = 0.8f;
    public GameObject storyPanel;

    [Header("Movement Lock")]
    public Behaviour[] movementComponentsToDisableDuringIntro;
    public Behaviour[] movementComponentsToEnableAfterIntro;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;

    [Header("Visual Feedback")]
    public Color normalTextColor = Color.white;
    public Color correctTextColor = Color.green;
    public Color wrongTextColor = Color.red;

    private int currentPageIndex = 0;
    private bool inputLocked = false;

    // Page order:
    // 0 = Welcome
    // 1 = Overview
    // 2... = Question pages

    private void Start()
    {
        SetMovementEnabled(false);

        if (storyPanel != null)
            storyPanel.SetActive(false);

        SetupAllPages();
        ShowPage(0);
    }

    private void Update()
    {
        if (inputLocked)
            return;

        // A button moves from Welcome -> Overview -> first question
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            if (currentPageIndex == 0)
            {
                ShowPage(1);
            }
            else if (currentPageIndex == 1)
            {
                ShowPage(2);
            }
        }

        // Optional back logic for Welcome / Overview / question pages
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            if (currentPageIndex > 0)
            {
                ShowPage(currentPageIndex - 1);
            }
        }
    }

    private void SetupAllPages()
    {
        if (welcomeDialogueText != null)
            welcomeDialogueText.text = welcomeText;

        if (overviewDialogueText != null)
            overviewDialogueText.text = overviewText;

        for (int i = 0; i < questionPages.Count; i++)
        {
            SetupQuestionPage(questionPages[i], i);
        }
    }

    private void SetupQuestionPage(QuestionPage page, int questionIndex)
    {
        if (page == null || page.pageRoot == null)
            return;

        if (page.dialogueText != null)
            page.dialogueText.text = page.questionText;

        SetupOptionButton(page.optionAButton, page.optionAText, 0, questionIndex);
        SetupOptionButton(page.optionBButton, page.optionBText, 1, questionIndex);
        SetupOptionButton(page.optionCButton, page.optionCText, 2, questionIndex);
        SetupOptionButton(page.optionDButton, page.optionDText, 3, questionIndex);
    }

    private void SetupOptionButton(Button button, string optionText, int optionIndex, int questionIndex)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = optionText;
            label.color = normalTextColor;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnOptionClicked(questionIndex, optionIndex));
    }

    private void OnOptionClicked(int questionIndex, int selectedOptionIndex)
    {
        if (inputLocked)
            return;

        if (questionIndex < 0 || questionIndex >= questionPages.Count)
            return;

        QuestionPage page = questionPages[questionIndex];
        if (page == null)
            return;

        ResetOptionColors(page);

        bool isCorrect = selectedOptionIndex == page.correctOptionIndex;

        if (isCorrect)
        {
            SetOptionTextColor(page, selectedOptionIndex, correctTextColor);
            PlayClip(correctClip);
            StartCoroutine(GoToNextPageAfterDelay());
        }
        else
        {
            SetOptionTextColor(page, selectedOptionIndex, wrongTextColor);
            PlayClip(wrongClip);
        }
    }

    private IEnumerator GoToNextPageAfterDelay()
    {
        inputLocked = true;
        yield return new WaitForSeconds(correctAnswerDelay);

        int nextPageIndex = currentPageIndex + 1;

        // If there are still more pages, go on
        if (nextPageIndex < GetTotalPageCount())
        {
            ShowPage(nextPageIndex);
            inputLocked = false;
        }
        else
        {
            FinishIntro();
        }
    }

    private void ShowPage(int pageIndex)
    {
        currentPageIndex = pageIndex;

        if (pageWelcome != null) pageWelcome.SetActive(pageIndex == 0);
        if (pageOverview != null) pageOverview.SetActive(pageIndex == 1);

        for (int i = 0; i < questionPages.Count; i++)
        {
            if (questionPages[i] != null && questionPages[i].pageRoot != null)
            {
                questionPages[i].pageRoot.SetActive(pageIndex == i + 2);

                // Reset colors whenever a page becomes active again
                if (pageIndex == i + 2)
                    ResetOptionColors(questionPages[i]);
            }
        }
    }

    private int GetTotalPageCount()
    {
        return 2 + questionPages.Count;
    }

    private void FinishIntro()
    {
        SetMovementEnabled(true);

        if (storyPanel != null)
            storyPanel.SetActive(true);

        gameObject.SetActive(false);
    }

    private void SetMovementEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (movementComponentsToDisableDuringIntro != null)
            {
                for (int i = 0; i < movementComponentsToDisableDuringIntro.Length; i++)
                {
                    if (movementComponentsToDisableDuringIntro[i] != null)
                        movementComponentsToDisableDuringIntro[i].enabled = false;
                }
            }
        }
        else
        {
            if (movementComponentsToEnableAfterIntro != null)
            {
                for (int i = 0; i < movementComponentsToEnableAfterIntro.Length; i++)
                {
                    if (movementComponentsToEnableAfterIntro[i] != null)
                        movementComponentsToEnableAfterIntro[i].enabled = true;
                }
            }
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void ResetOptionColors(QuestionPage page)
    {
        SetButtonTextColor(page.optionAButton, normalTextColor);
        SetButtonTextColor(page.optionBButton, normalTextColor);
        SetButtonTextColor(page.optionCButton, normalTextColor);
        SetButtonTextColor(page.optionDButton, normalTextColor);
    }

    private void SetOptionTextColor(QuestionPage page, int optionIndex, Color color)
    {
        switch (optionIndex)
        {
            case 0:
                SetButtonTextColor(page.optionAButton, color);
                break;
            case 1:
                SetButtonTextColor(page.optionBButton, color);
                break;
            case 2:
                SetButtonTextColor(page.optionCButton, color);
                break;
            case 3:
                SetButtonTextColor(page.optionDButton, color);
                break;
        }
    }

    private void SetButtonTextColor(Button button, Color color)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = color;
    }
}