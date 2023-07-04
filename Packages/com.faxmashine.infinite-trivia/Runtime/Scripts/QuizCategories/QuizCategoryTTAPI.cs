
using VRC.SDK3.Data;

namespace QuizCategories
{
    public class QuizCategoryTTAPI : QuizCategory
    {
        public override string ApiPrettyName => "The Trivia API";

        protected override DataDictionary[] ParseQuizJson(string json)
        {
            Log($"Starting to deserialize questions for category {CategoryPrettyName}.");
            var success = VRCJson.TryDeserializeFromJson(json, out var token);
            if (!success)
            {
                LogError("Json was not parsed successfully.");
                return null;
            }
    
            var questionList = token.DataList;
            var questions = new DataDictionary[questionList.Count];
        
            // Log Questions & Answers
            for (var i = 0; i < questions.Length; i++)
            {
                var question = questionList[i].DataDictionary;
                questions[i] = ParseQuestion(question);
            }
            Log($"Finished parsing {questions.Length} questions for category {CategoryPrettyName}.");
            return questions;
        }

        protected override DataDictionary ParseQuestion(DataDictionary data)
        {
            var questionData = new DataDictionary();

            data.TryGetValue("difficulty", TokenType.String, out var value);
            questionData.Add("difficulty", value.String);


            data.TryGetValue("question", TokenType.DataDictionary, out value);
            value.DataDictionary.TryGetValue("text", TokenType.String, out value);
            questionData.Add("question", value.String);

            data.TryGetValue("correctAnswer", TokenType.String, out value);
            questionData.Add("correctAnswer", value.String);


            data.TryGetValue("incorrectAnswers", TokenType.DataList, out value);
            questionData.Add("incorrectAnswers", value.DataList);

            return questionData;
        }
    }
}
