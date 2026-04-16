using OnlineQuiz.Models;

namespace OnlineQuiz.IRepository
{
    public interface IBiometricRepository
    {
        // Fingerprint Slot Management
        Task<bool> AssignFingerprintSlotAsync(int userId, int slotId);
        Task<int?> GetFingerprintSlotByUserIdAsync(int userId);
        Task<User?> GetUserByFingerprintSlotAsync(int slotId);
        Task<bool> RemoveFingerprintSlotAsync(int userId);
        Task<bool> IsSlotAvailableAsync(int slotId);
        Task<List<int>> GetAvailableSlotsAsync();
        Task<int?> GetNextAvailableSlotAsync();
        Task<bool> UpdateLastVerifiedAtAsync(int userId);

        // Biometric Log Management
        Task<int> LogBiometricEventAsync(BiometricLog log);
        Task<List<BiometricLog>> GetBiometricLogsAsync(
            int? userId = null,
            string? actionType = null,
            bool? success = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int page = 1,
            int pageSize = 20);
        Task<int> GetBiometricLogsCountAsync(
            int? userId = null,
            string? actionType = null,
            bool? success = null,
            DateTime? startDate = null,
            DateTime? endDate = null);
        Task<BiometricLog?> GetBiometricLogByIdAsync(int id);
    }
}
