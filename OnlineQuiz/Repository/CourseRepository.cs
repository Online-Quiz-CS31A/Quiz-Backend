using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using Postgrest;

namespace OnlineQuiz.Repository
{
    public class CourseRepository : ICourseRepository
    {
        private readonly SupabaseService _supabaseService;

        public CourseRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<Course> CreateAsync(Course course)
        {
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var response = await _supabaseService.GetClient().From<Course>().Insert(course, options);
            var createdCourse = response.Model ?? throw new InvalidOperationException("Failed to create course");
            
            // If CourseId is not populated, fetch by unique constraint (Code + Instructor)
            if (createdCourse.CourseId == 0)
            {
                var result = await _supabaseService.GetClient().From<Course>()
                    .Where(c => c.Code == course.Code && c.InstructorUserId == course.InstructorUserId)
                    .Order("CreatedAt", Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();
                return result.Models.FirstOrDefault() ?? createdCourse;
            }
            
            return createdCourse;
        }

        public async Task<Course?> GetByIdAsync(int courseId)
        {
            var response = await _supabaseService.GetClient().From<Course>()
                .Where(c => c.CourseId == courseId)
                .Single();
            return response;
        }

        public async Task<List<Course>> GetAllAsync()
        {
            var response = await _supabaseService.GetClient().From<Course>().Get();
            return response.Models;
        }

        public async Task<Course> UpdateAsync(Course course)
        {
            var response = await _supabaseService.GetClient().From<Course>().Update(course);
            return response.Model ?? throw new InvalidOperationException("Failed to update course");
        }

        public async Task<bool> DeleteAsync(int courseId)
        {
            // First check if course exists
            var course = await GetByIdAsync(courseId);
            if (course == null)
            {
                return false;
            }

            // Attempt to delete the course
            await _supabaseService.GetClient().From<Course>()
                .Where(c => c.CourseId == courseId)
                .Delete();

            // Verify deletion succeeded by checking if course still exists
            var verifyDeleted = await GetByIdAsync(courseId);
            return verifyDeleted == null;
        }

        public async Task<List<Course>> GetByInstructorIdAsync(int instructorId)
        {
            var response = await _supabaseService.GetClient().From<Course>()
                .Where(c => c.InstructorUserId == instructorId)
                .Get();
            return response.Models;
        }

        public async Task<List<Course>> GetByStudentIdAsync(int studentId)
        {
            var response = await _supabaseService.GetClient().From<Enrollment>()
                .Select("*")
                .Where(e => e.UserId == studentId)
                .Get();

            return response.Models.Select(e => e.Course).Where(c => c != null).Cast<Course>().ToList();
        }

        public async Task<int> CountAsync()
        {
            var response = await _supabaseService.GetClient().From<Course>().Count(Postgrest.Constants.CountType.Exact);
            return response;
        }

        public async Task<int> CountByInstructorAsync(int instructorId)
        {
            var response = await _supabaseService.GetClient().From<Course>()
                .Where(c => c.InstructorUserId == instructorId)
                .Count(Postgrest.Constants.CountType.Exact);
            return response;
        }

        public async Task<int> BulkDeleteAsync(List<int> courseIds)
        {
            if (!courseIds.Any()) return 0;

            await _supabaseService.GetClient().From<Course>()
                .Filter("CourseId", Postgrest.Constants.Operator.In, courseIds)
                .Delete();
            return courseIds.Count;
        }
    }
}
