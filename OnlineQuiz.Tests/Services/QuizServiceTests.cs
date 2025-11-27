using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using OnlineQuiz.Utilities;
using Xunit;

namespace OnlineQuiz.Tests.Services
{
    // In-memory fakes for QuizService
    public class FakeQuizRepositoryForQuizServiceTests : IQuizRepository
    {
        private int _nextQuizId = 1;
        private int _nextQuestionId = 1;
        private int _nextChoiceId = 1;
        private readonly Dictionary<int, Quiz> _quizzes = new();
        private readonly Dictionary<int, List<Question>> _questionsByQuiz = new();
        private readonly Dictionary<int, List<Choice>> _choicesByQuestion = new();

        public Task<Quiz> CreateAsync(Quiz quiz)
        {
            if (quiz.QuizId == 0) quiz.QuizId = _nextQuizId++;
            _quizzes[quiz.QuizId] = quiz;
            return Task.FromResult(quiz);
        }

        public Task<Quiz?> GetByIdAsync(int quizId)
        {
            _quizzes.TryGetValue(quizId, out var q);
            return Task.FromResult(q);
        }

        public Task<List<Quiz>> GetByCourseIdAsync(int courseId)
        {
            return Task.FromResult(_quizzes.Values.Where(q => q.CourseId == courseId).OrderBy(q => q.QuizId).ToList());
        }

        public Task<Quiz> UpdateAsync(Quiz quiz)
        {
            _quizzes[quiz.QuizId] = quiz;
            return Task.FromResult(quiz);
        }

        public Task<bool> DeleteAsync(int quizId)
        {
            var removed = _quizzes.Remove(quizId);
            return Task.FromResult(removed);
        }

        public Task<Question> CreateQuestionAsync(Question question)
        {
            if (question.QuestionId == 0) question.QuestionId = _nextQuestionId++;
            if (!_questionsByQuiz.TryGetValue(question.QuizId, out var list))
            {
                list = new List<Question>();
                _questionsByQuiz[question.QuizId] = list;
            }
            list.Add(question);
            return Task.FromResult(question);
        }

        public Task<Choice> CreateChoiceAsync(Choice choice)
        {
            if (choice.ChoiceId == 0) choice.ChoiceId = _nextChoiceId++;
            if (!_choicesByQuestion.TryGetValue(choice.QuestionId, out var list))
            {
                list = new List<Choice>();
                _choicesByQuestion[choice.QuestionId] = list;
            }
            list.Add(choice);
            return Task.FromResult(choice);
        }

        public Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId)
        {
            _questionsByQuiz.TryGetValue(quizId, out var list);
            list ??= new List<Question>();
            return Task.FromResult(list.OrderBy(q => q.SortOrder).ThenBy(q => q.QuestionId).ToList());
        }

        public Task<List<Choice>> GetChoicesByQuestionIdAsync(int questionId)
        {
            _choicesByQuestion.TryGetValue(questionId, out var list);
            list ??= new List<Choice>();
            return Task.FromResult(list.ToList());
        }

