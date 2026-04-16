using System.ComponentModel.DataAnnotations;

namespace OnlineQuiz.DTOs
{
    // =====================================================
    // REQUEST DTOs
    // =====================================================

    public class BiometricEnrollRequestDto
    {
        [Required]
        public int UserId { get; set; }
    }

    public class BiometricVerifyRequestDto
    {
        [Required]
        public int UserId { get; set; }

        public int? QuizId { get; set; }
    }

    // =====================================================
    // RESPONSE DTOs
    // =====================================================

    public class BiometricEnrollResponseDto
    {
        public bool Success { get; set; }
        public int? SlotId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BiometricVerifyResponseDto
    {
        public bool Success { get; set; }
        public bool Matched { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BiometricStatusDto
    {
        public bool IsConnected { get; set; }
        public string CurrentMode { get; set; } = "Idle"; // Idle, Enrollment, Verification
        public int? ActiveUserId { get; set; }
        public string? ActiveUserName { get; set; }
    }

    public class BiometricLogDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int? SlotId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =====================================================
    // ESP32 INTERNAL DTOs (for communication with hardware)
    // =====================================================

    public class ESP32CommandDto
    {
        public string Command { get; set; } = string.Empty; // "enroll", "verify", "cancel"
        public int? SlotId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ESP32ResponseDto
    {
        public bool Success { get; set; }
        public int? UserId { get; set; }
        public int? SlotId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // =====================================================
    // ADDITIONAL DTOs
    // =====================================================

    public class BiometricUnenrollRequestDto
    {
        [Required]
        public int UserId { get; set; }
    }

    public class AvailableSlotsResponseDto
    {
        public List<int> AvailableSlots { get; set; } = new();
        public int TotalSlots { get; set; } = 127;
        public int UsedSlots { get; set; }
    }

    public class BiometricLogFilterDto
    {
        public int? UserId { get; set; }
        public string? ActionType { get; set; }
        public bool? Success { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1")]
        public int Page { get; set; } = 1;
        
        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
        public int PageSize { get; set; } = 20;
    }
}
