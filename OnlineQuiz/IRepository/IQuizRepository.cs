using OnlineQuiz.Models;

namespace OnlineQuiz.IRepository
{
    public interface IQuizRepository
    {
        Task<Quiz> CreateAsync(Quiz quiz);
        Task<Quiz?> GetByIdAsync(int quizId);
        Task<List<Quiz>> GetByCourseIdAsync(int courseId);
        Task<Quiz> UpdateAsync(Quiz quiz);
        Task<bool> DeleteAsync(int quizId);
        
        // Question and Choice management
        Task<Question> CreateQuestionAsync(Question question);
        Task<Choice> CreateChoiceAsync(Choice choice);
        Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId);
        Task<List<Question>> GetQuestionsByQuizIdsAsync(List<int> quizIds);
        Task<List<Choice>> GetChoicesByQuestionIdAsync(int questionId);
        Task<List<Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds);
        Task<List<Quiz>> GetByIdsAsync(List<int> quizIds);
        Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold);
        Task<int> CountAsync();
        Task<int> CountByCourseAsync(int courseId);
        Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds);
        Task<int> CountByCourseIdsAsync(List<int> courseIds);
        Task<int> BulkDeleteAsync(List<int> quizIds);
    }
}
