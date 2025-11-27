using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.Controllers;
using OnlineQuiz.DTOs;
using OnlineQuiz.Services;
using System.Security.Claims;
using Xunit;

namespace OnlineQuiz.Tests.Controllers
{
    internal class FakeAnalyticsService : IAnalyticsService
    {
        public int? LastTeacherId { get; private set; }
        public int? LastStudentId { get; private set; }
        public int? LastCourseId { get; private set; }

        public Task<AdminDashboardDto> GetAdminDashboardAsync()
            => Task.FromResult(new AdminDashboardDto
            {
                TotalUsers = 100,
                TotalCourses = 10,
                TotalQuizzes = 50,
                ActiveUsers = 80,
                RecentRegistrations = new List<RegistrationStatDto> { new RegistrationStatDto { Date = DateTime.UtcNow.Date, Count = 5 } },
                RecentActivities = new List<ActivityLogDto>()
            });

        public Task<TeacherDashboardDto> GetTeacherDashboardAsync(int teacherId)
        {
            LastTeacherId = teacherId;
            return Task.FromResult(new TeacherDashboardDto
            {
                TotalCourses = 3,
                TotalStudents = 120,
                TotalQuizzes = 12,
                PendingGrading = 2,
                CoursePerformance = new List<CoursePerformanceDto>
                {
                    new CoursePerformanceDto { CourseId = 1, CourseName = "Algorithms", StudentCount = 40, AverageScore = 85.2 }
                }
            });
        }

        public Task<StudentDashboardDto> GetStudentDashboardAsync(int studentId)
        {
            LastStudentId = studentId;
            return Task.FromResult(new StudentDashboardDto
            {
                EnrolledCourses = 4,
                CompletedQuizzes = 7,
                AverageScore = 88.5,
                UpcomingQuizzes = new List<UpcomingQuizDto> { new UpcomingQuizDto { QuizId = 9, Title = "Graphs", CourseName = "Algorithms" } },
                RecentResults = new List<RecentResultDto> { new RecentResultDto { QuizId = 7, QuizTitle = "Sorting", CourseName = "Algorithms", Score = 92, SubmittedAt = DateTime.UtcNow } }
            });
        }

        public Task<CourseAnalyticsDto> GetCourseAnalyticsAsync(int courseId)
        {
            LastCourseId = courseId;
            return Task.FromResult(new CourseAnalyticsDto
            {
                CourseId = courseId,
                CourseName = "Linear Algebra",
                TotalStudents = 60,
                TotalQuizzes = 8,
                AverageScore = 76.4,
                StudentProgress = new List<StudentProgressDto>
                {
                    new StudentProgressDto { StudentId = 10, StudentName = "Alice", QuizzesTaken = 5, AverageScore = 80.1 }
                }
            });
        }
    }

    public class AnalyticsControllerTests
    {
        private static AnalyticsController CreateController(IAnalyticsService? service = null, ClaimsIdentity? identity = null)
        {
            var controller = new AnalyticsController(service ?? new FakeAnalyticsService());
            var httpContext = new DefaultHttpContext();
            if (identity != null)
            {
                httpContext.User = new ClaimsPrincipal(identity);
            }
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static ClaimsIdentity AdminIdentity()
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth");

        private static ClaimsIdentity TeacherIdentity(string userId = "15")
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Teacher")
            }, "TestAuth");

        private static ClaimsIdentity StudentIdentity(string userId = "20")
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Student")
            }, "TestAuth");

        [Fact]
        public async Task GetAdminDashboard_ReturnsOk_WithPayload()
        {
            var svc = new FakeAnalyticsService();
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.GetAdminDashboard();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<AdminDashboardDto>(ok.Value);
            Assert.Equal(100, payload.TotalUsers);
            Assert.Equal(10, payload.TotalCourses);
            Assert.True(payload.RecentRegistrations.Count >= 1);
        }

        [Fact]
        public async Task GetTeacherDashboard_ReturnsOk_UsesNameIdentifier()
        {
            var svc = new FakeAnalyticsService();
            var controller = CreateController(svc, TeacherIdentity("42"));

            var result = await controller.GetTeacherDashboard();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<TeacherDashboardDto>(ok.Value);
            Assert.Equal(3, payload.TotalCourses);
            Assert.Equal(42, svc.LastTeacherId);
        }

        [Fact]
        public async Task GetStudentDashboard_ReturnsOk_UsesNameIdentifier()
        {
            var svc = new FakeAnalyticsService();
            var controller = CreateController(svc, StudentIdentity("77"));

            var result = await controller.GetStudentDashboard();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<StudentDashboardDto>(ok.Value);
            Assert.Equal(4, payload.EnrolledCourses);
            Assert.Equal(77, svc.LastStudentId);
        }

        [Fact]
        public async Task GetCourseAnalytics_ReturnsOk_WithCourseId()
        {
            var svc = new FakeAnalyticsService();
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.GetCourseAnalytics(5);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<CourseAnalyticsDto>(ok.Value);
            Assert.Equal(5, payload.CourseId);
            Assert.Equal(5, svc.LastCourseId);
        }
    }
}

