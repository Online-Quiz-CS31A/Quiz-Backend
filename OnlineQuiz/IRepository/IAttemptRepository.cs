using OnlineQuiz.Models;

namespace OnlineQuiz.IRepository
{
    public interface IAttemptRepository
    {
        Task<Attempt> CreateAsync(Attempt attempt);
        Task<Attempt?> GetByIdAsync(int attemptId);
        Task<List<Attempt>> GetByQuizIdAsync(int quizId);
        Task<List<Attempt>> GetByQuizIdsAsync(List<int> quizIds);
        Task<List<Attempt>> GetByStudentIdAsync(int studentId);
        Task<List<Attempt>> GetByQuizIdAndUserIdAsync(int quizId, int userId);
        Task<Attempt> UpdateAsync(Attempt attempt);
        Task<bool> DeleteAsync(int attemptId);
        Task<double> GetAverageScoreByCourseAsync(int courseId);
        Task<List<Attempt>> GetRecentAttemptsByStudentAsync(int studentId, int count);
        Task<Dictionary<int, double>> GetAverageScoresByCourseIdsAsync(List<int> courseIds);
        Task<List<Attempt>> GetByQuizIdsAndStudentIdAsync(List<int> quizIds, int studentId);
        Task<int> BulkDeleteAsync(List<int> attemptIds);
        Task<List<Attempt>> GetAllAttemptsForExportAsync(int? quizId, int? courseId);
    }
}
