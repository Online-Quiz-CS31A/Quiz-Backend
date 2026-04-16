using OnlineQuiz.DTOs;

namespace OnlineQuiz.IServices
{
    public interface IBiometricService
    {
        // Enrollment Operations
        Task<BiometricEnrollResponseDto> StartEnrollmentAsync(int userId, int requestedByUserId);
        Task<BiometricEnrollResponseDto> CompleteEnrollmentAsync(int userId, int slotId, bool success, string? errorMessage = null);

        // Verification Operations
        Task<BiometricVerifyResponseDto> StartVerificationAsync(int userId, int? quizId = null);
        Task<BiometricVerifyResponseDto> CompleteVerificationAsync(int userId, bool matched, string? errorMessage = null);

        // Unenrollment Operations
        Task<BiometricEnrollResponseDto> UnenrollFingerprintAsync(int userId, int requestedByUserId);

        // Status & Information
        Task<BiometricStatusDto> GetDeviceStatusAsync();
        Task<AvailableSlotsResponseDto> GetAvailableSlotsAsync();
        Task<bool> IsUserEnrolledAsync(int userId);
        Task<int?> GetUserFingerprintSlotAsync(int userId);

        // Audit Logs
        Task<List<BiometricLogDto>> GetBiometricLogsAsync(BiometricLogFilterDto filter);
        Task<int> GetBiometricLogsCountAsync(BiometricLogFilterDto filter);

        // Cancel Operation
        Task<bool> CancelCurrentOperationAsync();
    }
}
