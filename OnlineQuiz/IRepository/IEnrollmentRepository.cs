using OnlineQuiz.Models;

namespace OnlineQuiz.IRepository
{
    public interface IEnrollmentRepository
    {
        Task<Enrollment> CreateAsync(Enrollment enrollment);
        Task<bool> ExistsAsync(int studentId, int courseId);
        Task<List<Enrollment>> GetByCourseIdAsync(int courseId);
        Task<bool> DeleteAsync(int enrollmentId);
        Task<int> CountByCourseIdAsync(int courseId);
        Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds);
        Task<Dictionary<int, int>> CountSectionsByCourseIdsAsync(List<int> courseIds);
        Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds);
        Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds);
    }
}
