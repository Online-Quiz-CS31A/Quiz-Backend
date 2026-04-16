using OnlineQuiz.DTOs;

namespace OnlineQuiz.IServices
{
    public interface IESP32Service
    {
        // Command Methods
        Task<ESP32ResponseDto> SendEnrollCommandAsync(int slotId, int userId);
        Task<ESP32ResponseDto> SendVerifyCommandAsync(int slotId, int userId);
        Task<ESP32ResponseDto> CancelOperationAsync();
        Task<BiometricStatusDto> GetDeviceStatusAsync();

        // Events - fired when hardware completes operations
        event EventHandler<ESP32ResponseDto>? OnEnrollmentCompleted;
        event EventHandler<ESP32ResponseDto>? OnVerificationCompleted;
        event EventHandler<string>? OnDeviceStatusChanged;

        // Connection Management
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        bool IsConnected { get; }
    }
}
