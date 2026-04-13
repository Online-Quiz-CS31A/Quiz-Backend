using OnlineQuiz.DTOs;

namespace OnlineQuiz.IServices
{
    public interface IManualGradingService
    {
        Task<AnswerResponseDto> GradeEssayAnswerAsync(int attemptAnswerId, GradeEssayAnswerDto gradeDto, int teacherId);
        Task<List<AnswerResponseDto>> BulkGradeEssayAnswersAsync(BulkGradeEssayDto bulkGradeDto, int teacherId);
        Task<List<EssayAnswerForGradingDto>> GetPendingEssayAnswersForQuizAsync(int quizId, int teacherId);
        Task<List<PendingEssayGradingDto>> GetAllPendingEssayAnswersForTeacherAsync(int teacherId);
        Task<AttemptResponseDto> RecalculateAttemptScoreAsync(int attemptId, int teacherId);
    }
}
