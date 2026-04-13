using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using Xunit;

namespace OnlineQuiz.Tests.Services
{
    public class AnswerServiceTests
    {
        private class InMemoryAttemptRepository : IAttemptRepository
        {
            private readonly Dictionary<int, Attempt> _store = new();
            private int _nextId = 1;
            public Attempt Seed(Attempt a)
            {
                a.AttemptId = _nextId++;
                _store[a.AttemptId] = Clone(a);
                return Clone(a);
            }
            public Task<Attempt> CreateAsync(Attempt attempt) => Task.FromResult(Seed(attempt));
            public Task<Attempt?> GetByIdAsync(int attemptId) { _store.TryGetValue(attemptId, out var a); return Task.FromResult(a != null ? Clone(a) : null); }
            public Task<List<Attempt>> GetByQuizIdAsync(int quizId) => Task.FromResult(_store.Values.Where(a => a.QuizId == quizId).Select(Clone).ToList());
            public Task<List<Attempt>> GetByStudentIdAsync(int studentId) => Task.FromResult(_store.Values.Where(a => a.UserId == studentId).Select(Clone).ToList());
            public Task<List<Attempt>> GetByQuizIdAndUserIdAsync(int quizId, int userId) => Task.FromResult(_store.Values.Where(a => a.QuizId == quizId && a.UserId == userId).Select(Clone).ToList());
            public Task<Attempt> UpdateAsync(Attempt attempt) { _store[attempt.AttemptId] = Clone(attempt); return Task.FromResult(Clone(attempt)); }
            public Task<bool> DeleteAsync(int attemptId) => Task.FromResult(_store.Remove(attemptId));
            public Task<double> GetAverageScoreByCourseAsync(int courseId) => Task.FromResult(0.0);
            public Task<List<Attempt>> GetRecentAttemptsByStudentAsync(int studentId, int count) => Task.FromResult(new List<Attempt>());
            public Task<Dictionary<int, double>> GetAverageScoresByCourseIdsAsync(List<int> courseIds) => Task.FromResult(new Dictionary<int, double>());
            public Task<List<Attempt>> GetByQuizIdsAndStudentIdAsync(List<int> quizIds, int studentId) => Task.FromResult(_store.Values.Where(a => quizIds.Contains(a.QuizId) && a.UserId == studentId).Select(Clone).ToList());
            public Task<int> BulkDeleteAsync(List<int> attemptIds) => Task.FromResult(0);
            public Task<List<Attempt>> GetAllAttemptsForExportAsync(int? quizId, int? courseId) => Task.FromResult(new List<Attempt>());
            private static Attempt Clone(Attempt a) => new Attempt { AttemptId = a.AttemptId, QuizId = a.QuizId, UserId = a.UserId, StartedAt = a.StartedAt, SubmittedAt = a.SubmittedAt, Score = a.Score, TimeSpentSeconds = a.TimeSpentSeconds };
        }

        private class InMemoryAnswerRepository : IAttemptAnswerRepository
        {
            private readonly Dictionary<int, AttemptAnswer> _store = new();
            private int _nextId = 1;
            public AttemptAnswer Seed(AttemptAnswer a)
            {
                a.AttemptAnswerId = _nextId++;
                _store[a.AttemptAnswerId] = Clone(a);
                return Clone(a);
            }
            public Task<AttemptAnswer> CreateAsync(AttemptAnswer answer) { answer.AttemptAnswerId = _nextId++; _store[answer.AttemptAnswerId] = Clone(answer); return Task.FromResult(Clone(answer)); }
            public Task<AttemptAnswer?> GetByIdAsync(int answerId) { _store.TryGetValue(answerId, out var a); return Task.FromResult(a != null ? Clone(a) : null); }
            public Task<List<AttemptAnswer>> GetByAttemptIdAsync(int attemptId) => Task.FromResult(_store.Values.Where(a => a.AttemptId == attemptId).Select(Clone).ToList());
            public Task<AttemptAnswer> UpdateAsync(AttemptAnswer answer) { _store[answer.AttemptAnswerId] = Clone(answer); return Task.FromResult(Clone(answer)); }
            public Task<bool> DeleteAsync(int answerId) => Task.FromResult(_store.Remove(answerId));
            private static AttemptAnswer Clone(AttemptAnswer a) => new AttemptAnswer { AttemptAnswerId = a.AttemptAnswerId, AttemptId = a.AttemptId, QuestionId = a.QuestionId, ChoiceId = a.ChoiceId, FreeText = a.FreeText, IsCorrect = a.IsCorrect };
        }

