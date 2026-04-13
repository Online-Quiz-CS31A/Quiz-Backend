using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Services
{
    public class ManualGradingService : IManualGradingService
    {
        private readonly IAttemptAnswerRepository _answerRepository;
        private readonly IAttemptRepository _attemptRepository;
        private readonly IQuizRepository _quizRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IUserRepository _userRepository;

        public ManualGradingService(
            IAttemptAnswerRepository answerRepository,
            IAttemptRepository attemptRepository,
            IQuizRepository quizRepository,
            ICourseRepository courseRepository,
            IUserRepository userRepository)
        {
            _answerRepository = answerRepository;
            _attemptRepository = attemptRepository;
            _quizRepository = quizRepository;
            _courseRepository = courseRepository;
            _userRepository = userRepository;
        }

        public async Task<AnswerResponseDto> GradeEssayAnswerAsync(int attemptAnswerId, GradeEssayAnswerDto gradeDto, int teacherId)
        {
            // Fetch the answer
            var answer = await _answerRepository.GetByIdAsync(attemptAnswerId);
            if (answer == null)
            {
                throw new ArgumentException($"Answer with ID {attemptAnswerId} not found");
            }

            // Fetch the attempt
            var attempt = await _attemptRepository.GetByIdAsync(answer.AttemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {answer.AttemptId} not found");
            }

            // Verify attempt is submitted
            if (attempt.SubmittedAt == null)
            {
                throw new InvalidOperationException("Cannot grade answers for unsubmitted attempts");
            }

            // Fetch the question
            var questions = await _quizRepository.GetQuestionsByQuizIdAsync(attempt.QuizId);
            var question = questions.FirstOrDefault(q => q.QuestionId == answer.QuestionId);
            if (question == null)
            {
                throw new ArgumentException($"Question with ID {answer.QuestionId} not found");
            }

            // Verify it's an essay question
            if (!QuestionTypeConstants.IsEssayType(question.Type))
            {
                throw new InvalidOperationException($"Manual grading is only allowed for essay/text questions. This question is of type: {question.Type}");
            }

            // Verify teacher authorization
            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {attempt.QuizId} not found");
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null || course.InstructorUserId != teacherId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can grade essay answers");
            }

            // Update the answer
            answer.IsCorrect = gradeDto.IsCorrect;
            answer.PointsAwarded = gradeDto.PointsAwarded;
            answer.Feedback = gradeDto.Feedback;
            var updatedAnswer = await _answerRepository.UpdateAsync(answer);

            // Recalculate attempt score after grading
            await RecalculateAttemptScoreAsync(answer.AttemptId, teacherId);

            return new AnswerResponseDto
            {
                AnswerId = updatedAnswer.AttemptAnswerId,
                AttemptId = updatedAnswer.AttemptId,
                QuestionId = updatedAnswer.QuestionId,
                ChoiceId = updatedAnswer.ChoiceId,
                TextAnswer = updatedAnswer.FreeText,
                AnsweredAt = attempt.SubmittedAt ?? attempt.StartedAt,
                IsCorrect = updatedAnswer.IsCorrect
            };
        }

        public async Task<List<AnswerResponseDto>> BulkGradeEssayAnswersAsync(BulkGradeEssayDto bulkGradeDto, int teacherId)
        {
            // Verify attempt exists and is submitted
            var attempt = await _attemptRepository.GetByIdAsync(bulkGradeDto.AttemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {bulkGradeDto.AttemptId} not found");
            }

            if (attempt.SubmittedAt == null)
            {
                throw new InvalidOperationException("Cannot grade answers for unsubmitted attempts");
            }

            // Verify teacher authorization
            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {attempt.QuizId} not found");
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null || course.InstructorUserId != teacherId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can grade essay answers");
            }

            var responses = new List<AnswerResponseDto>();

            foreach (var gradeDto in bulkGradeDto.Grades)
            {
                var response = await GradeEssayAnswerAsync(gradeDto.AttemptAnswerId, gradeDto, teacherId);
                responses.Add(response);
            }

            // Recalculate the attempt score after grading
            await RecalculateAttemptScoreAsync(bulkGradeDto.AttemptId, teacherId);

            return responses;
        }

        public async Task<List<EssayAnswerForGradingDto>> GetPendingEssayAnswersForQuizAsync(int quizId, int teacherId)
        {
            // Verify quiz exists and teacher authorization
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {quizId} not found");
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null || course.InstructorUserId != teacherId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can view pending essay answers");
            }

            return await GetPendingEssayAnswersForQuizInternalAsync(quizId);
        }

        /// <summary>
        /// Internal method to get pending essay answers without authorization checks.
        /// Used by GetAllPendingEssayAnswersForTeacherAsync to avoid redundant checks.
        /// </summary>
        private async Task<List<EssayAnswerForGradingDto>> GetPendingEssayAnswersForQuizInternalAsync(int quizId)
        {
            // Get all submitted attempts for this quiz
            var attempts = await _attemptRepository.GetByQuizIdAsync(quizId);
            var submittedAttempts = attempts.Where(a => a.SubmittedAt != null).ToList();

            if (!submittedAttempts.Any())
            {
                return new List<EssayAnswerForGradingDto>();
            }

            // Get all questions for this quiz
            var questions = await _quizRepository.GetQuestionsByQuizIdAsync(quizId);
            var essayQuestions = questions.Where(q => QuestionTypeConstants.IsEssayType(q.Type)).ToList();

            if (!essayQuestions.Any())
            {
                return new List<EssayAnswerForGradingDto>();
            }

            var essayQuestionIds = essayQuestions.Select(q => q.QuestionId).ToHashSet();
            var questionMap = essayQuestions.ToDictionary(q => q.QuestionId);

            // Batch fetch all answers for all attempts at once - OPTIMIZED!
            var attemptIds = submittedAttempts.Select(a => a.AttemptId).ToList();
            var allAnswers = await _answerRepository.GetByAttemptIdsAsync(attemptIds);

            // Filter to only essay answers that need grading
            var pendingEssayAnswers = allAnswers
                .Where(a => essayQuestionIds.Contains(a.QuestionId) && a.IsCorrect == null)
                .ToList();

            if (!pendingEssayAnswers.Any())
            {
                return new List<EssayAnswerForGradingDto>();
            }

            // Get user IDs for batch fetch
            var userIds = submittedAttempts.Select(a => a.UserId).Distinct().ToList();
            var users = await _userRepository.GetByIdsAsync(userIds);
            var userMap = users.ToDictionary(u => u.UserId, u => u.FullName);

            // Create attempt lookup
            var attemptMap = submittedAttempts.ToDictionary(a => a.AttemptId);

            // Build result list
            var pendingAnswers = new List<EssayAnswerForGradingDto>();
            foreach (var answer in pendingEssayAnswers)
            {
                if (questionMap.TryGetValue(answer.QuestionId, out var question) &&
                    attemptMap.TryGetValue(answer.AttemptId, out var attempt))
                {
                    var studentName = userMap.GetValueOrDefault(attempt.UserId, "Unknown");

                    pendingAnswers.Add(new EssayAnswerForGradingDto
                    {
                        AttemptAnswerId = answer.AttemptAnswerId,
                        AttemptId = answer.AttemptId,
                        QuestionId = answer.QuestionId,
                        QuestionBody = question.Body,
                        QuestionPoints = question.Points,
                        StudentAnswer = answer.FreeText,
                        IsCorrect = answer.IsCorrect,
                        PointsAwarded = answer.PointsAwarded,
                        Feedback = answer.Feedback,
                        StudentName = studentName,
                        AnsweredAt = attempt.SubmittedAt ?? attempt.StartedAt
                    });
                }
            }

            return pendingAnswers.OrderBy(a => a.AnsweredAt).ToList();
        }

        public async Task<List<PendingEssayGradingDto>> GetAllPendingEssayAnswersForTeacherAsync(int teacherId)
        {
            // Get all courses taught by this teacher
            var courses = await _courseRepository.GetByInstructorIdAsync(teacherId);
            
            if (!courses.Any())
            {
                return new List<PendingEssayGradingDto>();
            }

            var pendingGradingList = new List<PendingEssayGradingDto>();

            // Batch fetch all quizzes for all courses at once
            var courseIds = courses.Select(c => c.CourseId).ToList();
            var allQuizzes = new List<Quiz>();
            foreach (var courseId in courseIds)
            {
                var quizzes = await _quizRepository.GetByCourseIdAsync(courseId);
                allQuizzes.AddRange(quizzes);
            }

            if (!allQuizzes.Any())
            {
                return new List<PendingEssayGradingDto>();
            }

            // Create course lookup
            var courseMap = courses.ToDictionary(c => c.CourseId);

            // Process each quiz using internal method (skips redundant auth checks)
            foreach (var quiz in allQuizzes)
            {
                var pendingAnswers = await GetPendingEssayAnswersForQuizInternalAsync(quiz.QuizId);

                if (pendingAnswers.Any())
                {
                    var course = courseMap.GetValueOrDefault(quiz.CourseId);
                    
                    pendingGradingList.Add(new PendingEssayGradingDto
                    {
                        QuizId = quiz.QuizId,
                        QuizTitle = quiz.Title,
                        CourseId = quiz.CourseId,
                        CourseName = course?.Name ?? "Unknown",
                        PendingCount = pendingAnswers.Count,
                        PendingAnswers = pendingAnswers
                    });
                }
            }

            return pendingGradingList.OrderByDescending(p => p.PendingCount).ToList();
        }

        public async Task<AttemptResponseDto> RecalculateAttemptScoreAsync(int attemptId, int teacherId)
        {
            // Fetch the attempt
            var attempt = await _attemptRepository.GetByIdAsync(attemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {attemptId} not found");
            }

            if (attempt.SubmittedAt == null)
            {
                throw new InvalidOperationException("Cannot recalculate score for unsubmitted attempts");
            }

            // Verify teacher authorization
            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {attempt.QuizId} not found");
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null || course.InstructorUserId != teacherId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can recalculate attempt scores");
            }

            // Recalculate score
            var answers = await _answerRepository.GetByAttemptIdAsync(attemptId);
            var questions = await _quizRepository.GetQuestionsByQuizIdAsync(attempt.QuizId);
            var questionMap = questions.ToDictionary(q => q.QuestionId);

            decimal earnedPoints = 0;
            decimal totalPoints = questions.Sum(q => q.Points);

            foreach (var answer in answers)
            {
                if (questionMap.TryGetValue(answer.QuestionId, out var question))
                {
                    // Check if partial credit was awarded
                    if (answer.PointsAwarded.HasValue)
                    {
                        // Use partial credit points (teacher specified exact points)
                        earnedPoints += answer.PointsAwarded.Value;
                    }
                    else if (answer.IsCorrect == true)
                    {
                        // Full credit (binary grading)
                        earnedPoints += question.Points;
                    }
                    // If IsCorrect is false or null, no points awarded
                }
            }

            // Calculate percentage score
            decimal score = totalPoints > 0 ? Math.Round((earnedPoints / totalPoints) * 100, 2) : 0;

            // Update attempt
            attempt.Score = score;
            var updatedAttempt = await _attemptRepository.UpdateAsync(attempt);

            var student = await _userRepository.GetByIdAsync(updatedAttempt.UserId);

            return new AttemptResponseDto
            {
                AttemptId = updatedAttempt.AttemptId,
                UserId = updatedAttempt.UserId,
                StudentName = student?.FullName,
                QuizId = updatedAttempt.QuizId,
                QuizTitle = quiz.Title,
                StartedAt = updatedAttempt.StartedAt,
                SubmittedAt = updatedAttempt.SubmittedAt,
                Score = updatedAttempt.Score,
                TimeSpentSeconds = updatedAttempt.TimeSpentSeconds
            };
        }
    }
}
