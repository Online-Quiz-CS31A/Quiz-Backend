using OnlineQuiz.Models;

namespace OnlineQuiz.IRepository
{
    public interface IAttemptAnswerRepository
    {
        Task<AttemptAnswer> CreateAsync(AttemptAnswer answer);
        Task<AttemptAnswer?> GetByIdAsync(int answerId);
        Task<List<AttemptAnswer>> GetByAttemptIdAsync(int attemptId);
        Task<List<AttemptAnswer>> GetByAttemptIdsAsync(List<int> attemptIds);
        Task<AttemptAnswer> UpdateAsync(AttemptAnswer answer);
        Task<bool> DeleteAsync(int answerId);
    }
}
