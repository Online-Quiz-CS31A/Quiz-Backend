using Mapster;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
namespace OnlineQuiz.Services
{
    public class AnswerService : IAnswerService
    {
        private readonly IAttemptRepository _attemptRepository;
        private readonly IAttemptAnswerRepository _answerRepository;
        private readonly IQuizRepository _quizRepository;
        private readonly IUserRepository _userRepository;

        public AnswerService(
            IAttemptRepository attemptRepository,
            IAttemptAnswerRepository answerRepository,
            IQuizRepository quizRepository,
            IUserRepository userRepository)
        {
            _attemptRepository = attemptRepository;
            _answerRepository = answerRepository;
            _quizRepository = quizRepository;
            _userRepository = userRepository;
        }

        public async Task<AnswerResponseDto> RecordAnswerAsync(CreateAnswerDto createAnswerDto, int studentId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(createAnswerDto.AttemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {createAnswerDto.AttemptId} not found");
            }

            if (attempt.UserId != studentId)
            {
                throw new UnauthorizedAccessException("You can only record answers for your own attempts");
            }

            if (attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Cannot record answers for submitted attempts");
            }

            var answer = new AttemptAnswer
            {
                AttemptId = createAnswerDto.AttemptId,
                QuestionId = createAnswerDto.QuestionId,
                ChoiceId = createAnswerDto.ChoiceId,
                FreeText = createAnswerDto.TextAnswer,
                IsCorrect = null // Will be determined during grading
            };

            var createdAnswer = await _answerRepository.CreateAsync(answer);

            return new AnswerResponseDto
            {
                AnswerId = createdAnswer.AttemptAnswerId,
                AttemptId = createdAnswer.AttemptId,
                QuestionId = createdAnswer.QuestionId,
                ChoiceId = createdAnswer.ChoiceId,
                TextAnswer = createdAnswer.FreeText,
                // Note: AttemptAnswer table does not have CreatedAt column.
                // We use UtcNow for new answers, or Attempt.SubmittedAt if available (though here it's null check passed)
                AnsweredAt = DateTime.UtcNow,
                IsCorrect = attempt.SubmittedAt != null ? createdAnswer.IsCorrect : null
            };
        }

        public async Task<AttemptWithAnswersDto> GetAnswersForAttemptAsync(int attemptId, int userId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(attemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {attemptId} not found");
            }

            // Fetch additional details
            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            var student = await _userRepository.GetByIdAsync(attempt.UserId);

            var answers = await _answerRepository.GetByAttemptIdAsync(attemptId);
            var answerDtos = new List<AnswerResponseDto>();

            foreach (var answer in answers)
            {
                answerDtos.Add(new AnswerResponseDto
                {
                    AnswerId = answer.AttemptAnswerId,
                    AttemptId = answer.AttemptId,
                    QuestionId = answer.QuestionId,
                    ChoiceId = answer.ChoiceId,
                    TextAnswer = answer.FreeText,
                    AnsweredAt = attempt.SubmittedAt ?? attempt.StartedAt,
                    IsCorrect = attempt.SubmittedAt != null ? answer.IsCorrect : null
                });
            }

            return new AttemptWithAnswersDto
            {
                AttemptId = attempt.AttemptId,
                UserId = attempt.UserId,
                StudentName = student?.FullName,
                QuizId = attempt.QuizId,
                QuizTitle = quiz?.Title,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                Score = attempt.Score,
                TimeSpentSeconds = attempt.TimeSpentSeconds,
                Answers = answerDtos
            };
        }

        public async Task<AnswerResponseDto> UpdateAnswerAsync(int answerId, CreateAnswerDto updateAnswerDto, int studentId)
        {
            var answer = await _answerRepository.GetByIdAsync(answerId);
            if (answer == null)
            {
                throw new ArgumentException($"Answer with ID {answerId} not found");
            }

            var attempt = await _attemptRepository.GetByIdAsync(answer.AttemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt associated with answer {answerId} not found");
            }

            if (attempt.UserId != studentId)
            {
                throw new UnauthorizedAccessException("You can only update answers for your own attempts");
            }

            if (attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Cannot update answers for submitted attempts");
            }

            // Update fields
            answer.ChoiceId = updateAnswerDto.ChoiceId;
            answer.FreeText = updateAnswerDto.TextAnswer;
            // QuestionId shouldn't really change for an existing answer record usually, but let's keep it consistent if passed
            // actually, usually we just update the choice/text. Let's assume QuestionId matches or we update it too.
            // For safety, let's update it if provided, or just ignore if it's meant to be the same question.
            // Given CreateAnswerDto has QuestionId, we might update it, but it's weird to change the question of an answer.
            // I'll update ChoiceId and FreeText.

            var updatedAnswer = await _answerRepository.UpdateAsync(answer);

            return new AnswerResponseDto
            {
                AnswerId = updatedAnswer.AttemptAnswerId,
                AttemptId = updatedAnswer.AttemptId,
                QuestionId = updatedAnswer.QuestionId,
                ChoiceId = updatedAnswer.ChoiceId,
                TextAnswer = updatedAnswer.FreeText,
                AnsweredAt = DateTime.UtcNow, 
                IsCorrect = null
            };
        }

