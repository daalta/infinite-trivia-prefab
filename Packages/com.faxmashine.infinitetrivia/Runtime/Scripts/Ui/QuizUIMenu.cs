
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QuizUIMenu : UdonSharpBehaviour
{
    [Header("Settings")]
    [SerializeField] private float questionRevealSpeed = 20;

    [SerializeField] private string[] stringsAnswerCorrect =
    {
        "Great work!",
        "Well done!",
        "Excellent!",
        "Correctamundo!",
        "That's correct!",
        "You are correct!",
        "Correct answer!",
        "Smarty pants!",
        "Nicely done!",
        "Genius!",
        "Fantastic!",
        "Right on the money!",
        "You're right",
        "Spot on!",
        "Superb!",
        "Outstanding!",
        "Right you are!",
        "Marvelous!",
        "Awesome!",
        "Impressive!",
        "Right-o!",
        "Good going!",
        "Top-notch!",
        "Nice job!",
        "So smart!",
        "Big brain move",
        "Brilliant!",
        "You knew that?",
        "You got it!",
        "Nice!",
        "Good guess!",
        "Ding ding ding!",
        "Bullseye!",
    };

    [SerializeField] private string[] stringsAnswerIncorrect =
    {
        "Oops.",
        "Nice try.",
        "Sorry.",
        "Not quite.",
        "That's not right.",
        "Don't give up!",
        "Almost.",
        "Nope.",
        "Good guess.",
        "Maybe not.",
        "That's not it.",
        "Hmm.",
        "Better luck next time.",
        "That's not it.",
        "Not correct.",
        "Incorrect.",
        "That's incorrect.",
        "Wrong answer.",
        "Try again?",
        "Keep trying.",
        "Yikes.",
        "Oof.",
        "Nah.",
        "Oh no.",
        "Hahaha no.",
        "You tried.",
        "Tricky.",
        "Wrong.",
    };
    
    [SerializeField] private string[] stringsAnswerNone =
    {
        "Are you still there?",
        "No answer.",
        "You didn't answer.",
        "Click buttons to answer.",
        "Hello?",
        "Answer to join.",
        "Too difficult?",
        "Time's up!",
        "No response.",
        "Answer, please.",
        "Just guess!",
        "Guess next time.",
        "Try to guess.",
        "Don't be shy!",
        "Give it a try.",
        "Answer the question.",
        "Let's keep going.",
        "Forgot to answer?",
        "You gotta click.",
        "Might as well guess.",
        "Oops.",
        "Don't wanna play?",
    };
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clipSubmit;
    [SerializeField] private AudioClip clipRight;
    [SerializeField] private AudioClip clipWrong;
    [SerializeField] private AudioClip clipShowOption;
    [SerializeField] private AudioClip clipCharacterBlip;
    [SerializeField] private AudioClip clipError;
    [SerializeField] private AudioClip clipTimerStart;
    [SerializeField] private AudioClip clipShowQuestion;
    [SerializeField] private AudioClip clipOpenSettings;
    
    [Header("UI")]
    [SerializeField] private QuizUIButton[] quizUIButtons;
    [SerializeField] private TextMeshProUGUI questionTMP;
    [SerializeField] private TextMeshProUGUI categoryTMP;
    [SerializeField] private TextMeshProUGUI[] questionIndexTMPs;
    [SerializeField] private TextMeshProUGUI numVotesTMP;
    [SerializeField] private Animator mainAnimator;
    [SerializeField] private Animator timer;
    [SerializeField] private Image categoryImage;
    [SerializeField] private Sprite fallbackCategoryIcon;

    [Header("Logging")]
    [Tooltip("Whether to enable non-critical logging information.")]
    [SerializeField] private bool logInfo = false;
    
    //private readonly string DEBUG_LOG_HEADER = "<b>[<color=blue>Quiz UI Menu</color>]</b>";

    private string textQuestion;
    private float numRevealedCharacters = 0;
    private bool isRevealQueued;
    private int correctStreak;
    private int numAnswers; // Normally 4, sometimes 2 for true/false questions.
    private QuizCategory cachedQuizCategory;
    private QuizManager manager;

    public QuizManager Manager
    {
        private get => manager;
        set
        {
            manager = value;
            if (quizUIButtons.Length < 4)
            {
                LogWarning($"There are not enough buttons assigned for all question types! You may not be able to answer the questions.");
                return;
            }

            for (var i = 0; i < quizUIButtons.Length; i++)
            {
                quizUIButtons[i].ButtonIndex = i;
                quizUIButtons[i].Manager = Manager;
            }
        }
    }

    public void _UpdateRevealQuestion()
    {
        var oldNum = numRevealedCharacters;
        numRevealedCharacters += Time.deltaTime * questionRevealSpeed;
        
        if (ShouldPlayCharacterSound(oldNum)) PlayClip(clipCharacterBlip);

        if (numRevealedCharacters >= textQuestion.Length)
        {
            FinishRevealQuestion();
            return;
        }

        var numRevealedCharactersRounded = Mathf.FloorToInt(numRevealedCharacters);
        var numRevealedCharactersRemainder = numRevealedCharacters - Mathf.FloorToInt(numRevealedCharacters);
        
        var revealedString = numRevealedCharactersRounded <= 0 ? "" : textQuestion.Substring(0, numRevealedCharactersRounded);
        var unrevealedString = textQuestion.Substring(numRevealedCharactersRounded + 1, textQuestion.Length - numRevealedCharactersRounded - 1);
        
        var result = revealedString;
        var alpha = numRevealedCharactersRemainder;
        result += GetStringWithRichAlpha(textQuestion[numRevealedCharactersRounded], alpha);
        
        if (unrevealedString.Length > 0) result += "<alpha=#00>" + unrevealedString;

        questionTMP.text = result;
        isRevealQueued = true;
        SendCustomEventDelayedFrames(nameof(_UpdateRevealQuestion), 1);
    }

    private void FinishRevealQuestion()
    {
        questionTMP.text = textQuestion;
        isRevealQueued = false;
            
        for (var i = 0; i < quizUIButtons.Length; i++)
        {
            if (i >= numAnswers) return;
            string eventName;
            switch (i)
            {
                case 0:
                    eventName = nameof(_ShowButton0);
                    break;
                case 1:
                    eventName = nameof(_ShowButton1);
                    break;
                case 2:
                    eventName = nameof(_ShowButton2);
                    break;
                default:
                    eventName = nameof(_ShowButton3);
                    break;
                    
            }
            SendCustomEventDelayedSeconds(eventName, i + 0.5f);
        }

    }

    private string GetStringWithRichAlpha(char c, float alpha)
    {
        var alphaHex = Mathf.FloorToInt(alpha * 256).ToString("X2");
        return $"<alpha=#{alphaHex}>{c}";
    }

    private bool ShouldPlayCharacterSound(float oldNum)
    {
        return (int) numRevealedCharacters > (int) oldNum
               && numRevealedCharacters < textQuestion.Length
               && textQuestion[(int) numRevealedCharacters] != ' ';
    }

    private void Start()
    {
        if (questionTMP == null) LogWarning($"There are no question text elements specified. You may not be able to see the question!");
    }

    private void RevealQuestion(string question)
    {
        textQuestion = question;
        numRevealedCharacters = 0;
        if (!isRevealQueued) _UpdateRevealQuestion();
    }

    public void UIUpdate(string quizQuestion, string[] quizAnswers, QuizCategory category, int questionIndex)
    {
        if (Manager == null)
        {
            LogWarning($"No Quiz Manager provided. Please provide one!");
            return;
        }

        cachedQuizCategory = category;

        RevealQuestion(quizQuestion);
        if (categoryTMP != null) categoryTMP.text = cachedQuizCategory.CategoryPrettyName;
        if (questionIndexTMPs != null)
        {
            foreach (var text in questionIndexTMPs)
            {
                text.text = $"Question {questionIndex + 1}";
            }
        }

        if (categoryImage != null)
        {
            var icon = category._GetIcon();
            categoryImage.sprite = icon != null ? icon : fallbackCategoryIcon;
            mainAnimator.SetBool("IsCategoryVisible", true);
        }

        #region Quiz Answers

        if (quizUIButtons != null)
        {
            numAnswers = quizAnswers.Length;
        
            for (var i = 0; i < quizUIButtons.Length; i++)
            {
                var shouldBeVisible = i < quizAnswers.Length;
                quizUIButtons[i].gameObject.SetActive(shouldBeVisible);

                if (!shouldBeVisible) continue;
                quizUIButtons[i]._SetText(quizAnswers[i]);
            }
        }
        
        #endregion

        PlayClip(clipShowQuestion, true);
        timer.SetBool("HasSubmittedAnswer", false);
        SetTimeRemaining(0);
    }

    public void SetTimeRemaining(float progress)
    {
        if (timer != null) timer.SetFloat("TimeRemaining", progress);
    }

    [PublicAPI]
    public void _ShowButton0()
    {
        _ShowButton(0);
    }
    
    [PublicAPI]
    public void _ShowButton1()
    {
        _ShowButton(1);
    }
    
    [PublicAPI]
    public void _ShowButton2()
    {
        _ShowButton(2);
    }
    
    [PublicAPI]
    public void _ShowButton3()
    {
        _ShowButton(3);
    }

    public void _SetTotalNumberOfVotes(int numVotes)
    {
        if (numVotes <= 0 || VRCPlayerApi.GetPlayerCount() == 1) // No vote display in singleplayer
            numVotesTMP.text = "";
        else if (numVotes == 1)
            numVotesTMP.text = $"{numVotes} Vote";
        else
            numVotesTMP.text = $"{numVotes} Votes";
    }

    private void _ShowButton(int i)
    {
        if (quizUIButtons != null && quizUIButtons[i] != null) quizUIButtons[i]._ShowButton();
        PlayClip(clipShowOption, false, .9f + .1f * i);
        if (i + 1 == numAnswers) manager._OnQuestionHasBeenRevealed();
    }

    public void _SetNumberOfAnswers(int[] numberOfAnswers)
    {
        if (quizUIButtons == null) return;
        
        for (var i = 0; i < quizUIButtons.Length; i++)
        {
            quizUIButtons[i]._SetNumberOfVotes(numberOfAnswers[i]);
        }
    }

    public void _RevealAnswer(int submittedAnswer, int quizCorrectAnswerIndex)
    {
        numRevealedCharacters = textQuestion.Length - 1;
        quizUIButtons[quizCorrectAnswerIndex]._SetIsCorrect(true);
        mainAnimator.SetBool("IsCategoryVisible", false);
        if (submittedAnswer >= 0 && submittedAnswer != quizCorrectAnswerIndex) quizUIButtons[submittedAnswer]._SetIsCorrect(false);

        var guessedCorrectly = submittedAnswer == quizCorrectAnswerIndex;
        var hasAnswered = submittedAnswer >= 0;
        var streakEnded = !guessedCorrectly && correctStreak > 0 ? correctStreak : -1;
        correctStreak = guessedCorrectly ? correctStreak + 1 : 0;
        PlayClip(guessedCorrectly ? clipRight : clipWrong);
        var feedbackText = GetFeedbackText(guessedCorrectly, hasAnswered, streakEnded);
        foreach (var tmp in questionIndexTMPs)
        {
            tmp.text = feedbackText;
        }

        timer.SetBool("IsCorrect", guessedCorrectly);
        timer.SetBool("HasSubmittedAnswer", hasAnswered);
        
        categoryTMP.text = $"Source: {cachedQuizCategory.ApiPrettyName}";
        Log($"{submittedAnswer} was submitted, {quizCorrectAnswerIndex} is correct.");
    }

    private string GetFeedbackText(bool guessedCorrectly, bool hasAnswered, int streakEnded)
    {
        if (streakEnded >= 3) return $"<b>{streakEnded} streak over.";
        
        if (correctStreak >= 3)
        {
            switch (correctStreak)
            {
                default: return $"<b>{correctStreak} in a row!";
                case 5: return "<b>ANSWER SPREE!";
                case 10: return "<b>RAMPAGE!!";
                case 15: return "<b>DOMINATING!!!";
                case 20: return "<b>UNSTOPPABLE!!!!";
                case 25: return "<b>WICKED SICK!!!";
            }
        }
        
        var strings = !hasAnswered ? stringsAnswerNone : 
            guessedCorrectly ? stringsAnswerCorrect : stringsAnswerIncorrect;
        Random.InitState(manager._GetSeed());
        return strings[Random.Range(0, strings.Length)];
    }

    public void _SetIsSubmitted(int answerIndex)
    {
        if (answerIndex < 0) return;
        quizUIButtons[answerIndex]._SetIsSubmitted(true);
        PlayClip(clipSubmit);
    }

    private void PlayClip(AudioClip audioClip, bool asOneShot = false, float pitch = 1)
    {
        if (audioSource == null || audioClip == null) return;
        audioSource.pitch = pitch;

        if (asOneShot)
        {
            audioSource.PlayOneShot(audioClip);
        }
        else
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }
    
    public void _ShowDownloadScreen(int questionIndex, int attemptIndex)
    {
        _OnError();
        questionTMP.text = $"Loading question {questionIndex}<br>" +
                           $"Attempt {attemptIndex}" + 
                           "<size=50%>Check 'Allow untrusted URLs' in your settings.";
    }

    public void _OnError()
    {
        PlayClip(clipError, true);
    }
    
    public void _OnTimerStart()
    {
        PlayClip(clipTimerStart, true);
    }

    [PublicAPI]
    public void _PlayGenericSound()
    {
        PlayClip(clipOpenSettings, true);
    }

    #region Logging helper functions
    
    private void Log(string s)
    {
        if (logInfo) Debug.Log($"[{name}] {s}");
    }
    
    private void LogWarning(string s)
    {
        Debug.LogWarning($"[<color=yellow>{name}</color>] {s}");
    }
    
    private void LogError(string s)
    {
        Debug.LogError($"[<color=red>{name}</color>] {s}");
    }
    
    #endregion
}
