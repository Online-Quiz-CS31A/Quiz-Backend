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
    // Fakes for AnalyticsService
    public class FakeUserRepositoryForAnalyticsServiceTests : IUserRepository
    {
        private readonly Dictionary<int, User> _users = new();

        public void Seed(IEnumerable<User> users)
        {
            foreach (var u in users)
            {
                _users[u.UserId] = u;
            }
        }

        public Task<int> CountAsync() => Task.FromResult(_users.Count);
        public Task<List<User>> GetRecentRegistrationsAsync(int days)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return Task.FromResult(_users.Values.Where(u => u.CreatedAt >= since).ToList());
        }

        // Unused methods implemented minimally
        public Task<User> CreateAsync(User user) { _users[user.UserId] = user; return Task.FromResult(user); }
        public Task<User?> GetByIdAsync(int userId) { _users.TryGetValue(userId, out var u); return Task.FromResult(u); }
        public Task<User?> GetByEmailAsync(string email) { return Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email)); }
        public Task<List<User>> GetAllAsync() { return Task.FromResult(_users.Values.ToList()); }
        public Task<List<User>> GetByIdsAsync(List<int> userIds) { return Task.FromResult(userIds.Select(id => _users.TryGetValue(id, out var u) ? u : null).Where(u => u != null).Cast<User>().ToList()); }
        public Task<int> CountByRoleAsync(int roleId) { return Task.FromResult(0); }
        public Task<User> UpdateAsync(User user) { _users[user.UserId] = user; return Task.FromResult(user); }
        public Task<bool> DeleteAsync(int userId) { return Task.FromResult(_users.Remove(userId)); }
        public Task<int> BulkDeleteAsync(List<int> userIds) { int c=0; foreach(var id in userIds){ if (_users.Remove(id)) c++; } return Task.FromResult(c); }
    }

    public class FakeCourseRepositoryForAnalyticsServiceTests : ICourseRepository
    {
        private readonly Dictionary<int, Course> _courses = new();
        public void Seed(IEnumerable<Course> courses){ foreach(var c in courses){ _courses[c.CourseId] = c; } }
        public Task<int> CountAsync() => Task.FromResult(_courses.Count);
        public Task<Course?> GetByIdAsync(int courseId){ _courses.TryGetValue(courseId, out var c); return Task.FromResult(c); }
        public Task<List<Course>> GetByInstructorIdAsync(int instructorId){ return Task.FromResult(_courses.Values.Where(c => c.InstructorUserId == instructorId).ToList()); }
        public Task<List<Course>> GetByStudentIdAsync(int studentId){ // For tests, return all courses as enrolled
            return Task.FromResult(_courses.Values.ToList()); }
        // Unused/minimal
        public Task<Course> CreateAsync(Course course){ _courses[course.CourseId]=course; return Task.FromResult(course); }
        public Task<List<Course>> GetAllAsync(){ return Task.FromResult(_courses.Values.ToList()); }
        public Task<Course> UpdateAsync(Course course){ _courses[course.CourseId]=course; return Task.FromResult(course); }
        public Task<bool> DeleteAsync(int courseId){ return Task.FromResult(_courses.Remove(courseId)); }
        public Task<int> CountByInstructorAsync(int instructorId){ return Task.FromResult(_courses.Values.Count(c=>c.InstructorUserId==instructorId)); }
        public Task<int> BulkDeleteAsync(List<int> courseIds){ int c=0; foreach(var id in courseIds){ if(_courses.Remove(id)) c++; } return Task.FromResult(c); }
    }

    public class FakeQuizRepositoryForAnalyticsServiceTests : IQuizRepository
    {
        private readonly Dictionary<int, Quiz> _quizzes = new();
        private readonly Dictionary<int, List<Question>> _questions = new();
        private readonly Dictionary<int, List<Choice>> _choices = new();
        public void Seed(IEnumerable<Quiz> quizzes){ foreach(var q in quizzes){ _quizzes[q.QuizId]=q; } }
        public Task<int> CountAsync()=>Task.FromResult(_quizzes.Count);
        public Task<List<Quiz>> GetByCourseIdAsync(int courseId){ return Task.FromResult(_quizzes.Values.Where(q=>q.CourseId==courseId).ToList()); }
        public Task<List<Quiz>> GetByCourseIdsAsync(List<int> courseIds){ return Task.FromResult(_quizzes.Values.Where(q=>courseIds.Contains(q.CourseId)).ToList()); }
        public Task<int> CountByCourseIdsAsync(List<int> courseIds){ return Task.FromResult(_quizzes.Values.Count(q=>courseIds.Contains(q.CourseId))); }
        public Task<List<Quiz>> GetByIdsAsync(List<int> quizIds){ return Task.FromResult(quizIds.Select(id=>_quizzes.TryGetValue(id,out var q)?q:null).Where(q=>q!=null).Cast<Quiz>().ToList()); }
        // Unused/minimal
        public Task<Quiz> CreateAsync(Quiz quiz){ _quizzes[quiz.QuizId]=quiz; return Task.FromResult(quiz); }
        public Task<Quiz?> GetByIdAsync(int quizId){ _quizzes.TryGetValue(quizId, out var q); return Task.FromResult(q); }
        public Task<Quiz> UpdateAsync(Quiz quiz){ _quizzes[quiz.QuizId]=quiz; return Task.FromResult(quiz); }
        public Task<bool> DeleteAsync(int quizId){ return Task.FromResult(_quizzes.Remove(quizId)); }
        public Task<Question> CreateQuestionAsync(Question question){ if(!_questions.ContainsKey(question.QuizId)) _questions[question.QuizId]=new List<Question>(); _questions[question.QuizId].Add(question); return Task.FromResult(question); }
        public Task<Choice> CreateChoiceAsync(Choice choice){ if(!_choices.ContainsKey(choice.QuestionId)) _choices[choice.QuestionId]=new List<Choice>(); _choices[choice.QuestionId].Add(choice); return Task.FromResult(choice); }
        public Task<List<Question>> GetQuestionsByQuizIdAsync(int quizId){ return Task.FromResult(_questions.GetValueOrDefault(quizId, new List<Question>())); }
        public Task<List<Choice>> GetChoicesByQuestionIdAsync(int questionId){ return Task.FromResult(_choices.GetValueOrDefault(questionId, new List<Choice>())); }
        public Task<List<Choice>> GetChoicesByQuestionIdsAsync(List<int> questionIds){ var res=new List<Choice>(); foreach(var id in questionIds){ res.AddRange(_choices.GetValueOrDefault(id, new List<Choice>())); } return Task.FromResult(res); }
        public Task<List<Quiz>> GetUpcomingDeadlinesAsync(DateTime threshold){ return Task.FromResult(_quizzes.Values.Where(q=>q.DueAt.HasValue && q.DueAt>threshold).ToList()); }
        public Task<int> CountByCourseAsync(int courseId){ return Task.FromResult(_quizzes.Values.Count(q=>q.CourseId==courseId)); }
        public Task<int> BulkDeleteAsync(List<int> quizIds){ int c=0; foreach(var id in quizIds){ if(_quizzes.Remove(id)) c++; } return Task.FromResult(c); }
    }

    public class FakeAttemptRepositoryForAnalyticsServiceTests : IAttemptRepository
    {
        private readonly List<Attempt> _attempts = new();
        private readonly Dictionary<int,int> _quizCourseMap = new(); // quizId -> courseId
        public void Seed(IEnumerable<Attempt> attempts){ _attempts.AddRange(attempts); }
        public void RegisterQuizCourse(int quizId, int courseId){ _quizCourseMap[quizId] = courseId; }
        public Task<List<Attempt>> GetByStudentIdAsync(int studentId){ return Task.FromResult(_attempts.Where(a=>a.UserId==studentId).ToList()); }
        public Task<List<Attempt>> GetRecentAttemptsByStudentAsync(int studentId, int count){ return Task.FromResult(_attempts.Where(a=>a.UserId==studentId).OrderByDescending(a=>a.SubmittedAt ?? a.StartedAt).Take(count).ToList()); }
        public Task<Dictionary<int, double>> GetAverageScoresByCourseIdsAsync(List<int> courseIds)
        {
            var res=new Dictionary<int,double>();
            foreach(var cid in courseIds)
            {
                var scores=_attempts.Where(a=>_quizCourseMap.GetValueOrDefault(a.QuizId)==cid).Select(a=>a.Score);
                res[cid]=scores.Any()? (double) scores.Average() : 0;
            }
            return Task.FromResult(res);
        }
        public Task<List<Attempt>> GetByQuizIdsAndStudentIdAsync(List<int> quizIds, int studentId){ return Task.FromResult(_attempts.Where(a=>quizIds.Contains(a.QuizId) && a.UserId==studentId).ToList()); }
        public Task<double> GetAverageScoreByCourseAsync(int courseId){ var scores=_attempts.Where(a=>_quizCourseMap.GetValueOrDefault(a.QuizId)==courseId).Select(a=>a.Score); return Task.FromResult(scores.Any()? (double) scores.Average() : 0); }
        // Unused/minimal
        public Task<Attempt> CreateAsync(Attempt attempt){ _attempts.Add(attempt); return Task.FromResult(attempt); }
        public Task<Attempt?> GetByIdAsync(int attemptId){ return Task.FromResult(_attempts.FirstOrDefault(a=>a.AttemptId==attemptId)); }
        public Task<List<Attempt>> GetByQuizIdAsync(int quizId){ return Task.FromResult(_attempts.Where(a=>a.QuizId==quizId).ToList()); }
        public Task<List<Attempt>> GetByQuizIdAndUserIdAsync(int quizId, int userId){ return Task.FromResult(_attempts.Where(a=>a.QuizId==quizId && a.UserId==userId).ToList()); }
        public Task<Attempt> UpdateAsync(Attempt attempt){ return Task.FromResult(attempt); }
        public Task<bool> DeleteAsync(int attemptId){ var a=_attempts.FirstOrDefault(x=>x.AttemptId==attemptId); if(a!=null){ _attempts.Remove(a); return Task.FromResult(true);} return Task.FromResult(false); }
        public Task<int> BulkDeleteAsync(List<int> attemptIds){ int c=0; foreach(var id in attemptIds){ var a=_attempts.FirstOrDefault(x=>x.AttemptId==id); if(a!=null){ _attempts.Remove(a); c++; } } return Task.FromResult(c); }
        public Task<List<Attempt>> GetAllAttemptsForExportAsync(int? quizId, int? courseId){ return Task.FromResult(_attempts.ToList()); }
    }

    public class FakeActivityLogRepositoryForAnalyticsServiceTests : IActivityLogRepository
    {
        private readonly List<ActivityLog> _logs = new();
        public void Seed(IEnumerable<ActivityLog> logs){ _logs.AddRange(logs); }
        public Task<List<ActivityLog>> GetRecentAsync(int limit){ return Task.FromResult(_logs.OrderByDescending(l=>l.CreatedAt).Take(limit).ToList()); }
        // Minimal others
        public Task<ActivityLog> CreateAsync(ActivityLog log){ _logs.Add(log); return Task.FromResult(log); }
        public Task<ActivityLog?> GetByIdAsync(long activityLogId){ return Task.FromResult(_logs.FirstOrDefault(l=>l.ActivityLogId==activityLogId)); }
        public Task<List<ActivityLog>> GetAllAsync(){ return Task.FromResult(_logs.ToList()); }
        public Task<List<ActivityLog>> GetByUserIdAsync(int userId){ return Task.FromResult(_logs.Where(l=>l.UserId==userId).ToList()); }
        public Task<List<ActivityLog>> GetByActionAsync(string action){ return Task.FromResult(_logs.Where(l=>l.Action==action).ToList()); }
        public Task<List<ActivityLog>> GetByEntityAsync(string entity, long? entityId = null){ var q=_logs.Where(l=>l.Entity==entity); if(entityId.HasValue) q=q.Where(l=>l.EntityId==entityId.Value); return Task.FromResult(q.ToList()); }
        public Task<List<ActivityLog>> GetFilteredAsync(ActivityLogFilterDto filter){ return Task.FromResult(_logs.ToList()); }
    }

    public class FakeEnrollmentRepositoryForAnalyticsServiceTests : IEnrollmentRepository
    {
        private readonly List<Enrollment> _enrollments = new();
        public void Seed(IEnumerable<Enrollment> enrollments){ _enrollments.AddRange(enrollments); }
        public Task<Dictionary<int,int>> CountByCourseIdsAsync(List<int> courseIds)
        {
            var result = new Dictionary<int,int>();
            foreach(var cid in courseIds){ result[cid] = _enrollments.Count(e=>e.CourseId==cid); }
            return Task.FromResult(result);
        }
        // Minimal others
        public Task<Enrollment> CreateAsync(Enrollment enrollment){ _enrollments.Add(enrollment); return Task.FromResult(enrollment); }
        public Task<bool> ExistsAsync(int studentId, int courseId){ return Task.FromResult(_enrollments.Any(e=>e.UserId==studentId && e.CourseId==courseId)); }
        public Task<List<Enrollment>> GetByCourseIdAsync(int courseId){ return Task.FromResult(_enrollments.Where(e=>e.CourseId==courseId).ToList()); }
        public Task<bool> DeleteAsync(int enrollmentId){ var e=_enrollments.FirstOrDefault(x=>x.EnrollmentId==enrollmentId); if(e!=null){ _enrollments.Remove(e); return Task.FromResult(true);} return Task.FromResult(false); }
        public Task<int> CountByCourseIdAsync(int courseId){ return Task.FromResult(_enrollments.Count(e=>e.CourseId==courseId)); }
        public Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds){ int c=0; foreach(var id in enrollmentIds){ var e=_enrollments.FirstOrDefault(x=>x.EnrollmentId==id); if(e!=null){ _enrollments.Remove(e); c++; } } return Task.FromResult(c); }
        public Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds){ int c=0; foreach(var s in studentIds){ var e=_enrollments.FirstOrDefault(x=>x.CourseId==courseId && x.UserId==s); if(e!=null){ _enrollments.Remove(e); c++; } } return Task.FromResult(c); }
    }

    public class AnalyticsServiceTests
    {
        private AnalyticsService CreateService(
            FakeUserRepositoryForAnalyticsServiceTests? userRepo = null,
            FakeCourseRepositoryForAnalyticsServiceTests? courseRepo = null,
            FakeQuizRepositoryForAnalyticsServiceTests? quizRepo = null,
            FakeAttemptRepositoryForAnalyticsServiceTests? attemptRepo = null,
            FakeActivityLogRepositoryForAnalyticsServiceTests? logRepo = null,
            FakeEnrollmentRepositoryForAnalyticsServiceTests? enrollRepo = null)
        {
            userRepo ??= new FakeUserRepositoryForAnalyticsServiceTests();
            courseRepo ??= new FakeCourseRepositoryForAnalyticsServiceTests();
            quizRepo ??= new FakeQuizRepositoryForAnalyticsServiceTests();
            attemptRepo ??= new FakeAttemptRepositoryForAnalyticsServiceTests();
            logRepo ??= new FakeActivityLogRepositoryForAnalyticsServiceTests();
            enrollRepo ??= new FakeEnrollmentRepositoryForAnalyticsServiceTests();
            return new AnalyticsService(userRepo, courseRepo, quizRepo, attemptRepo, logRepo, enrollRepo);
        }

        [Fact]
        public async Task GetAdminDashboardAsync_ReturnsCountsAndRecent()
        {
            var userRepo = new FakeUserRepositoryForAnalyticsServiceTests();
            userRepo.Seed(new[]
            {
                new User{ UserId=1, FullName="A", Email="a@x", CreatedAt=DateTime.UtcNow.AddDays(-2)},
                new User{ UserId=2, FullName="B", Email="b@x", CreatedAt=DateTime.UtcNow.AddDays(-8)} // outside 7-day window
            });

            var logRepo = new FakeActivityLogRepositoryForAnalyticsServiceTests();
            logRepo.Seed(new[]
            {
                new ActivityLog{ ActivityLogId=1, UserId=1, Action="LOGIN", Entity="Auth", Description="Login", CreatedAt=DateTime.UtcNow }
            });

            var courseRepo = new FakeCourseRepositoryForAnalyticsServiceTests();
            courseRepo.Seed(new[]
            {
                new Course{ CourseId=1, Name="C1" }
            });
            var quizRepo = new FakeQuizRepositoryForAnalyticsServiceTests();
            quizRepo.Seed(new[] { new Quiz{ QuizId=1, CourseId=1, Title="Q1" } });

            var svc = CreateService(userRepo, courseRepo, quizRepo, new FakeAttemptRepositoryForAnalyticsServiceTests(), logRepo, new FakeEnrollmentRepositoryForAnalyticsServiceTests());
            var dto = await svc.GetAdminDashboardAsync();
            Assert.Equal(2, dto.TotalUsers);
            Assert.Equal(1, dto.TotalCourses);
            Assert.Equal(1, dto.TotalQuizzes);
            Assert.Single(dto.RecentRegistrations); // only user 1 in window
            Assert.Single(dto.RecentActivities);
            Assert.Equal("LOGIN", dto.RecentActivities[0].Action);
        }

        [Fact]
        public async Task GetTeacherDashboardAsync_NoCourses_ReturnsZeros()
        {
            var svc = CreateService();
            var dto = await svc.GetTeacherDashboardAsync(999);
            Assert.Equal(0, dto.TotalCourses);
            Assert.Equal(0, dto.TotalQuizzes);
            Assert.Equal(0, dto.TotalStudents);
            Assert.Equal(0, dto.PendingGrading);
            Assert.Empty(dto.CoursePerformance);
        }

        [Fact]
        public async Task GetTeacherDashboardAsync_WithCourses_ReturnsAggregated()
        {
            var courseRepo = new FakeCourseRepositoryForAnalyticsServiceTests();
            courseRepo.Seed(new[]
            {
                new Course{ CourseId=1, Name="C1", InstructorUserId=42 },
                new Course{ CourseId=2, Name="C2", InstructorUserId=42 }
            });
            var quizRepo = new FakeQuizRepositoryForAnalyticsServiceTests();
            quizRepo.Seed(new[]
            {
                new Quiz{ QuizId=1, CourseId=1, Title="Q1" },
                new Quiz{ QuizId=2, CourseId=1, Title="Q2" },
                new Quiz{ QuizId=3, CourseId=2, Title="Q3" }
            });
            var enrollRepo = new FakeEnrollmentRepositoryForAnalyticsServiceTests();
            enrollRepo.Seed(new[]
            {
                new Enrollment{ EnrollmentId=1, CourseId=1, UserId=10 },
                new Enrollment{ EnrollmentId=2, CourseId=1, UserId=11 },
                new Enrollment{ EnrollmentId=3, CourseId=2, UserId=12 }
            });
            var attemptRepo = new FakeAttemptRepositoryForAnalyticsServiceTests();
            attemptRepo.Seed(new[]
            {
                new Attempt{ AttemptId=1, QuizId=1, UserId=10, Score=80m },
                new Attempt{ AttemptId=2, QuizId=2, UserId=11, Score=60m },
                new Attempt{ AttemptId=3, QuizId=3, UserId=12, Score=90m }
            });
            // Register quiz -> course mapping for averages
            attemptRepo.RegisterQuizCourse(1, 1);
            attemptRepo.RegisterQuizCourse(2, 1);
            attemptRepo.RegisterQuizCourse(3, 2);

            var svc = CreateService(new FakeUserRepositoryForAnalyticsServiceTests(), courseRepo, quizRepo, attemptRepo, new FakeActivityLogRepositoryForAnalyticsServiceTests(), enrollRepo);
            var dto = await svc.GetTeacherDashboardAsync(42);
            Assert.Equal(2, dto.TotalCourses);
            Assert.Equal(3, dto.TotalQuizzes);
            Assert.Equal(3, dto.TotalStudents);
            Assert.Equal(2, dto.CoursePerformance.Count);
            var c1 = dto.CoursePerformance.First(cp=>cp.CourseId==1);
            var c2 = dto.CoursePerformance.First(cp=>cp.CourseId==2);
            Assert.Equal(2, c1.StudentCount);
            Assert.Equal(1, c2.StudentCount);
            Assert.True(c1.AverageScore > 0);
            Assert.True(c2.AverageScore > 0);
        }

        [Fact]
        public async Task GetStudentDashboardAsync_ReturnsUpcomingAndRecent()
        {
            var courseRepo = new FakeCourseRepositoryForAnalyticsServiceTests();
            courseRepo.Seed(new[]
            {
                new Course{ CourseId=1, Name="C1", InstructorUserId=7 },
                new Course{ CourseId=2, Name="C2", InstructorUserId=7 }
            });
            var quizRepo = new FakeQuizRepositoryForAnalyticsServiceTests();
            quizRepo.Seed(new[]
            {
                new Quiz{ QuizId=1, CourseId=1, Title="Q1", IsPublished=true, DueAt=DateTime.UtcNow.AddDays(2) },
                new Quiz{ QuizId=2, CourseId=2, Title="Q2", IsPublished=true, DueAt=DateTime.UtcNow.AddDays(3) },
                new Quiz{ QuizId=3, CourseId=2, Title="Q3", IsPublished=false, DueAt=DateTime.UtcNow.AddDays(5) }
            });
            var attemptRepo = new FakeAttemptRepositoryForAnalyticsServiceTests();
            attemptRepo.Seed(new[]
            {
                new Attempt{ AttemptId=1, QuizId=2, UserId=99, Score=75m, SubmittedAt=DateTime.UtcNow.AddDays(-1) },
                new Attempt{ AttemptId=2, QuizId=1, UserId=99, Score=85m, SubmittedAt=DateTime.UtcNow.AddDays(-2) }
            });
            attemptRepo.RegisterQuizCourse(1, 1);
            attemptRepo.RegisterQuizCourse(2, 2);

            var svc = CreateService(new FakeUserRepositoryForAnalyticsServiceTests(), courseRepo, quizRepo, attemptRepo, new FakeActivityLogRepositoryForAnalyticsServiceTests(), new FakeEnrollmentRepositoryForAnalyticsServiceTests());
            var dto = await svc.GetStudentDashboardAsync(99);
            Assert.Equal(2, dto.CompletedQuizzes);
            Assert.True(dto.AverageScore >= 80 && dto.AverageScore <= 85);
            // Upcoming should exclude attempted quizzes for student 99
            Assert.Empty(dto.UpcomingQuizzes); // both published upcoming quizzes already attempted
            Assert.Equal(2, dto.RecentResults.Count);
            Assert.Contains(dto.RecentResults, r => r.CourseName == "C1" || r.CourseName == "C2");
        }

        [Fact]
        public async Task GetCourseAnalyticsAsync_NotFound_Throws()
        {
            var svc = CreateService();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetCourseAnalyticsAsync(123));
        }

        [Fact]
        public async Task GetCourseAnalyticsAsync_ReturnsAggregated()
        {
            var courseRepo = new FakeCourseRepositoryForAnalyticsServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=10, Name="Data", InstructorUserId=1 } });
            var quizRepo = new FakeQuizRepositoryForAnalyticsServiceTests();
            quizRepo.Seed(new[]
            {
                new Quiz{ QuizId=1, CourseId=10, Title="Intro" },
                new Quiz{ QuizId=2, CourseId=10, Title="Mid" }
            });
            var attemptRepo = new FakeAttemptRepositoryForAnalyticsServiceTests();
            attemptRepo.Seed(new[]
            {
                new Attempt{ AttemptId=1, QuizId=1, UserId=5, Score=70m },
                new Attempt{ AttemptId=2, QuizId=2, UserId=6, Score=90m }
            });
            attemptRepo.RegisterQuizCourse(1, 10);
            attemptRepo.RegisterQuizCourse(2, 10);
            var svc = CreateService(new FakeUserRepositoryForAnalyticsServiceTests(), courseRepo, quizRepo, attemptRepo, new FakeActivityLogRepositoryForAnalyticsServiceTests(), new FakeEnrollmentRepositoryForAnalyticsServiceTests());
            var dto = await svc.GetCourseAnalyticsAsync(10);
            Assert.Equal(10, dto.CourseId);
            Assert.Equal("Data", dto.CourseName);
            Assert.Equal(2, dto.TotalQuizzes);
            Assert.Equal(80, dto.AverageScore);
        }
    }
}
