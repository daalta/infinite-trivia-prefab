
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace QuizCategories
{
    public class QuizCategoryOTDB : QuizCategory
    {
        public override string ApiPrettyName => "Open Trivia DB";

        protected override DataDictionary[] ParseQuizJson(string json)
        {
            Log($"Starting to deserialize questions for category {CategoryPrettyName}.");
        
            json = json.Replace("&quot;", "'");
            json = json.Replace("&eacute;", "é");
            json = json.Replace("&#039;", "'");
            json = json.Replace("&micro;", "π");
            json = json.Replace("&amp;", "&");
            json = json.Replace("&sup2;", "2");
            json = json.Replace("&shy;", "-");
            json = json.Replace("&deg;", "°");
            json = json.Replace("&ldquo;", "'");
        
            var success = VRCJson.TryDeserializeFromJson(json, out var token);
            if (!success)
            {
                LogError("Json was not parsed successfully: " + json);
                return null;
            }

            token.DataDictionary.TryGetValue("results", TokenType.DataList, out var v);

            var questionList = v.DataList;
            var questions = new DataDictionary[questionList.Count];
            
            // Generate random question indices because OpenTDB's randomization is bugged
            var questionIndicesRandom = new int[questions.Length];
            for (var i = 0; i < questions.Length; i++)
            {
                questionIndicesRandom[i] = i;
            }
            
            UnityEngine.Random.InitState(json.GetHashCode());
            Utilities.ShuffleArray(questionIndicesRandom);
        
            // Log Questions & Answers
            for (var i = 0; i < questions.Length; i++)
            {
                var question = questionList[i].DataDictionary;
                questions[questionIndicesRandom[i]] = ParseQuestion(question);
            }
            Log($"Finished parsing {questions.Length} questions for category {CategoryPrettyName}.");
            return questions;
        }

        protected override DataDictionary ParseQuestion(DataDictionary data)
        {
            var questionData = new DataDictionary();

            data.TryGetValue("difficulty", TokenType.String, out var value);
            questionData.Add("difficulty", value.String);


            data.TryGetValue("question", TokenType.String, out value);
            questionData.Add("question", value.String);

            data.TryGetValue("correct_answer", TokenType.String, out value);
            questionData.Add("correctAnswer", value.String);


            data.TryGetValue("incorrect_answers", TokenType.DataList, out value);
            questionData.Add("incorrectAnswers", value.DataList);

            return questionData;
        }
    }
}
