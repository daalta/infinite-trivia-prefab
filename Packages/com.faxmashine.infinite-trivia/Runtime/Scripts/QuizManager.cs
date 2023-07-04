using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QuizManager : UdonSharpBehaviour
{
    [Header("Timer length in seconds")]
    [SerializeField] private float timerLengthMax = 5;
    [SerializeField] private float timerLengthMaxSingleplayer = 1;
    
    [SerializeField] private float timeBetweenQuestions = 4;

    [Header("Logging")]
    [Tooltip("Whether to enable non-critical logging information.")]
    [SerializeField] private bool logInfo = false;
    
    [Header("References (Do not remove)")]
    [SerializeField] private QuizUIMenu menuUi;

    [SerializeField] private GameObject categoriesParent;

    [UdonSynced] private int currentRoundIndex = -1;
    [UdonSynced] private int currentCategoryIndex = -1;

    /// <summary>
    /// The local player's guess for the correct answer.
    /// </summary>
    private int submittedAnswer = -1;

    /// <summary>
    /// The answers received by all players for all answers
    /// </summary>
    private int[] submittedNetAnswers = new int[4];

    /// <summary>
    /// Index of the correct answer. Updated after loading the current question.
    /// </summary>
    private int quizCorrectAnswerIndex;
    
    private bool hasQuestionBeenRevealed;
    private bool hasAnswerBeenRevealed;
    private bool isLocalPlayerAlone;
    
    private bool isTimerUpdateQueued;
    private float timer = -1;
    private int numAnswersLastRound;

    private bool isQuestionLoaded;
    
    private QuizCategory[] categories;

    private int numDownloadAttempts;

    private void Start()
    {
        Log("Welcome to the Quiz Prefab! Please contact Fax#6041 if you run into any issues.");
        
        if (!TrySetCategories())
        {
            LogError("Couldn't find any quiz categories.");
            return;
        }

        if (!TrySetManagerReferences())
        {
            LogError("Unable to set manager references.");
            return;
        }
        if (!Networking.IsOwner(gameObject)) return;
        
        PrepareNextRound();
    }

    private bool TrySetCategories()
    {
        categories = categoriesParent.GetComponentsInChildren<QuizCategory>();
        return categories.Length > 0;
    }

    private int GetRandomCategoryIndex()
    {
        var totalWeight = 0f;
        foreach (var category in categories)
        {
            totalWeight += category._GetFrequency();
        }

        Random.InitState(Time.frameCount);
        var randomValue = UnityEngine.Random.Range(0, totalWeight);

        for (var i = 0; i < categories.Length; i++)
        {
            randomValue -= categories[i]._GetFrequency();
            if (randomValue <= 0) return i;
        }
        
        LogWarning("Random category selection failed. Falling back to category at index 0.");
        return 0;
    }

    private bool TrySetManagerReferences()
    {
        if (menuUi == null) LogError("Menu UI reference is missing!");
        else
        {
            menuUi.Manager = this;
            return true;
        }

        return false;
    }

    public void _UpdateTimer()
    {
        isTimerUpdateQueued = false;
        
        if (timer <= 0) return;
        
        var percentagePlayersAnswered = Mathf.Clamp01((float) GetNumAnswers() / numAnswersLastRound);
        timer -= Time.deltaTime * percentagePlayersAnswered * (!isLocalPlayerAlone ? 1 : timerLengthMax / timerLengthMaxSingleplayer);
        menuUi.SetTimeRemaining(timer / timerLengthMax);

        if (timer <= 0)
        {
            timer = -1;
            _RevealAnswer();
            return;
        }

        SendCustomEventDelayedFrames(nameof(_UpdateTimer), 1);
        isTimerUpdateQueued = true;

    }
    
    private void _RevealAnswer()
    {
        hasAnswerBeenRevealed = true;

        _RevealNumberOfAnswers();

        menuUi._RevealAnswer(submittedAnswer, quizCorrectAnswerIndex);

        if (Networking.IsOwner(gameObject)) PrepareNextRound(timeBetweenQuestions);
    }

    private void _RevealNumberOfAnswers()
    {       // if at least one remote player answered or at least one remote and local player answered
        var numAnswers = GetNumAnswers();
        if (numAnswers >= 2 || (numAnswers > 0 && submittedAnswer == -1))
            menuUi._SetNumberOfAnswers(submittedNetAnswers); 
    }

    /// <summary>
    /// Loads the next round without requesting serialization.
    /// After a question's answer is revealed, the next question should already be downloaded.
    /// </summary>
    public void PrepareNextRound(float syncDelay = 1)
    {
        if (!Networking.IsOwner(gameObject)) return;
        
        Log("Preparing next quiz round.");
        
        if (categories == null)
        {
            LogError("Categories array is null.");
            return;
        }

        if (currentCategoryIndex >= categories.Length)
        {
            LogError("Category index is higher than the amount of categories.");
            return;
        }
        
        currentRoundIndex++;
        currentCategoryIndex = GetRandomCategoryIndex();

        var currentCategory = categories[currentCategoryIndex];

        if (currentCategory == null)
        {
            LogError("Current category is null.");
            return;
        }
        
        Log($"Current round is {currentRoundIndex}. Current category is {currentCategory.CategoryPrettyName}.");

        currentCategory._PrepareNextQuestion();
        SendCustomEventDelayedSeconds(nameof(_RequestSerializationIfQuizReady), syncDelay);
    }

    public void _RequestSerializationIfQuizReady()
    {
        if (!Networking.IsOwner(gameObject))
        {
            LogWarning("Non-master should not be trying to check if the quiz is ready.");
            return;
        }
        
        var delay = 1;
        if (!IsQuizReady())
        {
            numDownloadAttempts++;
            if (numDownloadAttempts > 5)
            {
                LogError($"Failed to download category {currentCategoryIndex}. Trying next category.");
                PrepareNextRound(0.1f);
                return;
            }
            
            LogWarning($"Category is not ready yet. Delaying by {delay} seconds.");
            menuUi._ShowDownloadScreen(currentRoundIndex, numDownloadAttempts);
            SendCustomEventDelayedSeconds(nameof(_RequestSerializationIfQuizReady), delay);
            return;
        }

        numDownloadAttempts = 0;
        
        RequestSerialization();
        OnDeserialization();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (hasAnswerBeenRevealed) PrepareNextRound();
    }
    
    public void StartQuestionTimer()
    {
        // Timer already started
        if (timer != -1 || hasAnswerBeenRevealed) return;


        if (!hasQuestionBeenRevealed) return;
        
        timer = timerLengthMax;
        isLocalPlayerAlone = VRCPlayerApi.GetPlayerCount() <= 1;
        menuUi._OnTimerStart();
        if (!isTimerUpdateQueued) _UpdateTimer();
    }

    private void LoadCurrentQuestion()
    {
        Log("Loading current question.");
        
        if (currentRoundIndex < 0)
        {
            LogError("Round index is invalid, not loading question.");
            return;
        }

        var currentQuestion = categories[currentCategoryIndex]._GetCurrentQuestion();
        currentQuestion.TryGetValue("question", TokenType.String, out var question);
        currentQuestion.TryGetValue("correctAnswer", TokenType.String, out var correct);
        currentQuestion.TryGetValue("incorrectAnswers", TokenType.DataList, out var incorrect);
        
        var numAnswers = 1 + incorrect.DataList.Count;
        var answerIndicesShuffled = new int[numAnswers];
        for (var i = 0; i < answerIndicesShuffled.Length; i++) answerIndicesShuffled[i] = i;

        // Same shuffle for all players
        Random.InitState(_GetSeed());
        Utilities.ShuffleArray(answerIndicesShuffled);

        var answersShuffled = new string[1 + incorrect.DataList.Count];
        var incorrectAnswers = incorrect.DataList.ToArray();
        for (var i = 0; i < numAnswers; i++)
        {
            var answerIndex = answerIndicesShuffled[i];
            if (answerIndex == 0)
            {
                answersShuffled[i] = correct.String;
                quizCorrectAnswerIndex = i;
            }
            else
            {
                answersShuffled[i] = incorrectAnswers[answerIndex - 1].String;
            }
        }

        isQuestionLoaded = true;
        
        Log($"\"{question.String}\"");

        if (menuUi == null) return;

        menuUi.UIUpdate(question.String,
            answersShuffled, 
            categories[currentCategoryIndex],
            currentRoundIndex);
        
        menuUi._SetTotalNumberOfVotes(0);
    }

    public int _GetSeed()
    {
        if (categories == null || currentCategoryIndex < 0 || currentCategoryIndex >= categories.Length) return 0;
        var currentQuestion = categories[currentCategoryIndex]._GetCurrentQuestion();
        if (currentQuestion == null) return 0;
        currentQuestion.TryGetValue("question", TokenType.String, out var question);
        return currentRoundIndex + question.String.GetHashCode();
    }

    public void _SubmitAnswer(int answerIndex)
    {
        if (submittedAnswer >= 0 || hasAnswerBeenRevealed)
        {
            menuUi._OnError();
            return; // Local player has already answered or answer has already been revealed
        }
        submittedAnswer = answerIndex;
        menuUi._SetIsSubmitted(answerIndex);

        string eventName;
        switch (answerIndex)
        {
            default:
                eventName = nameof(SubmitNetworkedAnswer0);
                break;
            case 1:
                eventName = nameof(SubmitNetworkedAnswer1);
                break;
            case 2:
                eventName = nameof(SubmitNetworkedAnswer2);
                break;
            case 3:
                eventName = nameof(SubmitNetworkedAnswer3);
                break;
        }

        SendCustomNetworkEvent(NetworkEventTarget.All, eventName);
    }

    public void SubmitNetworkedAnswer0() { SubmitNetworkedAnswer(0); }
    public void SubmitNetworkedAnswer1() { SubmitNetworkedAnswer(1); }
    public void SubmitNetworkedAnswer2() { SubmitNetworkedAnswer(2); }
    public void SubmitNetworkedAnswer3() { SubmitNetworkedAnswer(3); }

    private void SubmitNetworkedAnswer(int i)
    {
        submittedNetAnswers[i]++;
        Log($"Received networked answer {i}");
        menuUi._SetTotalNumberOfVotes(GetNumAnswers());
        StartQuestionTimer();
        if (hasAnswerBeenRevealed) _RevealNumberOfAnswers();
    }

    public void _OnQuestionHasBeenRevealed()
    {
        hasQuestionBeenRevealed = true;
        if (GetNumAnswers() > 0) StartQuestionTimer();
    }

    public override void OnDeserialization()
    {
        isQuestionLoaded = false; // TODO Is it safe to assume that OnDeserialization should load a new question?
        _OnQuizHasBeenUpdated();
    }

    public void _OnQuizHasBeenUpdated()
    {
        Log($"Quiz has been updated! Round {currentRoundIndex}, category {currentCategoryIndex}.");
        
        if (!IsQuizReady())
        {
            LogWarning("Tried to deserialize quiz manager before quiz was ready. Trying again...");
            if (Networking.IsClogged) LogWarning("Networking is clogged.");
            SendCustomEventDelayedSeconds(nameof(_OnQuizHasBeenUpdated), 1);
            return;
        }

        if (isQuestionLoaded)
        {
            LogWarning("Current question has already been loaded. Quiz update cancelled.");
            return;
        }
        
        LoadCurrentQuestion();
        
        submittedAnswer = -1;
        // Prevent timer from decreasingly too quickly between rounds if many players suddenly left.
        numAnswersLastRound = Mathf.Max(numAnswersLastRound / 2, GetNumAnswers()); 
        submittedNetAnswers = new int[4];
        hasAnswerBeenRevealed = false;
        hasQuestionBeenRevealed = false;
    }

    private int GetNumAnswers()
    {
        if (submittedNetAnswers == null) return 1; 
        var total = 0;
        foreach (var num in submittedNetAnswers)
        {
            total += num;
        }

        return total;
    }

    private bool IsQuizReady()
    {
        if (currentRoundIndex < 0) LogWarning("Current round index is less than zero.");
        else if (currentCategoryIndex < 0) LogWarning("Current category index is less than zero.");
        else if (categories[currentCategoryIndex] == null) LogError("Current category is null.");
        else if (categories[currentCategoryIndex]._GetCurrentQuestion() == null) LogError("Current category is null.");
        else return true;

        LogWarning("Quiz is not ready.");
        return false;
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