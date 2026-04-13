using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;

namespace OnlineQuiz.Repository
{
    public class EnrollmentRepository : IEnrollmentRepository
    {
        private readonly SupabaseService _supabaseService;

        public EnrollmentRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<Enrollment> CreateAsync(Enrollment enrollment)
        {
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var response = await _supabaseService.GetClient().From<Enrollment>().Insert(enrollment, options);
            var created = response.Model ?? throw new InvalidOperationException("Failed to create enrollment");
            
            // If EnrollmentId is not populated, fetch by unique constraint
            if (created.EnrollmentId == 0)
            {
                var result = await _supabaseService.GetClient().From<Enrollment>()
                    .Where(e => e.UserId == enrollment.UserId && e.CourseId == enrollment.CourseId)
                    .Get();
                return result.Models.FirstOrDefault() ?? created;
            }
            
            return created;
        }

        public async Task<bool> ExistsAsync(int studentId, int courseId)
        {
            var response = await _supabaseService.GetClient().From<Enrollment>()
                .Where(e => e.UserId == studentId && e.CourseId == courseId)
                .Get();
            return response.Models.Any();
        }

        public async Task<List<Enrollment>> GetByCourseIdAsync(int courseId)
        {
            var response = await _supabaseService.GetClient().From<Enrollment>()
                .Where(e => e.CourseId == courseId)
                .Get();
            return response.Models;
        }

        public async Task<bool> DeleteAsync(int enrollmentId)
        {
            await _supabaseService.GetClient().From<Enrollment>()
                .Where(e => e.EnrollmentId == enrollmentId)
                .Delete();
            return true;
        }

        public async Task<int> CountByCourseIdAsync(int courseId)
        {
            var count = await _supabaseService.GetClient().From<Enrollment>()
                .Where(e => e.CourseId == courseId)
                .Count(Postgrest.Constants.CountType.Exact);
            return count;
        }

        public async Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds)
        {
            if (!courseIds.Any()) return new Dictionary<int, int>();

            var response = await _supabaseService.GetClient().From<Enrollment>()
                .Filter("CourseId", Postgrest.Constants.Operator.In, courseIds)
                .Get();

            return response.Models
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<int, int>> CountSectionsByCourseIdsAsync(List<int> courseIds)
        {
            if (!courseIds.Any()) return new Dictionary<int, int>();

            var response = await _supabaseService.GetClient().From<Enrollment>()
                .Filter("CourseId", Postgrest.Constants.Operator.In, courseIds)
                .Get();

            return response.Models
                .GroupBy(e => e.CourseId)
                .ToDictionary(
                    g => g.Key, 
                    g => g.Select(e => e.Section).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count()
                );
        }

        public async Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds)
        {
            if (!enrollmentIds.Any()) return 0;

            await _supabaseService.GetClient().From<Enrollment>()
                .Filter("EnrollmentId", Postgrest.Constants.Operator.In, enrollmentIds)
                .Delete();
            return enrollmentIds.Count;
        }

        public async Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds)
        {
            if (!studentIds.Any()) return 0;

            // Find enrollments matching the course and student IDs
            var enrollments = await _supabaseService.GetClient().From<Enrollment>()
                .Where(e => e.CourseId == courseId)
                .Filter("UserId", Postgrest.Constants.Operator.In, studentIds)
                .Get();

            if (!enrollments.Models.Any()) return 0;

            var enrollmentIds = enrollments.Models.Select(e => e.EnrollmentId).ToList();
            
            await _supabaseService.GetClient().From<Enrollment>()
                .Filter("EnrollmentId", Postgrest.Constants.Operator.In, enrollmentIds)
                .Delete();
            
            return enrollmentIds.Count;
        }
    }
}
