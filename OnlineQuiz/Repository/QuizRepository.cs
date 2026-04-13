using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using Postgrest;

namespace OnlineQuiz.Repository
{
    public class QuizRepository : IQuizRepository
    {
        private readonly SupabaseService _supabaseService;

        public QuizRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<Quiz> CreateAsync(Quiz quiz)
        {
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var response = await _supabaseService.GetClient().From<Quiz>().Insert(quiz, options);
            var createdQuiz = response.Model ?? throw new InvalidOperationException("Failed to create quiz");
            
            // If QuizId is not populated, fetch by CourseId and Title
            if (createdQuiz.QuizId == 0)
            {
                var result = await _supabaseService.GetClient().From<Quiz>()
                    .Where(q => q.CourseId == quiz.CourseId && q.Title == quiz.Title)
                    .Order("CreatedAt", Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();
                return result.Models.FirstOrDefault() ?? createdQuiz;
            }
            
            return createdQuiz;
        }

        public async Task<Quiz?> GetByIdAsync(int quizId)
        {
            var response = await _supabaseService.GetClient().From<Quiz>()
                .Where(q => q.QuizId == quizId)
                .Single();
            return response;
        }

        public async Task<List<Quiz>> GetByCourseIdAsync(int courseId)
        {
            var response = await _supabaseService.GetClient().From<Quiz>()
                .Where(q => q.CourseId == courseId)
                .Get();
            return response.Models;
        }

        public async Task<Quiz> UpdateAsync(Quiz quiz)
        {
            var response = await _supabaseService.GetClient().From<Quiz>().Update(quiz);
            return response.Model ?? throw new InvalidOperationException("Failed to update quiz");
        }

        public async Task<bool> DeleteAsync(int quizId)
        {
            await _supabaseService.GetClient().From<Quiz>()
                .Where(q => q.QuizId == quizId)
                .Delete();
            return true;
        }

        public async Task<Question> CreateQuestionAsync(Question question)
        {
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var response = await _supabaseService.GetClient().From<Question>().Insert(question, options);
            var created = response.Models.FirstOrDefault() ?? throw new InvalidOperationException("Failed to create question");
            
            // If QuestionId is not populated, fetch it back
            if (created.QuestionId == 0)
            {
                var fetchResult = await _supabaseService.GetClient().From<Question>()
                    .Where(q => q.QuizId == question.QuizId)
                    .Order("QuestionId", Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();
                return fetchResult.Models.FirstOrDefault() ?? created;
            }
            
            return created;
        }

        public async Task<Choice> CreateChoiceAsync(Choice choice)
        {
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var response = await _supabaseService.GetClient().From<Choice>().Insert(choice, options);
            var created = response.Models.FirstOrDefault() ?? throw new InvalidOperationException("Failed to create choice");
            
            // If ChoiceId is not populated, fetch it back
            if (created.ChoiceId == 0)
            {
                var fetchResult = await _supabaseService.GetClient().From<Choice>()
                    .Where(c => c.QuestionId == choice.QuestionId)
                    .Order("ChoiceId", Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();
                return fetchResult.Models.FirstOrDefault() ?? created;
            }
            
            return created;
        }

        public async Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId)
        {
            var response = await _supabaseService.GetClient().From<Question>()
                .Where(q => q.QuizId == quizId)
                .Order("Sort_Order", Postgrest.Constants.Ordering.Ascending)
                .Get();
            return response.Models;
        }

        public async Task<List<Question>> GetQuestionsByQuizIdsAsync(List<int> quizIds)
        {
            if (!quizIds.Any()) return new List<Question>();

            var response = await _supabaseService.GetClient().From<Question>()
                .Filter("QuizId", Postgrest.Constants.Operator.In, quizIds)
                .Order("Sort_Order", Postgrest.Constants.Ordering.Ascending)
                .Get();
            return response.Models;
        }

        public async Task<List<Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds)
        {
            if (!questionIds.Any()) return new List<Choice>();

            var response = await _supabaseService.GetClient().From<Choice>()
                .Filter("QuestionId", Postgrest.Constants.Operator.In, questionIds)
                .Get();
            return response.Models;
        }

        public async Task<List<Quiz>> GetByIdsAsync(List<int> quizIds)
        {
            if (!quizIds.Any()) return new List<Quiz>();

            var response = await _supabaseService.GetClient().From<Quiz>()
                .Filter("QuizId", Postgrest.Constants.Operator.In, quizIds)
                .Get();
            return response.Models;
        }

        public async Task<List<Choice>> GetChoicesByQuestionIdAsync(int questionId)
        {
            var response = await _supabaseService.GetClient().From<Choice>()
                .Where(c => c.QuestionId == questionId)
                .Get();
            return response.Models;
        }

        public async Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold)
        {
            // Fetch quizzes that have a due date and are not yet due (or recently due)
            // Ideally we want Due_At > Now AND Due_At <= Threshold
            // Convert DateTime to ISO 8601 string format for Postgrest compatibility
            
            var nowIso = DateTime.UtcNow.ToString("o");
            var thresholdIso = threshold.ToString("o");
            
            var response = await _supabaseService.GetClient().From<Quiz>()
                .Filter("Due_At", Postgrest.Constants.Operator.GreaterThan, nowIso)
                .Filter("Due_At", Postgrest.Constants.Operator.LessThanOrEqual, thresholdIso)
                .Get();
            return response.Models;
        }

        public async Task<int> CountAsync()
        {
            var response = await _supabaseService.GetClient().From<Quiz>().Count(Postgrest.Constants.CountType.Exact);
            return response;
        }

        public async Task<int> CountByCourseAsync(int courseId)
        {
            var response = await _supabaseService.GetClient().From<Quiz>()
                .Where(q => q.CourseId == courseId)
                .Count(Postgrest.Constants.CountType.Exact);
            return response;
        }

        public async Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds)
        {
            if (!courseIds.Any()) return new List<Quiz>();

            var response = await _supabaseService.GetClient().From<Quiz>()
                .Filter("CourseId", Postgrest.Constants.Operator.In, courseIds)
                .Get();
            return response.Models;
        }

        public async Task<int> CountByCourseIdsAsync(List<int> courseIds)
        {
            if (!courseIds.Any()) return 0;

            var response = await _supabaseService.GetClient().From<Quiz>()
                .Filter("CourseId", Postgrest.Constants.Operator.In, courseIds)
                .Count(Postgrest.Constants.CountType.Exact);
            return response;
        }

        public async Task<int> BulkDeleteAsync(List<int> quizIds)
        {
            if (!quizIds.Any()) return 0;

            await _supabaseService.GetClient().From<Quiz>()
                .Filter("QuizId", Postgrest.Constants.Operator.In, quizIds)
                .Delete();
            return quizIds.Count;
        }
    }
}
