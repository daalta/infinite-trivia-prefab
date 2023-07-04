
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Downloads and parses questions.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public abstract class QuizCategory : UdonSharpBehaviour
{
    [SerializeField, Tooltip("Used for calculating how often this category should appear.")]
    private int totalNumberOfQuestionsInCategory;
    
    [SerializeField, UdonSynced, Tooltip("Multiplier by the number of questions to calculate the frequency.")]
    private float frequencyMultiplier = 1;
    
    [SerializeField]
    private bool logInfoMessages = false;

    [Header("References")]
    [SerializeField, Tooltip("URL to download questions from")]
    private VRCUrl url = new VRCUrl("https://the-trivia-api.com/v2/questions?limit=50");

    [SerializeField] private Sprite icon;

    private string QuizJson
    {
        get => quizJson;
        set
        {
            if (quizJson == value) return;
            quizJson = value;
            questions = ParseQuizJson(quizJson);
        }
    }
    
    /// <summary>
    /// Synced unparsed JSON of downloaded question data. Downloaded the owner and sent to all other player.
    /// Each player parses the question data locally after receiving it.
    /// </summary>
    [UdonSynced, FieldChangeCallback(nameof(QuizJson))] private string quizJson;

    /// <summary>
    /// Index of the current question.
    /// </summary>
    [UdonSynced] private int questionIndex = -1;

    /// <summary>
    /// The name of the API. Will be shown to players, citing it as the source of the current question.
    /// </summary>
    public abstract string ApiPrettyName
    {
        get;
    }

    public string CategoryPrettyName => gameObject.name;

    private DataDictionary[] questions;
    
    private bool isDownloadQueued = false;
    
    public void _QueueDownloadNewQuestions(float delay)
    {
        if (isDownloadQueued)
        {
            LogWarning($"Did not queue question download for {CategoryPrettyName}, already queued.");
            return;
        }
        ;
        isDownloadQueued = true;
        if (delay == 0) _DownloadNewQuestions();
        else SendCustomEventDelayedSeconds(nameof(_DownloadNewQuestions), Mathf.Max(float.Epsilon, delay));
    }
    
    /// <summary>
    /// Download new questions from the internet. Always use QueueDownloadNewQuestions instead.
    /// </summary>
    public void _DownloadNewQuestions()
    {
        if (!isDownloadQueued) return;
        isDownloadQueued = false;

        if (!Networking.IsOwner(gameObject))
        {
            LogWarning($"Did not download questions for {CategoryPrettyName}, local player is not the owner.");
            return;
        }

        Log($"Downloading new questions for category {CategoryPrettyName}.");
        
        // ReSharper disable once SuspiciousTypeConversion.Global
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }
    
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        Log($"Successfully downloaded new questions for category {CategoryPrettyName}.");
        QuizJson = result.Result;
        questionIndex = 0;
        RequestSerialization();
        OnDeserialization();
    }
    
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        _QueueDownloadNewQuestions(6);
    }

    protected abstract DataDictionary[] ParseQuizJson(string json);

    protected abstract DataDictionary ParseQuestion(DataDictionary data);
    public int _GetNumQuestionsRemaining()
    {
        if (QuizJson == null || questions == null || questions.Length == 0) return 0;
        return questions.Length - questionIndex;
    }
    
    public float _GetFrequency()
    {
        if (totalNumberOfQuestionsInCategory <= 0) return 0;
        return totalNumberOfQuestionsInCategory * frequencyMultiplier;
    }

    public Sprite _GetIcon()
    {
        return icon;
    }

    public DataDictionary _GetCurrentQuestion()
    {
        if (questions == null)
        {
            LogError($"Category {CategoryPrettyName} has no questions loaded.");
            return null;
        }
        
        if (questionIndex < 0 || questionIndex >= questions.Length)
        {
            LogError($"Category {CategoryPrettyName} has invalid question index {questionIndex}");
            return null;
        }

        var question = questions[questionIndex];

        if (question == null)
        {
            LogError($"Current question in category {CategoryPrettyName} is null.");
        }
        
        return questions[questionIndex];
    }

    public void _PrepareNextQuestion()
    {
        if (questions == null || questionIndex + 1 >= questions.Length)
        {
            _QueueDownloadNewQuestions(0);
            return;
        }
        
        questionIndex++;
        Log($"Ready for question {questionIndex+1} of category {CategoryPrettyName}.");
        
        RequestSerialization();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        Log($"Category {CategoryPrettyName} has been updated.");
    }
    
        
    #region Logging helper functions

    protected void Log(string s)
    {
        if (logInfoMessages) Debug.Log($"[{name}] {s}");
    }
    
    protected void LogWarning(string s)
    {
        Debug.LogWarning($"[<color=yellow>{name}</color>] {s}");
    }
    
    protected void LogError(string s)
    {
        Debug.LogError($"[<color=red>{name}</color>] {s}");
    }

    #endregion

}