        public Task<List<Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds)
        {
            var result = new List<Choice>();
            foreach (var qid in questionIds)
            {
                if (_choicesByQuestion.TryGetValue(qid, out var list))
                {
                    result.AddRange(list);
                }
            }
            return Task.FromResult(result);
        }

        public Task<List<Quiz>> GetByIdsAsync(List<int> quizIds)
        {
            var result = quizIds.Select(id => _quizzes.TryGetValue(id, out var q) ? q : null)
                .Where(q => q != null).Cast<Quiz>().ToList();
            return Task.FromResult(result);
        }

        public Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold)
        {
            var result = _quizzes.Values.Where(q => q.DueAt.HasValue && q.DueAt.Value <= threshold).ToList();
            return Task.FromResult(result);
        }

        public Task<int> CountAsync() => Task.FromResult(_quizzes.Count);

        public Task<int> CountByCourseAsync(int courseId) => Task.FromResult(_quizzes.Values.Count(q => q.CourseId == courseId));

        public Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds)
        {
            return Task.FromResult(_quizzes.Values.Where(q => courseIds.Contains(q.CourseId)).ToList());
        }

        public Task<int> CountByCourseIdsAsync(List<int> courseIds)
        {
            var count = _quizzes.Values.Count(q => courseIds.Contains(q.CourseId));
            return Task.FromResult(count);
        }

        public Task<int> BulkDeleteAsync(List<int> quizIds)
        {
            int c = 0;
            foreach (var id in quizIds)
            {
                if (_quizzes.Remove(id)) c++;
            }
            return Task.FromResult(c);
        }
    }

    public class FakeCourseRepositoryForQuizServiceTests : ICourseRepository
    {
        private readonly Dictionary<int, Course> _courses = new();
        public void Seed(IEnumerable<Course> courses) { foreach (var c in courses) _courses[c.CourseId] = c; }
        public Task<Course> CreateAsync(Course course) { _courses[course.CourseId] = course; return Task.FromResult(course); }
        public Task<Course?> GetByIdAsync(int courseId) { _courses.TryGetValue(courseId, out var c); return Task.FromResult(c); }
        public Task<List<Course>> GetAllAsync() { return Task.FromResult(_courses.Values.ToList()); }
        public Task<Course> UpdateAsync(Course course) { _courses[course.CourseId] = course; return Task.FromResult(course); }
        public Task<bool> DeleteAsync(int courseId) { return Task.FromResult(_courses.Remove(courseId)); }
        public Task<List<Course>> GetByInstructorIdAsync(int instructorId) { return Task.FromResult(_courses.Values.Where(c => c.InstructorUserId == instructorId).ToList()); }
        public Task<List<Course>> GetByStudentIdAsync(int studentId) { return Task.FromResult(_courses.Values.ToList()); }
        public Task<int> CountAsync() { return Task.FromResult(_courses.Count); }
        public Task<int> CountByInstructorAsync(int instructorId) { return Task.FromResult(_courses.Values.Count(c => c.InstructorUserId == instructorId)); }
        public Task<int> BulkDeleteAsync(List<int> courseIds) { int c = 0; foreach (var id in courseIds) if (_courses.Remove(id)) c++; return Task.FromResult(c); }
    }

    public class FakeEnrollmentRepositoryForQuizServiceTests : IEnrollmentRepository
    {
        private readonly HashSet<(int userId, int courseId)> _enrollments = new();
        private readonly List<Enrollment> _enrollmentList = new();
        public void Seed(IEnumerable<Enrollment> enrollments) { foreach (var e in enrollments) { _enrollments.Add((e.UserId, e.CourseId)); _enrollmentList.Add(e); } }
        public Task<Enrollment> CreateAsync(Enrollment enrollment) { _enrollments.Add((enrollment.UserId, enrollment.CourseId)); _enrollmentList.Add(enrollment); return Task.FromResult(enrollment); }
        public Task<bool> ExistsAsync(int studentId, int courseId) { return Task.FromResult(_enrollments.Contains((studentId, courseId))); }
        public Task<List<Enrollment>> GetByCourseIdAsync(int courseId) { return Task.FromResult(_enrollmentList.Where(e => e.CourseId == courseId).ToList()); }
        public Task<bool> DeleteAsync(int enrollmentId) { var e = _enrollmentList.FirstOrDefault(x => x.EnrollmentId == enrollmentId); if (e != null) { _enrollmentList.Remove(e); _enrollments.Remove((e.UserId, e.CourseId)); return Task.FromResult(true); } return Task.FromResult(false); }
        public Task<int> CountByCourseIdAsync(int courseId) { return Task.FromResult(_enrollmentList.Count(e => e.CourseId == courseId)); }
        public Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds) { var d = new Dictionary<int, int>(); foreach (var id in courseIds) d[id] = _enrollmentList.Count(e => e.CourseId == id); return Task.FromResult(d); }
        public Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds) { int c = 0; foreach (var id in enrollmentIds) { var e = _enrollmentList.FirstOrDefault(x => x.EnrollmentId == id); if (e != null) { _enrollmentList.Remove(e); _enrollments.Remove((e.UserId, e.CourseId)); c++; } } return Task.FromResult(c); }
        public Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds) { int c = 0; foreach (var s in studentIds) { var e = _enrollmentList.FirstOrDefault(x => x.CourseId == courseId && x.UserId == s); if (e != null) { _enrollmentList.Remove(e); _enrollments.Remove((e.UserId, e.CourseId)); c++; } } return Task.FromResult(c); }
    }

    public class FakeUserRepositoryForQuizServiceTests : IUserRepository
    {
        private readonly Dictionary<int, User> _users = new();
        public void Seed(IEnumerable<User> users) { foreach (var u in users) _users[u.UserId] = u; }
        public Task<User> CreateAsync(User user) { _users[user.UserId] = user; return Task.FromResult(user); }
        public Task<User?> GetByIdAsync(int userId) { _users.TryGetValue(userId, out var u); return Task.FromResult(u); }
        public Task<User?> GetByEmailAsync(string email) { return Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email)); }
        public Task<List<User>> GetAllAsync() { return Task.FromResult(_users.Values.ToList()); }
        public Task<List<User>> GetByIdsAsync(List<int> userIds) { return Task.FromResult(userIds.Select(id => _users.TryGetValue(id, out var u) ? u : null).Where(u => u != null).Cast<User>().ToList()); }
        public Task<int> CountAsync() { return Task.FromResult(_users.Count); }
        public Task<int> CountByRoleAsync(int roleId) { return Task.FromResult(0); }
        public Task<List<User>> GetRecentRegistrationsAsync(int days) { return Task.FromResult(_users.Values.ToList()); }
        public Task<User> UpdateAsync(User user) { _users[user.UserId] = user; return Task.FromResult(user); }
        public Task<bool> DeleteAsync(int userId) { return Task.FromResult(_users.Remove(userId)); }
        public Task<int> BulkDeleteAsync(List<int> userIds) { int c = 0; foreach (var id in userIds) if (_users.Remove(id)) c++; return Task.FromResult(c); }
    }

    public class FakeUserRoleRepositoryForQuizServiceTests : IUserRoleRepository
    {
        private readonly List<UserRole> _roles = new();
        public void Seed(IEnumerable<UserRole> roles) { _roles.AddRange(roles); }
        public Task<UserRole> CreateAsync(UserRole userRole) { _roles.Add(userRole); return Task.FromResult(userRole); }
        public Task<List<UserRole>> GetByUserIdAsync(int userId) { return Task.FromResult(_roles.Where(r => r.UserId == userId).ToList()); }
        public Task<List<UserRole>> GetAllAsync() { return Task.FromResult(_roles.ToList()); }
        public Task<bool> DeleteByUserIdAsync(int userId) { var countBefore = _roles.Count; _roles.RemoveAll(r => r.UserId == userId); return Task.FromResult(_roles.Count < countBefore); }
        public Task<bool> IsAdminAsync(int userId) { var isAdmin = _roles.Any(r => r.UserId == userId && r.RoleId == RoleConstants.Admin); return Task.FromResult(isAdmin); }
    }

    public class FakeNotificationServiceForQuizServiceTests : INotificationService
    {
        public readonly List<(int quizId, int courseId, string title)> Notifications = new();
        public Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationDto createNotificationDto, int createdBy) { return Task.FromResult(new NotificationResponseDto()); }
        public Task<NotificationResponseDto?> GetNotificationByIdAsync(int notificationId, int userId) { return Task.FromResult<NotificationResponseDto?>(null); }
        public Task<List<NotificationResponseDto>> GetNotificationsForUserAsync(int userId) { return Task.FromResult(new List<NotificationResponseDto>()); }
        public Task<NotificationResponseDto> MarkAsReadAsync(int notificationId, int userId) { return Task.FromResult(new NotificationResponseDto()); }
        public Task<bool> DeleteNotificationAsync(int notificationId, int userId) { return Task.FromResult(true); }
        public Task<bool> MarkAllAsReadAsync(int userId) { return Task.FromResult(true); }
        public Task NotifyStudentsOfNewQuizAsync(int quizId, int courseId, string quizTitle) { Notifications.Add((quizId, courseId, quizTitle)); return Task.CompletedTask; }
        public Task<int> BulkDeleteNotificationsAsync(List<int> notificationIds, int userId) { return Task.FromResult(0); }
    }

    public class QuizServiceTests
    {
        private QuizService CreateService(
            FakeQuizRepositoryForQuizServiceTests? quizRepo = null,
            FakeCourseRepositoryForQuizServiceTests? courseRepo = null,
            FakeEnrollmentRepositoryForQuizServiceTests? enrollmentRepo = null,
            FakeUserRepositoryForQuizServiceTests? userRepo = null,
            FakeUserRoleRepositoryForQuizServiceTests? userRoleRepo = null,
            FakeNotificationServiceForQuizServiceTests? notifSvc = null)
        {
            quizRepo ??= new FakeQuizRepositoryForQuizServiceTests();
            courseRepo ??= new FakeCourseRepositoryForQuizServiceTests();
            enrollmentRepo ??= new FakeEnrollmentRepositoryForQuizServiceTests();
            userRepo ??= new FakeUserRepositoryForQuizServiceTests();
            userRoleRepo ??= new FakeUserRoleRepositoryForQuizServiceTests();
            notifSvc ??= new FakeNotificationServiceForQuizServiceTests();
            return new QuizService(quizRepo, courseRepo, enrollmentRepo, userRepo, userRoleRepo, notifSvc);
        }

        [Fact]
        public async Task CreateQuizAsync_Published_NotifiesAndCreatesQuestionsChoices()
        {
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            courseRepo.Seed(new[] { new Course { CourseId = 1, Name = "Physics", InstructorUserId = 50 } });

            var notif = new FakeNotificationServiceForQuizServiceTests();
            var svc = CreateService(courseRepo: courseRepo, notifSvc: notif);

            var createDto = new CreateQuizDto
            {
                CourseId = 1,
                Title = "Quiz 1",
                DueAt = DateTime.UtcNow.AddDays(2),
                TimeLimitMinutes = 30,
                IsPublished = true,
                CreatedBy = 50,
                Questions = new List<CreateQuestionDto>
                {
                    new CreateQuestionDto { QuizId = 0, Type = "Single", Body = "Q1", Points = 2m, SortOrder = 1,
                        Choices = new List<CreateChoiceDto>{ new CreateChoiceDto{ Body = "A", IsCorrect = true }, new CreateChoiceDto{ Body = "B", IsCorrect = false } } },
                    new CreateQuestionDto { QuizId = 0, Type = "Multiple", Body = "Q2", Points = 3m, SortOrder = 2,
                        Choices = new List<CreateChoiceDto>{ new CreateChoiceDto{ Body = "C", IsCorrect = false }, new CreateChoiceDto{ Body = "D", IsCorrect = true } } }
                }
            };

            var response = await svc.CreateQuizAsync(createDto);

            Assert.Equal("Quiz 1", response.Title);
            Assert.Equal(1, response.CourseId);
            Assert.True(response.Questions.Count == 2);
            Assert.All(response.Questions, q => Assert.True(q.Choices.Count >= 2));
            Assert.Single(notif.Notifications);
            var n = notif.Notifications[0];
            Assert.Equal(response.QuizId, n.quizId);
            Assert.Equal(1, n.courseId);
            Assert.Equal("Quiz 1", n.title);
        }

        [Fact]
        public async Task GetQuizzesForCourseAsync_StudentNotEnrolled_Throws()
        {
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var svc = CreateService(courseRepo: courseRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetQuizzesForCourseAsync(1, userId: 99, isStudent: true));
        }

        [Fact]
        public async Task GetQuizzesForCourseAsync_StudentSeesOnlyPublished()
        {
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var enrollRepo = new FakeEnrollmentRepositoryForQuizServiceTests();
            enrollRepo.Seed(new[] { new Enrollment { CourseId = 1, UserId = 99 } });
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q-Pub", IsPublished = true, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q-Private", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, enrollmentRepo: enrollRepo);
            var list = await svc.GetQuizzesForCourseAsync(1, userId: 99, isStudent: true);
            Assert.Single(list);
            Assert.Equal("Q-Pub", list[0].Title);
        }

        [Fact]
        public async Task GetQuizzesForCourseAsync_TeacherNotInstructor_Throws()
        {
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var svc = CreateService(courseRepo: courseRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetQuizzesForCourseAsync(1, userId: 77, isStudent: false));
        }

        [Fact]
        public async Task GetQuizByIdAsync_ReturnsQuestionsAndChoices()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var created = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q", IsPublished = true, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var q1 = await quizRepo.CreateQuestionAsync(new Question { QuizId = created.QuizId, Type = "Single", Body = "Q1", Points = 1m, SortOrder = 1 });
            await quizRepo.CreateChoiceAsync(new Choice { QuestionId = q1.QuestionId, Body = "A", IsCorrect = true });
            await quizRepo.CreateChoiceAsync(new Choice { QuestionId = q1.QuestionId, Body = "B", IsCorrect = false });

            var svc = CreateService(quizRepo: quizRepo, courseRepo: new FakeCourseRepositoryForQuizServiceTests());
            var dto = await svc.GetQuizByIdAsync(created.QuizId);
            Assert.NotNull(dto);
            Assert.Equal("Q", dto!.Title);
            Assert.Single(dto.Questions);
            Assert.Collection(dto.Questions[0].Choices, _ => { }, _ => { });
        }

        [Fact]
        public async Task UpdateQuizAsync_Unauthorized_WhenNotInstructor()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var created = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.UpdateQuizAsync(created.QuizId, new UpdateQuizDto { Title = "New" }, userId: 77));
        }

        [Fact]
        public async Task UpdateQuizAsync_UpdatesFields_AddsQuestion_Publishes_Notifies()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var notif = new FakeNotificationServiceForQuizServiceTests();
            var created = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, notifSvc: notif);

            var update = new UpdateQuizDto
            {
                Title = "New Title",
                IsPublished = true,
                Questions = new List<UpdateQuestionDto>
                {
                    new UpdateQuestionDto { Body = "Added Q", Points = 2m, SortOrder = 10, Type = "Single",
                        Choices = new List<UpdateChoiceDto> { new UpdateChoiceDto{ Body = "A", IsCorrect = true } } }
                }
            };

            var updated = await svc.UpdateQuizAsync(created.QuizId, update, userId: 50);
            Assert.Equal("New Title", updated.Title);
            Assert.True(updated.IsPublished);
            Assert.True(updated.Questions.Count >= 1);
            Assert.Single(notif.Notifications);
            Assert.Equal(updated.QuizId, notif.Notifications[0].quizId);
        }

        [Fact]
        public async Task DeleteQuizAsync_Unauthorized_ForNonInstructorNonAdmin()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var userRoleRepo = new FakeUserRoleRepositoryForQuizServiceTests();
            var created = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 } });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, userRoleRepo: userRoleRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteQuizAsync(created.QuizId, userId: 77));
        }

        [Fact]
        public async Task DeleteQuizAsync_AdminCanDelete()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var userRoleRepo = new FakeUserRoleRepositoryForQuizServiceTests();
            var created = await quizRepo.CreateAsync(new Quiz { CourseId = 2, Title = "Q", IsPublished = false, CreatedBy = 60, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            courseRepo.Seed(new[] { new Course { CourseId = 2, InstructorUserId = 60 } });
            userRoleRepo.Seed(new[] { new UserRole { UserId = 77, RoleId = RoleConstants.Admin } });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, userRoleRepo: userRoleRepo);
            var ok = await svc.DeleteQuizAsync(created.QuizId, userId: 77);
            Assert.True(ok);
        }

        [Fact]
        public async Task BulkDeleteQuizzesAsync_TeacherNotOwningAll_Throws()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var userRoleRepo = new FakeUserRoleRepositoryForQuizServiceTests();
            // Teacher 50 owns course 1, not course 2
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 }, new Course { CourseId = 2, InstructorUserId = 60 } });
            var q1 = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q1", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var q2 = await quizRepo.CreateAsync(new Quiz { CourseId = 2, Title = "Q2", IsPublished = false, CreatedBy = 60, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, userRoleRepo: userRoleRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.BulkDeleteQuizzesAsync(new List<int> { q1.QuizId, q2.QuizId }, userId: 50));
        }

        [Fact]
        public async Task BulkDeleteQuizzesAsync_AdminSucceeds()
        {
            var quizRepo = new FakeQuizRepositoryForQuizServiceTests();
            var courseRepo = new FakeCourseRepositoryForQuizServiceTests();
            var userRoleRepo = new FakeUserRoleRepositoryForQuizServiceTests();
            courseRepo.Seed(new[] { new Course { CourseId = 1, InstructorUserId = 50 }, new Course { CourseId = 2, InstructorUserId = 60 } });
            var q1 = await quizRepo.CreateAsync(new Quiz { CourseId = 1, Title = "Q1", IsPublished = false, CreatedBy = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var q2 = await quizRepo.CreateAsync(new Quiz { CourseId = 2, Title = "Q2", IsPublished = false, CreatedBy = 60, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            userRoleRepo.Seed(new[] { new UserRole { UserId = 77, RoleId = RoleConstants.Admin } });
            var svc = CreateService(quizRepo: quizRepo, courseRepo: courseRepo, userRoleRepo: userRoleRepo);
            var deleted = await svc.BulkDeleteQuizzesAsync(new List<int> { q1.QuizId, q2.QuizId }, userId: 77);
            Assert.Equal(2, deleted);
        }
    }
}
