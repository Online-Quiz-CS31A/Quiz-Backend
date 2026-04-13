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
    public class AttemptServiceTests
    {
        // In-memory fakes tailored to the methods used by AttemptService in these tests
        private class InMemoryAttemptRepository : IAttemptRepository
        {
            private readonly Dictionary<int, Attempt> _store = new();
            private int _nextId = 1;

            public Attempt Seed(Attempt attempt)
            {
                attempt.AttemptId = _nextId++;
                _store[attempt.AttemptId] = Clone(attempt);
                return Clone(attempt);
            }

            public Task<Attempt> CreateAsync(Attempt attempt)
            {
                attempt.AttemptId = _nextId++;
                _store[attempt.AttemptId] = Clone(attempt);
                return Task.FromResult(Clone(attempt));
            }

            public Task<Attempt?> GetByIdAsync(int attemptId)
            {
                _store.TryGetValue(attemptId, out var a);
                return Task.FromResult(a != null ? Clone(a) : null);
            }

            public Task<List<Attempt>> GetByQuizIdAsync(int quizId)
                => Task.FromResult(_store.Values.Where(a => a.QuizId == quizId).Select(Clone).ToList());

            public Task<List<Attempt>> GetByStudentIdAsync(int studentId)
                => Task.FromResult(_store.Values.Where(a => a.UserId == studentId).Select(Clone).ToList());

            public Task<List<Attempt>> GetByQuizIdAndUserIdAsync(int quizId, int userId)
                => Task.FromResult(_store.Values.Where(a => a.QuizId == quizId && a.UserId == userId).Select(Clone).ToList());

            public Task<Attempt> UpdateAsync(Attempt attempt)
            {
                _store[attempt.AttemptId] = Clone(attempt);
                return Task.FromResult(Clone(attempt));
            }

            public Task<bool> DeleteAsync(int attemptId)
            {
                var existed = _store.Remove(attemptId);
                return Task.FromResult(existed);
            }

            public Task<double> GetAverageScoreByCourseAsync(int courseId) => Task.FromResult(0.0);
            public Task<List<Attempt>> GetRecentAttemptsByStudentAsync(int studentId, int count) => Task.FromResult(new List<Attempt>());
            public Task<Dictionary<int, double>> GetAverageScoresByCourseIdsAsync(List<int> courseIds) => Task.FromResult(new Dictionary<int, double>());
            public Task<List<Attempt>> GetByQuizIdsAndStudentIdAsync(List<int> quizIds, int studentId)
                => Task.FromResult(_store.Values.Where(a => quizIds.Contains(a.QuizId) && a.UserId == studentId).Select(Clone).ToList());
            public Task<int> BulkDeleteAsync(List<int> attemptIds)
            {
                int count = 0;
                foreach (var id in attemptIds)
                {
                    if (_store.Remove(id)) count++;
                }
                return Task.FromResult(count);
            }
            public Task<List<Attempt>> GetAllAttemptsForExportAsync(int? quizId, int? courseId) => Task.FromResult(new List<Attempt>());

            private static Attempt Clone(Attempt a) => new Attempt
            {
                AttemptId = a.AttemptId,
                QuizId = a.QuizId,
                UserId = a.UserId,
                StartedAt = a.StartedAt,
                SubmittedAt = a.SubmittedAt,
                Score = a.Score,
                TimeSpentSeconds = a.TimeSpentSeconds
            };
        }

        private class InMemoryAnswerRepository : IAttemptAnswerRepository
        {
            private readonly Dictionary<int, AttemptAnswer> _store = new();
            private int _nextId = 1;

            public AttemptAnswer Seed(AttemptAnswer ans)
            {
                ans.AttemptAnswerId = _nextId++;
                _store[ans.AttemptAnswerId] = Clone(ans);
                return Clone(ans);
            }

            public Task<AttemptAnswer> CreateAsync(AttemptAnswer answer)
            {
                answer.AttemptAnswerId = _nextId++;
                _store[answer.AttemptAnswerId] = Clone(answer);
                return Task.FromResult(Clone(answer));
            }

            public Task<AttemptAnswer?> GetByIdAsync(int answerId)
            {
                _store.TryGetValue(answerId, out var a);
                return Task.FromResult(a != null ? Clone(a) : null);
            }

            public Task<List<AttemptAnswer>> GetByAttemptIdAsync(int attemptId)
                => Task.FromResult(_store.Values.Where(a => a.AttemptId == attemptId).Select(Clone).ToList());

            public Task<AttemptAnswer> UpdateAsync(AttemptAnswer answer)
            {
                _store[answer.AttemptAnswerId] = Clone(answer);
                return Task.FromResult(Clone(answer));
            }

            public Task<bool> DeleteAsync(int answerId)
            {
                var existed = _store.Remove(answerId);
                return Task.FromResult(existed);
            }

            private static AttemptAnswer Clone(AttemptAnswer a) => new AttemptAnswer
            {
                AttemptAnswerId = a.AttemptAnswerId,
                AttemptId = a.AttemptId,
                QuestionId = a.QuestionId,
                ChoiceId = a.ChoiceId,
                FreeText = a.FreeText,
                IsCorrect = a.IsCorrect
            };
        }

        private class InMemoryQuizRepository : IQuizRepository
        {
            private readonly Dictionary<int, Quiz> _quizStore = new();
            private readonly Dictionary<int, List<Question>> _questions = new();
            private readonly Dictionary<int, List<Choice>> _choicesByQuestion = new();
            private int _nextQuizId = 1;
            private int _nextQuestionId = 1;
            private int _nextChoiceId = 1;

            public Quiz SeedQuiz(Quiz quiz)
            {
                quiz.QuizId = _nextQuizId++;
                _quizStore[quiz.QuizId] = Clone(quiz);
                return Clone(quiz);
            }

            public Question SeedQuestion(int quizId, Question question)
            {
                question.QuestionId = _nextQuestionId++;
                question.QuizId = quizId;
                if (!_questions.ContainsKey(quizId)) _questions[quizId] = new List<Question>();
                _questions[quizId].Add(Clone(question));
                return Clone(question);
            }

            public Choice SeedChoice(int questionId, Choice choice)
            {
                choice.ChoiceId = _nextChoiceId++;
                choice.QuestionId = questionId;
                if (!_choicesByQuestion.ContainsKey(questionId)) _choicesByQuestion[questionId] = new List<Choice>();
                _choicesByQuestion[questionId].Add(Clone(choice));
                return Clone(choice);
            }

            public Task<Quiz> CreateAsync(Quiz quiz)
            {
                return Task.FromResult(SeedQuiz(quiz));
            }

            public Task<Quiz?> GetByIdAsync(int quizId)
            {
                _quizStore.TryGetValue(quizId, out var q);
                return Task.FromResult(q != null ? Clone(q) : null);
            }

            public Task<List<Quiz>> GetByCourseIdAsync(int courseId)
                => Task.FromResult(_quizStore.Values.Where(q => q.CourseId == courseId).Select(Clone).ToList());

            public Task<Quiz> UpdateAsync(Quiz quiz)
            {
                _quizStore[quiz.QuizId] = Clone(quiz);
                return Task.FromResult(Clone(quiz));
            }

            public Task<bool> DeleteAsync(int quizId)
            {
                var existed = _quizStore.Remove(quizId);
                return Task.FromResult(existed);
            }

            public Task<Question> CreateQuestionAsync(Question question)
            {
                return Task.FromResult(SeedQuestion(question.QuizId, question));
            }

            public Task<Choice> CreateChoiceAsync(Choice choice)
            {
                return Task.FromResult(SeedChoice(choice.QuestionId, choice));
            }

            public Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId)
            {
                var list = _questions.ContainsKey(quizId) ? _questions[quizId].Select(Clone).ToList() : new List<Question>();
                return Task.FromResult(list);
            }

            public Task<List<Choice>> GetChoicesByQuestionIdAsync(int questionId)
            {
                var list = _choicesByQuestion.ContainsKey(questionId) ? _choicesByQuestion[questionId].Select(Clone).ToList() : new List<Choice>();
                return Task.FromResult(list);
            }

            public Task<List<Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds)
            {
                var result = new List<Choice>();
                foreach (var qid in questionIds)
                {
                    if (_choicesByQuestion.TryGetValue(qid, out var list))
                    {
                        result.AddRange(list.Select(Clone));
                    }
                }
                return Task.FromResult(result);
            }

            public Task<List<Quiz>> GetByIdsAsync(List<int> quizIds)
            {
                var items = _quizStore.Values.Where(q => quizIds.Contains(q.QuizId)).Select(Clone).ToList();
                return Task.FromResult(items);
            }

            public Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold) => Task.FromResult(new List<Quiz>());
            public Task<int> CountAsync() => Task.FromResult(_quizStore.Count);
            public Task<int> CountByCourseAsync(int courseId) => Task.FromResult(_quizStore.Values.Count(q => q.CourseId == courseId));
            public Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds) => Task.FromResult(_quizStore.Values.Where(q => courseIds.Contains(q.CourseId)).Select(Clone).ToList());
            public Task<int> CountByCourseIdsAsync(List<int> courseIds) => Task.FromResult(_quizStore.Values.Count(q => courseIds.Contains(q.CourseId)));
            public Task<int> BulkDeleteAsync(List<int> quizIds) => Task.FromResult(0);

            private static Quiz Clone(Quiz q) => new Quiz
            {
                QuizId = q.QuizId,
                CourseId = q.CourseId,
                Title = q.Title,
                DueAt = q.DueAt,
                TimeLimitMinutes = q.TimeLimitMinutes,
                IsPublished = q.IsPublished
            };

            private static Question Clone(Question q) => new Question
            {
                QuestionId = q.QuestionId,
                QuizId = q.QuizId,
                Body = q.Body,
                Type = q.Type,
                Points = q.Points
            };

            private static Choice Clone(Choice c) => new Choice
            {
                ChoiceId = c.ChoiceId,
                QuestionId = c.QuestionId,
                Body = c.Body,
                IsCorrect = c.IsCorrect
            };
        }

        private class InMemoryCourseRepository : ICourseRepository
        {
            private readonly Dictionary<int, Course> _store = new();
            private int _nextId = 1;

            public Course Seed(Course c)
            {
                c.CourseId = _nextId++;
                _store[c.CourseId] = Clone(c);
                return Clone(c);
            }

            public Task<Course> CreateAsync(Course course)
            {
                return Task.FromResult(Seed(course));
            }

            public Task<Course?> GetByIdAsync(int courseId)
            {
                _store.TryGetValue(courseId, out var c);
                return Task.FromResult(c != null ? Clone(c) : null);
            }

            public Task<List<Course>> GetAllAsync() => Task.FromResult(_store.Values.Select(Clone).ToList());
            public Task<Course> UpdateAsync(Course course) { _store[course.CourseId] = Clone(course); return Task.FromResult(Clone(course)); }
            public Task<bool> DeleteAsync(int courseId) => Task.FromResult(_store.Remove(courseId));
            public Task<List<Course>> GetByInstructorIdAsync(int instructorId) => Task.FromResult(_store.Values.Where(c => c.InstructorUserId == instructorId).Select(Clone).ToList());
            public Task<List<Course>> GetByStudentIdAsync(int studentId) => Task.FromResult(new List<Course>());
            public Task<int> CountAsync() => Task.FromResult(_store.Count);
            public Task<int> CountByInstructorAsync(int instructorId) => Task.FromResult(_store.Values.Count(c => c.InstructorUserId == instructorId));
            public Task<int> BulkDeleteAsync(List<int> courseIds) => Task.FromResult(0);

            private static Course Clone(Course c) => new Course
            {
                CourseId = c.CourseId,
                Code = c.Code,
                Name = c.Name,
                InstructorUserId = c.InstructorUserId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                Status = c.Status,
                Category = c.Category,
                Section = c.Section,
                CreatedBy = c.CreatedBy
            };
        }

        private class InMemoryEnrollmentRepository : IEnrollmentRepository
        {
            private readonly HashSet<(int studentId, int courseId)> _enrollments = new();

            public void Seed(int studentId, int courseId)
            {
                _enrollments.Add((studentId, courseId));
            }

            public Task<Enrollment> CreateAsync(Enrollment enrollment)
            {
                _enrollments.Add((enrollment.UserId, enrollment.CourseId));
                return Task.FromResult(enrollment);
            }

            public Task<bool> ExistsAsync(int studentId, int courseId)
                => Task.FromResult(_enrollments.Contains((studentId, courseId)));

            public Task<List<Enrollment>> GetByCourseIdAsync(int courseId)
                => Task.FromResult(_enrollments.Where(e => e.courseId == courseId).Select(e => new Enrollment { CourseId = e.courseId, UserId = e.studentId }).ToList());

            public Task<bool> DeleteAsync(int enrollmentId) => Task.FromResult(true);
            public Task<int> CountByCourseIdAsync(int courseId) => Task.FromResult(_enrollments.Count(e => e.courseId == courseId));
            public Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds)
                => Task.FromResult(courseIds.ToDictionary(id => id, id => _enrollments.Count(e => e.courseId == id)));
            public Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds) => Task.FromResult(0);
            public Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds)
            {
                var before = _enrollments.Count;
                _enrollments.RemoveWhere(e => e.courseId == courseId && studentIds.Contains(e.studentId));
                return Task.FromResult(before - _enrollments.Count);
            }
        }

        private class InMemoryUserRepository : IUserRepository
        {
            private readonly Dictionary<int, User> _store = new();
            public User Seed(User u)
            {
                _store[u.UserId] = Clone(u);
                return Clone(u);
            }
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
            private static User Clone(User u) => new User
            {
                UserId = u.UserId,
                Email = u.Email,
                PasswordHash = u.PasswordHash,
                FullName = u.FullName,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            };
        }

        private class InMemoryUserRoleRepository : IUserRoleRepository
        {
            public Task<UserRole> CreateAsync(UserRole userRole) => Task.FromResult(userRole);
            public Task<List<UserRole>> GetByUserIdAsync(int userId) => Task.FromResult(new List<UserRole>());
            public Task<List<UserRole>> GetAllAsync() => Task.FromResult(new List<UserRole>());
            public Task<bool> DeleteByUserIdAsync(int userId) => Task.FromResult(true);
            public Task<bool> IsAdminAsync(int userId) => Task.FromResult(false);
        }

        private AttemptService CreateService(
            InMemoryAttemptRepository? attempts = null,
            InMemoryQuizRepository? quizzes = null,
            InMemoryCourseRepository? courses = null,
            InMemoryEnrollmentRepository? enrollments = null,
            InMemoryUserRepository? users = null,
            InMemoryUserRoleRepository? roles = null,
            InMemoryAnswerRepository? answers = null)
        {
            return new AttemptService(
                attempts ?? new InMemoryAttemptRepository(),
                quizzes ?? new InMemoryQuizRepository(),
                courses ?? new InMemoryCourseRepository(),
                enrollments ?? new InMemoryEnrollmentRepository(),
                users ?? new InMemoryUserRepository(),
                roles ?? new InMemoryUserRoleRepository(),
                answers ?? new InMemoryAnswerRepository());
        }

        [Fact]
        public async Task StartAttempt_RequiresEnrollment()
        {
            var attempts = new InMemoryAttemptRepository();
            var quizzes = new InMemoryQuizRepository();
            var enrollments = new InMemoryEnrollmentRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Math", Code = "M101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Quiz 1", IsPublished = true });
            users.Seed(new User { UserId = 42, Email = "student@example.com", FullName = "Alice", PasswordHash = "x" });

            var service = CreateService(attempts, quizzes, courses, enrollments, users);

            var dto = new StartAttemptDto { QuizId = quiz.QuizId, StudentId = 42 };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.StartAttemptAsync(dto));
        }

        [Fact]
        public async Task StartAttempt_Succeeds_ForEnrolledStudent()
        {
            var attempts = new InMemoryAttemptRepository();
            var quizzes = new InMemoryQuizRepository();
            var enrollments = new InMemoryEnrollmentRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Math", Code = "M101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Quiz 1", IsPublished = true });
            users.Seed(new User { UserId = 42, Email = "student@example.com", FullName = "Alice", PasswordHash = "x" });
            enrollments.Seed(42, course.CourseId);

            var service = CreateService(attempts, quizzes, courses, enrollments, users);
            var dto = new StartAttemptDto { QuizId = quiz.QuizId, StudentId = 42 };
            var resp = await service.StartAttemptAsync(dto);

            Assert.Equal(42, resp.UserId);
            Assert.Equal(quiz.QuizId, resp.QuizId);
            Assert.Equal("Alice", resp.StudentName);
            Assert.NotEqual(default, resp.StartedAt);
        }

        [Fact]
        public async Task StartAttempt_Fails_OnUnpublishedQuiz()
        {
            var attempts = new InMemoryAttemptRepository();
            var quizzes = new InMemoryQuizRepository();
            var enrollments = new InMemoryEnrollmentRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Math", Code = "M101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Draft Quiz", IsPublished = false });
            users.Seed(new User { UserId = 42, Email = "student@example.com", FullName = "Alice", PasswordHash = "x" });
            enrollments.Seed(42, course.CourseId);

            var service = CreateService(attempts, quizzes, courses, enrollments, users);
            var dto = new StartAttemptDto { QuizId = quiz.QuizId, StudentId = 42 };
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAttemptAsync(dto));
        }

        [Fact]
        public async Task SubmitAttempt_CalculatesScore_SetsStatus_BlocksResubmission()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var quizzes = new InMemoryQuizRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Math", Code = "M101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Quiz 1", IsPublished = true, TimeLimitMinutes = 30 });
            var q1 = quizzes.SeedQuestion(quiz.QuizId, new Question { Body = "Q1", Type = "Single", Points = 5 });
            var c1a = quizzes.SeedChoice(q1.QuestionId, new Choice { Body = "A", IsCorrect = true });
            var c1b = quizzes.SeedChoice(q1.QuestionId, new Choice { Body = "B", IsCorrect = false });
            var q2 = quizzes.SeedQuestion(quiz.QuizId, new Question { Body = "Q2", Type = "Multiple", Points = 5 });
            var c2a = quizzes.SeedChoice(q2.QuestionId, new Choice { Body = "A", IsCorrect = false });
            var c2b = quizzes.SeedChoice(q2.QuestionId, new Choice { Body = "B", IsCorrect = true });

            users.Seed(new User { UserId = 42, Email = "student@example.com", FullName = "Alice", PasswordHash = "x" });

            var attempt = attempts.Seed(new Attempt { QuizId = quiz.QuizId, UserId = 42, StartedAt = DateTime.UtcNow });

            // Record answers: correct for Q1, incorrect for Q2
            answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = q1.QuestionId, ChoiceId = c1a.ChoiceId });
            answers.Seed(new AttemptAnswer { AttemptId = attempt.AttemptId, QuestionId = q2.QuestionId, ChoiceId = c2a.ChoiceId });

            var service = CreateService(attempts, quizzes, courses, new InMemoryEnrollmentRepository(), users, new InMemoryUserRoleRepository(), answers);

            var dto = new SubmitAttemptDto { TimeSpentSeconds = 120 };
            var resp = await service.SubmitAttemptAsync(attempt.AttemptId, dto, studentId: 42);

            Assert.Equal(42, resp.UserId);
            Assert.NotNull(resp.SubmittedAt);
            Assert.Equal(120, resp.TimeSpentSeconds);
            Assert.Equal(50m, resp.Score);

            // Resubmission should be blocked
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAttemptAsync(attempt.AttemptId, dto, studentId: 42));
        }

        [Fact]
        public async Task SubmitAttempt_Blocks_WhenDeadlineExceeded()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var quizzes = new InMemoryQuizRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Math", Code = "M101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Timed Quiz", IsPublished = true, TimeLimitMinutes = 1 });

            users.Seed(new User { UserId = 42, Email = "student@example.com", FullName = "Alice", PasswordHash = "x" });
            var attempt = attempts.Seed(new Attempt { QuizId = quiz.QuizId, UserId = 42, StartedAt = DateTime.UtcNow.AddMinutes(-5) });

            var service = CreateService(attempts, quizzes, courses, new InMemoryEnrollmentRepository(), users, new InMemoryUserRoleRepository(), answers);
            var dto = new SubmitAttemptDto { TimeSpentSeconds = 300 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAttemptAsync(attempt.AttemptId, dto, studentId: 42));
        }

        [Fact]
        public async Task SubmitAttempt_Allows_BeforeAndAtDeadline_WithBuffer()
        {
            var attempts = new InMemoryAttemptRepository();
            var answers = new InMemoryAnswerRepository();
            var quizzes = new InMemoryQuizRepository();
            var users = new InMemoryUserRepository();
            var courses = new InMemoryCourseRepository();

            var course = courses.Seed(new Course { InstructorUserId = 1, Name = "Physics", Code = "P101", Status = "Active", CreatedBy = 1 });
            var quiz = quizzes.SeedQuiz(new Quiz { CourseId = course.CourseId, Title = "Timed Quiz", IsPublished = true, TimeLimitMinutes = 1 });
            users.Seed(new User { UserId = 7, Email = "stud@example.com", FullName = "Bob", PasswordHash = "x" });

            // Started 1 minute ago (deadline = now; buffer allows for +2 minutes)
            var attempt1 = attempts.Seed(new Attempt { QuizId = quiz.QuizId, UserId = 7, StartedAt = DateTime.UtcNow.AddMinutes(-1) });
            var service = CreateService(attempts, quizzes, courses, new InMemoryEnrollmentRepository(), users, new InMemoryUserRoleRepository(), answers);
            var dto = new SubmitAttemptDto { TimeSpentSeconds = 10 };
            var resp1 = await service.SubmitAttemptAsync(attempt1.AttemptId, dto, studentId: 7);
            Assert.Equal(7, resp1.UserId);
            Assert.NotNull(resp1.SubmittedAt);
        }
    }
}
