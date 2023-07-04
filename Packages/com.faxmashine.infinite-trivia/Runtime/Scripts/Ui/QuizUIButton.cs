using JetBrains.Annotations;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class QuizUIButton : UdonSharpBehaviour
{
    public QuizManager Manager { private get; set; }
    public int ButtonIndex { private get; set; }
    [SerializeField] private TextMeshProUGUI textAnswer;
    [SerializeField] private TextMeshProUGUI textNumberOfAnswers;
    [SerializeField] private Animator animator;
    [SerializeField] private bool logInfoText;


    [PublicAPI]
    public void _SubmitAnswer()
    {
        Log($"Submitting answer {ButtonIndex}: \"{textAnswer.text}\"");
        Manager._SubmitAnswer(ButtonIndex);
    }

    private void Reset()
    {
        animator.SetBool("IsHidden", true);
        animator.SetBool("IsSubmitted", false);
        animator.SetBool("IsCorrect", false);
        animator.SetBool("IsWrong", false);
        textNumberOfAnswers.enabled = false;
    }

    public void _SetText(string s)
    {
        Reset();
        textAnswer.text = s;
    }

    public void _ShowButton()
    {
        animator.SetBool("IsHidden", false);
    }

    public void _SetIsCorrect(bool b)
    {
        animator.SetBool("IsCorrect", b);
        animator.SetBool("IsWrong", !b);
    }

    public void _SetIsSubmitted(bool b)
    {
        animator.SetBool("IsSubmitted", b);
    }

    public void _SetNumberOfVotes(int numberOfVotes)
    {
        if (textNumberOfAnswers == null) return;
        var hasVotes = numberOfVotes > 0;
        textNumberOfAnswers.enabled = hasVotes;
        if (hasVotes) textNumberOfAnswers.text = $"{numberOfVotes} Votes";

    }
    
    #region Logging helper functions
    
    protected void Log(string s)
    {
        if (logInfoText) Debug.Log($"[{name}] {s}");
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