        private class InMemoryQuizRepository : IQuizRepository
        {
            private readonly Dictionary<int, Quiz> _quizzes = new();
            private readonly Dictionary<int, List<Question>> _questions = new();
            private int _nextQuizId = 1;
            private int _nextQuestionId = 1;
            public Quiz SeedQuiz(Quiz quiz) { quiz.QuizId = _nextQuizId++; _quizzes[quiz.QuizId] = Clone(quiz); return Clone(quiz); }
            public Question SeedQuestion(int quizId, Question question) { question.QuestionId = _nextQuestionId++; question.QuizId = quizId; if (!_questions.ContainsKey(quizId)) _questions[quizId] = new List<Question>(); _questions[quizId].Add(Clone(question)); return Clone(question); }
            public Task<Quiz> CreateAsync(Quiz quiz) => Task.FromResult(SeedQuiz(quiz));
            public Task<Quiz?> GetByIdAsync(int quizId) { _quizzes.TryGetValue(quizId, out var q); return Task.FromResult(q != null ? Clone(q) : null); }
            public Task<List<Quiz>> GetByCourseIdAsync(int courseId) => Task.FromResult(_quizzes.Values.Where(q => q.CourseId == courseId).Select(Clone).ToList());
            public Task<Quiz> UpdateAsync(Quiz quiz) { _quizzes[quiz.QuizId] = Clone(quiz); return Task.FromResult(Clone(quiz)); }
            public Task<bool> DeleteAsync(int quizId) => Task.FromResult(_quizzes.Remove(quizId));
            public Task<Question> CreateQuestionAsync(Question question) => Task.FromResult(SeedQuestion(question.QuizId, question));
            public Task<OnlineQuiz.Models.Choice> CreateChoiceAsync(OnlineQuiz.Models.Choice choice) => Task.FromResult(choice);
            public Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId) { var list = _questions.ContainsKey(quizId) ? _questions[quizId].Select(Clone).ToList() : new List<Question>(); return Task.FromResult(list); }
            public Task<List<OnlineQuiz.Models.Choice>> GetChoicesByQuestionIdAsync(int questionId) => Task.FromResult(new List<OnlineQuiz.Models.Choice>());
            public Task<List<OnlineQuiz.Models.Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds) => Task.FromResult(new List<OnlineQuiz.Models.Choice>());
            public Task<List<Quiz>> GetByIdsAsync(List<int> quizIds) => Task.FromResult(_quizzes.Values.Where(q => quizIds.Contains(q.QuizId)).Select(Clone).ToList());
            public Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold) => Task.FromResult(new List<Quiz>());
            public Task<int> CountAsync() => Task.FromResult(_quizzes.Count);
            public Task<int> CountByCourseAsync(int courseId) => Task.FromResult(_quizzes.Values.Count(q => q.CourseId == courseId));
            public Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds) => Task.FromResult(_quizzes.Values.Where(q => courseIds.Contains(q.CourseId)).Select(Clone).ToList());
            public Task<int> CountByCourseIdsAsync(List<int> courseIds) => Task.FromResult(_quizzes.Values.Count(q => courseIds.Contains(q.CourseId)));
            public Task<int> BulkDeleteAsync(List<int> quizIds) => Task.FromResult(0);
            private static Quiz Clone(Quiz q) => new Quiz { QuizId = q.QuizId, CourseId = q.CourseId, Title = q.Title, DueAt = q.DueAt, TimeLimitMinutes = q.TimeLimitMinutes, IsPublished = q.IsPublished };
            private static Question Clone(Question q) => new Question { QuestionId = q.QuestionId, QuizId = q.QuizId, Body = q.Body, Type = q.Type, Points = q.Points };
        }

        private class InMemoryUserRepository : IUserRepository
        {
            private readonly Dictionary<int, User> _store = new();
            public User Seed(User u) { _store[u.UserId] = Clone(u); return Clone(u); }
            public Task<User> CreateAsync(User user) { _store[user.UserId] = Clone(user); return Task.FromResult(Clone(user)); }
            public Task<User?> GetByIdAsync(int userId) { _store.TryGetValue(userId, out var u); return Task.FromResult(u != null ? Clone(u) : null); }
            public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
            public Task<List<User>> GetAllAsync() => Task.FromResult(_store.Values.Select(Clone).ToList());
            public Task<List<User>> GetByIdsAsync(List<int> userIds) => Task.FromResult(_store.Values.Where(u => userIds.Contains(u.UserId)).Select(Clone).ToList());
            public Task<int> CountAsync() => Task.FromResult(_store.Count);
            public Task<int> CountByRoleAsync(int roleId) => Task.FromResult(0);
            public Task<List<User>> GetRecentRegistrationsAsync(int days) => Task.FromResult(new List<User>());
            public Task<User> UpdateAsync(User user) { _store[user.UserId] = Clone(user); return Task.FromResult(Clone(user)); }
            public Task<bool> DeleteAsync(int userId) => Task.FromResult(_store.Remove(userId));
            public Task<int> BulkDeleteAsync(List<int> userIds) => Task.FromResult(0);
            private static User Clone(User u) => new User { UserId = u.UserId, Email = u.Email, FullName = u.FullName, PasswordHash = u.PasswordHash, CreatedAt = u.CreatedAt, UpdatedAt = u.UpdatedAt };
        }

        private AnswerService CreateService(
            InMemoryAttemptRepository? attempts = null,
            InMemoryAnswerRepository? answers = null,
            InMemoryQuizRepository? quizzes = null,
            InMemoryUserRepository? users = null)
        {
            return new AnswerService(
                attempts ?? new InMemoryAttemptRepository(),
                answers ?? new InMemoryAnswerRepository(),
                quizzes ?? new InMemoryQuizRepository(),
                users ?? new InMemoryUserRepository());
        }

        [Fact]
        public async Task RecordAnswer_OnlyOwnerCanRecord()
        {
            var attempts = new InMemoryAttemptRepository();
            var users = new InMemoryUserRepository();
            users.Seed(new User { UserId = 1, FullName = "Alice", Email = "a@a", PasswordHash = "x" });
            var attempt = attempts.Seed(new Attempt { AttemptId = 0, QuizId = 10, UserId = 1, StartedAt = DateTime.UtcNow });

            var service = CreateService(attempts);
            var dto = new CreateAnswerDto { AttemptId = attempt.AttemptId, QuestionId = 100, ChoiceId = 200 };

            // Different student should be unauthorized
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RecordAnswerAsync(dto, studentId: 2));
        }

        [Fact]
        public async Task RecordAnswer_Blocks_WhenAttemptSubmitted()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var service = CreateService(attempts, answers);

            var attempt = attempts.Seed(new Attempt { QuizId = 10, UserId = 3, StartedAt = DateTime.UtcNow, SubmittedAt = DateTime.UtcNow });
            var dto = new CreateAnswerDto { AttemptId = attempt.AttemptId, QuestionId = 101, ChoiceId = 201 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAnswerAsync(dto, studentId: 3));
        }

        [Fact]
        public async Task UpdateAnswer_OnlyOwner_Unsubmitted()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var service = CreateService(attempts, answers);

            var attempt = attempts.Seed(new Attempt { QuizId = 10, UserId = 5, StartedAt = DateTime.UtcNow });
            var created = answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = 200, ChoiceId = 300, FreeText = null });

            var updatedDto = new CreateAnswerDto { AttemptId = attempt.AttemptId, QuestionId = 200, ChoiceId = 301, TextAnswer = "hi" };

            // Wrong student cannot update
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAnswerAsync(created.AttemptAnswerId, updatedDto, studentId: 99));

            // Owner can update when not submitted
            var resp = await service.UpdateAnswerAsync(created.AttemptAnswerId, updatedDto, studentId: 5);
            Assert.Equal(301, resp.ChoiceId);
            Assert.Equal("hi", resp.TextAnswer);
        }

        [Fact]
        public async Task DeleteAnswer_OnlyOwner_Unsubmitted()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var service = CreateService(attempts, answers);

            var attempt = attempts.Seed(new Attempt { QuizId = 10, UserId = 5, StartedAt = DateTime.UtcNow });
            var created = answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = 200, ChoiceId = 300 });

            // Wrong student cannot delete
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAnswerAsync(created.AttemptAnswerId, studentId: 99));

            // Owner can delete
            var ok = await service.DeleteAnswerAsync(created.AttemptAnswerId, studentId: 5);
            Assert.True(ok);
        }

        [Fact]
        public async Task GetAnswersForAttempt_ReturnsMappedAnswers()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var quizzes = new InMemoryQuizRepository();
            var users = new InMemoryUserRepository();

            users.Seed(new User { UserId = 11, FullName = "Jane", Email = "j@j", PasswordHash = "x" });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = 1, Title = "Quiz Title", IsPublished = true });
            var attempt = attempts.Seed(new Attempt { QuizId = quiz.QuizId, UserId = 11, StartedAt = DateTime.UtcNow });
            answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = 100, ChoiceId = 200, FreeText = null });

            var service = CreateService(attempts, answers, quizzes, users);
            var dto = await service.GetAnswersForAttemptAsync(attempt.AttemptId, userId: 11);

            Assert.Equal(attempt.AttemptId, dto.AttemptId);
            Assert.Equal("Jane", dto.StudentName);
            Assert.Equal("Quiz Title", dto.QuizTitle);
            Assert.Single(dto.Answers);
            Assert.Equal(200, dto.Answers[0].ChoiceId);
        }

        [Fact]
        public async Task RecordBulkAnswers_UpsertsAnswers()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var service = CreateService(attempts, answers);

            var attempt = attempts.Seed(new Attempt { QuizId = 10, UserId = 22, StartedAt = DateTime.UtcNow });
            // Existing answer for Q1
            var existing = answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = 1, ChoiceId = 10, FreeText = null });

            var bulk = new BulkAnswerRequestDto
            {
                AttemptId = attempt.AttemptId,
                Answers = new List<AnswerSubmissionDto>
                {
                    new AnswerSubmissionDto { QuestionId = 1, ChoiceId = 11 }, // update existing
                    new AnswerSubmissionDto { QuestionId = 2, TextAnswer = "free" } // create new
                }
            };

            var result = await service.RecordBulkAnswersAsync(bulk, studentId: 22);

            Assert.Equal(2, result.Count);
            // Verify update
            var updated = await answers.GetByIdAsync(existing.AttemptAnswerId);
            Assert.Equal(11, updated!.ChoiceId);
            // Verify new creation exists
            var created = (await answers.GetByAttemptIdAsync(attempt.AttemptId)).FirstOrDefault(a => a.QuestionId == 2);
            Assert.NotNull(created);
            Assert.Equal("free", created!.FreeText);
        }
    }
}