        public async Task<bool> DeleteAnswerAsync(int answerId, int studentId)
        {
            var answer = await _answerRepository.GetByIdAsync(answerId);
            if (answer == null)
            {
                throw new ArgumentException($"Answer with ID {answerId} not found");
            }

            var attempt = await _attemptRepository.GetByIdAsync(answer.AttemptId);
            if (attempt == null)
            {
                // If attempt is gone, maybe answer should be gone too, but let's be safe
                throw new ArgumentException($"Attempt associated with answer {answerId} not found");
            }

            if (attempt.UserId != studentId)
            {
                throw new UnauthorizedAccessException("You can only delete answers for your own attempts");
            }

            if (attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Cannot delete answers for submitted attempts");
            }

            return await _answerRepository.DeleteAsync(answerId);
        }

        public async Task<List<AnswerResponseDto>> RecordBulkAnswersAsync(BulkAnswerRequestDto bulkAnswerDto, int studentId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(bulkAnswerDto.AttemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {bulkAnswerDto.AttemptId} not found");
            }

            if (attempt.UserId != studentId)
            {
                throw new UnauthorizedAccessException("You can only record answers for your own attempts");
            }

            if (attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Cannot record answers for submitted attempts");
            }

            var responses = new List<AnswerResponseDto>();

            // Optimization: We could do bulk insert if repository supports it, but for now loop is fine
            // or we can delete existing answers for these questions first?
            // "Bulk submission" usually implies "save all these". 
            // If answers exist for these questions, we should probably update them or delete/re-create.
            // Let's assume we delete existing answers for the questions involved and create new ones.
            
            // Fetch existing answers for this attempt
            var existingAnswers = await _answerRepository.GetByAttemptIdAsync(bulkAnswerDto.AttemptId);
            var existingMap = existingAnswers.ToDictionary(a => a.QuestionId, a => a);

            foreach (var ansDto in bulkAnswerDto.Answers)
            {
                if (existingMap.TryGetValue(ansDto.QuestionId, out var existingAnswer))
                {
                    // Update existing
                    existingAnswer.ChoiceId = ansDto.ChoiceId;
                    existingAnswer.FreeText = ansDto.TextAnswer;
                    await _answerRepository.UpdateAsync(existingAnswer);
                    
                    responses.Add(new AnswerResponseDto
                    {
                        AnswerId = existingAnswer.AttemptAnswerId,
                        AttemptId = existingAnswer.AttemptId,
                        QuestionId = existingAnswer.QuestionId,
                        ChoiceId = existingAnswer.ChoiceId,
                        TextAnswer = existingAnswer.FreeText,
                        AnsweredAt = attempt.SubmittedAt ?? attempt.StartedAt, // Consistent with fix M2
                        IsCorrect = null
                    });
                }
                else
                {
                    // Create new
                    var newAnswer = new AttemptAnswer
                    {
                        AttemptId = bulkAnswerDto.AttemptId,
                        QuestionId = ansDto.QuestionId,
                        ChoiceId = ansDto.ChoiceId,
                        FreeText = ansDto.TextAnswer,
                        IsCorrect = null
                    };
                    var created = await _answerRepository.CreateAsync(newAnswer);
                    
                    responses.Add(new AnswerResponseDto
                    {
                        AnswerId = created.AttemptAnswerId,
                        AttemptId = created.AttemptId,
                        QuestionId = created.QuestionId,
                        ChoiceId = created.ChoiceId,
                        TextAnswer = created.FreeText,
                        AnsweredAt = attempt.SubmittedAt ?? attempt.StartedAt,
                        IsCorrect = null
                    });
                }
            }

            return responses;
        }
    }
}

