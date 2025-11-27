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
    // In-memory fakes for CourseService
    public class FakeCourseRepositoryForCourseServiceTests : ICourseRepository
    {
        private readonly Dictionary<int, Course> _courses = new();
        public void Seed(IEnumerable<Course> courses){ foreach(var c in courses){ _courses[c.CourseId]=c; } }
        public Task<Course> CreateAsync(Course course){ _courses[course.CourseId]=course; return Task.FromResult(course); }
        public Task<Course?> GetByIdAsync(int courseId){ _courses.TryGetValue(courseId, out var c); return Task.FromResult(c); }
        public Task<List<Course>> GetAllAsync(){ return Task.FromResult(_courses.Values.ToList()); }
        public Task<Course> UpdateAsync(Course course){ _courses[course.CourseId]=course; return Task.FromResult(course); }
        public Task<bool> DeleteAsync(int courseId){ return Task.FromResult(_courses.Remove(courseId)); }
        public Task<List<Course>> GetByInstructorIdAsync(int instructorId){ return Task.FromResult(_courses.Values.Where(c=>c.InstructorUserId==instructorId).ToList()); }
        public Task<List<Course>> GetByStudentIdAsync(int studentId){ return Task.FromResult(_courses.Values.ToList()); }
        public Task<int> CountAsync(){ return Task.FromResult(_courses.Count); }
        public Task<int> CountByInstructorAsync(int instructorId){ return Task.FromResult(_courses.Values.Count(c=>c.InstructorUserId==instructorId)); }
        public Task<int> BulkDeleteAsync(List<int> courseIds){ int count=0; foreach(var id in courseIds){ if(_courses.Remove(id)) count++; } return Task.FromResult(count); }
    }

    public class FakeUserRepositoryForCourseServiceTests : IUserRepository
    {
        private readonly Dictionary<int, User> _users = new();
        public void Seed(IEnumerable<User> users){ foreach(var u in users){ _users[u.UserId]=u; } }
        public Task<User> CreateAsync(User user){ _users[user.UserId]=user; return Task.FromResult(user); }
        public Task<User?> GetByIdAsync(int userId){ _users.TryGetValue(userId, out var u); return Task.FromResult(u); }
        public Task<User?> GetByEmailAsync(string email){ return Task.FromResult(_users.Values.FirstOrDefault(u=>u.Email==email)); }
        public Task<List<User>> GetAllAsync(){ return Task.FromResult(_users.Values.ToList()); }
        public Task<List<User>> GetByIdsAsync(List<int> userIds){ return Task.FromResult(userIds.Select(id=>_users.TryGetValue(id, out var u)?u:null).Where(u=>u!=null).Cast<User>().ToList()); }
        public Task<int> CountAsync(){ return Task.FromResult(_users.Count); }
        public Task<int> CountByRoleAsync(int roleId){ return Task.FromResult(0); }
        public Task<List<User>> GetRecentRegistrationsAsync(int days){ return Task.FromResult(_users.Values.ToList()); }
        public Task<User> UpdateAsync(User user){ _users[user.UserId]=user; return Task.FromResult(user); }
        public Task<bool> DeleteAsync(int userId){ return Task.FromResult(_users.Remove(userId)); }
        public Task<int> BulkDeleteAsync(List<int> userIds){ int c=0; foreach(var id in userIds){ if(_users.Remove(id)) c++; } return Task.FromResult(c); }
    }

    public class FakeStudentRepositoryForCourseServiceTests : IStudentRepository
    {
        private readonly Dictionary<int, Student> _students = new(); // key: UserId
        public void Seed(IEnumerable<Student> students){ foreach(var s in students){ _students[s.UserId]=s; } }
        public Task<Student> CreateAsync(Student student){ _students[student.UserId]=student; return Task.FromResult(student); }
        public Task<Student?> GetByUserIdAsync(int userId){ _students.TryGetValue(userId, out var s); return Task.FromResult(s); }
        public Task<List<Student>> GetAllAsync(){ return Task.FromResult(_students.Values.ToList()); }
        public Task<List<Student>> GetByIdsAsync(List<int> userIds){ return Task.FromResult(userIds.Select(id=>_students.TryGetValue(id,out var s)?s:null).Where(s=>s!=null).Cast<Student>().ToList()); }
        public Task<Student> UpdateAsync(Student student){ _students[student.UserId]=student; return Task.FromResult(student); }
        public Task<bool> DeleteAsync(int userId){ return Task.FromResult(_students.Remove(userId)); }
    }

    public class FakeTeacherRepositoryForCourseServiceTests : ITeacherRepository
    {
        private readonly Dictionary<int, Teacher> _teachers = new(); // key: UserId
        public void Seed(IEnumerable<Teacher> teachers){ foreach(var t in teachers){ _teachers[t.UserId]=t; } }
        public Task<Teacher> CreateAsync(Teacher teacher){ _teachers[teacher.UserId]=teacher; return Task.FromResult(teacher); }
        public Task<Teacher?> GetByUserIdAsync(int userId){ _teachers.TryGetValue(userId, out var t); return Task.FromResult(t); }
        public Task<List<Teacher>> GetAllAsync(){ return Task.FromResult(_teachers.Values.ToList()); }
        public Task<Teacher> UpdateAsync(Teacher teacher){ _teachers[teacher.UserId]=teacher; return Task.FromResult(teacher); }
        public Task<bool> DeleteAsync(int userId){ return Task.FromResult(_teachers.Remove(userId)); }
    }

    public class FakeEnrollmentRepositoryForCourseServiceTests : IEnrollmentRepository
    {
        private readonly List<Enrollment> _enrollments = new();
        public void Seed(IEnumerable<Enrollment> enrollments){ _enrollments.AddRange(enrollments); }
        public Task<Enrollment> CreateAsync(Enrollment enrollment){ _enrollments.Add(enrollment); return Task.FromResult(enrollment); }
        public Task<bool> ExistsAsync(int studentId, int courseId){ return Task.FromResult(_enrollments.Any(e=>e.UserId==studentId && e.CourseId==courseId)); }
        public Task<List<Enrollment>> GetByCourseIdAsync(int courseId){ return Task.FromResult(_enrollments.Where(e=>e.CourseId==courseId).ToList()); }
        public Task<bool> DeleteAsync(int enrollmentId){ var e=_enrollments.FirstOrDefault(x=>x.EnrollmentId==enrollmentId); if(e!=null){ _enrollments.Remove(e); return Task.FromResult(true);} return Task.FromResult(false); }
        public Task<int> CountByCourseIdAsync(int courseId){ return Task.FromResult(_enrollments.Count(e=>e.CourseId==courseId)); }
        public Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds){ var d=new Dictionary<int,int>(); foreach(var id in courseIds){ d[id]=_enrollments.Count(e=>e.CourseId==id);} return Task.FromResult(d); }
        public Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds){ int c=0; foreach(var id in enrollmentIds){ var e=_enrollments.FirstOrDefault(x=>x.EnrollmentId==id); if(e!=null){ _enrollments.Remove(e); c++; } } return Task.FromResult(c); }
        public Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds){ int c=0; foreach(var s in studentIds){ var e=_enrollments.FirstOrDefault(x=>x.CourseId==courseId && x.UserId==s); if(e!=null){ _enrollments.Remove(e); c++; } } return Task.FromResult(c); }
    }

    public class CourseServiceTests
    {
        private CourseService CreateService(
            FakeCourseRepositoryForCourseServiceTests? courseRepo = null,
            FakeEnrollmentRepositoryForCourseServiceTests? enrollmentRepo = null,
            FakeUserRepositoryForCourseServiceTests? userRepo = null,
            FakeTeacherRepositoryForCourseServiceTests? teacherRepo = null,
            FakeStudentRepositoryForCourseServiceTests? studentRepo = null)
        {
            courseRepo ??= new FakeCourseRepositoryForCourseServiceTests();
            enrollmentRepo ??= new FakeEnrollmentRepositoryForCourseServiceTests();
            userRepo ??= new FakeUserRepositoryForCourseServiceTests();
            teacherRepo ??= new FakeTeacherRepositoryForCourseServiceTests();
            studentRepo ??= new FakeStudentRepositoryForCourseServiceTests();
            return new CourseService(courseRepo, enrollmentRepo, userRepo, teacherRepo, studentRepo);
        }

        [Fact]
        public async Task GetCourseByIdAsync_ReturnsInstructorName()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=101, Name="Algebra", InstructorUserId=50 } });

            var userRepo = new FakeUserRepositoryForCourseServiceTests();
            userRepo.Seed(new[] { new User{ UserId=50, FullName="Prof. Euler", Email="euler@example.com" } });

            var svc = CreateService(courseRepo, null, userRepo);
            var dto = await svc.GetCourseByIdAsync(101);
            Assert.NotNull(dto);
            Assert.Equal("Algebra", dto!.Name);
            Assert.Equal("Prof. Euler", dto.InstructorName);
        }

        [Fact]
        public async Task GetCoursesForTeacherAsync_ReturnsListWithInstructorName()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] {
                new Course{ CourseId=1, Name="C1", InstructorUserId=50 },
                new Course{ CourseId=2, Name="C2", InstructorUserId=50 }
            });
            var userRepo = new FakeUserRepositoryForCourseServiceTests();
            userRepo.Seed(new[] { new User{ UserId=50, FullName="Prof. Euler", Email="euler@example.com" } });

            var svc = CreateService(courseRepo, null, userRepo);
            var list = await svc.GetCoursesForTeacherAsync(50);
            Assert.Equal(2, list.Count);
            Assert.All(list, c => Assert.Equal("Prof. Euler", c.InstructorName));
        }

        [Fact]
        public async Task EnrollStudentAsync_Succeeds()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=10, Name="Physics", InstructorUserId=50, Code="PHY" } });

            var userRepo = new FakeUserRepositoryForCourseServiceTests();
            userRepo.Seed(new[] {
                new User{ UserId=50, FullName="Prof. Feynman", Email="feynman@example.com" },
                new User{ UserId=99, FullName="Alice", Email="alice@example.com" }
            });

            var studentRepo = new FakeStudentRepositoryForCourseServiceTests();
            studentRepo.Seed(new[] { new Student{ UserId=99, StudentId="S-99", YearLevel=2, Section="B", Course="Physics" } });

            var enrollmentRepo = new FakeEnrollmentRepositoryForCourseServiceTests();

            var svc = CreateService(courseRepo, enrollmentRepo, userRepo, null, studentRepo);
            var result = await svc.EnrollStudentAsync(new EnrollStudentDto
            {
                CourseId = 10,
                StudentId = 99,
                EnrolledBy = 50,
                Section = "B"
            });

            Assert.Equal(99, result.UserId);
            Assert.Equal("Alice", result.StudentName);
            Assert.Equal(10, result.CourseId);
            Assert.Equal("Physics", result.CourseName);
            Assert.Equal("PHY", result.CourseCode);
            Assert.Equal("Prof. Feynman", result.EnrolledByName);
        }

        [Fact]
        public async Task GetCourseEnrollmentsAsync_Unauthorized_Throws()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=10, Name="Physics", InstructorUserId=50 } });
            var svc = CreateService(courseRepo);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetCourseEnrollmentsAsync(10, teacherId: 51));
        }

        [Fact]
        public async Task UnenrollStudentAsync_DeletesEnrollment()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=10, Name="Physics", InstructorUserId=50 } });
            var enrollmentRepo = new FakeEnrollmentRepositoryForCourseServiceTests();
            enrollmentRepo.Seed(new[] { new Enrollment{ EnrollmentId=1, CourseId=10, UserId=99 } });

            var svc = CreateService(courseRepo, enrollmentRepo);
            var ok = await svc.UnenrollStudentAsync(10, 99, 50);
            Assert.True(ok);
        }

        [Fact]
        public async Task UpdateCourseAsync_ChangesFieldsAndInstructor()
        {
            var courseRepo = new FakeCourseRepositoryForCourseServiceTests();
            courseRepo.Seed(new[] { new Course{ CourseId=10, Name="Old", InstructorUserId=50, Category="A" } });
            var teacherRepo = new FakeTeacherRepositoryForCourseServiceTests();
            teacherRepo.Seed(new[] { new Teacher{ UserId=77, Department="Science" } });
            var userRepo = new FakeUserRepositoryForCourseServiceTests();
            userRepo.Seed(new[] { new User{ UserId=77, FullName="Prof. Maxwell", Email="maxwell@example.com" } });

            var svc = CreateService(courseRepo, null, userRepo, teacherRepo);
            var updated = await svc.UpdateCourseAsync(10, new UpdateCourseDto
            {
                Name = "New",
                Category = "B",
                Section = "C",
                InstructorId = 77
            });

            Assert.Equal("New", updated.Name);
            Assert.Equal("Prof. Maxwell", updated.InstructorName);
        }
    }
}
