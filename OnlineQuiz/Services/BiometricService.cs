using Mapster;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;

namespace OnlineQuiz.Services
{
    public class BiometricService : IBiometricService
    {
        private readonly IBiometricRepository _biometricRepository;
        private readonly IUserRepository _userRepository;
        private readonly IESP32Service _esp32Service;
        private readonly ILogger<BiometricService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BiometricService(
            IBiometricRepository biometricRepository,
            IUserRepository userRepository,
            IESP32Service esp32Service,
            ILogger<BiometricService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _biometricRepository = biometricRepository;
            _userRepository = userRepository;
            _esp32Service = esp32Service;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // =====================================================
        // ENROLLMENT OPERATIONS
        // =====================================================

        public async Task<BiometricEnrollResponseDto> StartEnrollmentAsync(int userId, int requestedByUserId)
        {
            try
            {
                _logger.LogInformation("Starting fingerprint enrollment for user {UserId}", userId);

                // Validate user exists
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    await LogEventAsync(userId, "enrollment_failed", false, null, "User not found");
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Check if user already has fingerprint enrolled
                if (user.FingerprintSlotId.HasValue)
                {
                    await LogEventAsync(userId, "enrollment_failed", false, user.FingerprintSlotId, "User already has fingerprint enrolled");
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = $"User already has fingerprint enrolled in slot {user.FingerprintSlotId}"
                    };
                }

                // Get next available slot
                var availableSlot = await _biometricRepository.GetNextAvailableSlotAsync();
                if (!availableSlot.HasValue)
                {
                    await LogEventAsync(userId, "enrollment_failed", false, null, "No available slots");
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = "No available fingerprint slots"
                    };
                }

                // Check ESP32 connection
                if (!_esp32Service.IsConnected)
                {
                    await LogEventAsync(userId, "enrollment_failed", false, null, "ESP32 device not connected");
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = "Fingerprint device not connected"
                    };
                }

                // Log enrollment started
                await LogEventAsync(userId, "enrollment_started", true, availableSlot.Value);

                // Send command to ESP32
                var esp32Response = await _esp32Service.SendEnrollCommandAsync(availableSlot.Value, userId);

                if (!esp32Response.Success)
                {
                    await LogEventAsync(userId, "enrollment_failed", false, availableSlot.Value, esp32Response.Message);
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = esp32Response.Message
                    };
                }

                return new BiometricEnrollResponseDto
                {
                    Success = true,
                    SlotId = availableSlot.Value,
                    Message = $"Enrollment started for slot {availableSlot.Value}. Please scan your finger twice."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting enrollment for user {UserId}", userId);
                await LogEventAsync(userId, "enrollment_failed", false, null, ex.Message);
                throw;
            }
        }

        public async Task<BiometricEnrollResponseDto> CompleteEnrollmentAsync(int userId, int slotId, bool success, string? errorMessage = null)
        {
            try
            {
                if (success)
                {
                    // Assign slot to user in database
                    var assigned = await _biometricRepository.AssignFingerprintSlotAsync(userId, slotId);
                    
                    if (assigned)
                    {
                        await LogEventAsync(userId, "enrollment_completed", true, slotId);
                        _logger.LogInformation("Fingerprint enrollment completed for user {UserId} in slot {SlotId}", userId, slotId);
                        
                        return new BiometricEnrollResponseDto
                        {
                            Success = true,
                            SlotId = slotId,
                            Message = "Fingerprint enrolled successfully"
                        };
                    }
                    else
                    {
                        await LogEventAsync(userId, "enrollment_failed", false, slotId, "Failed to assign slot in database");
                        return new BiometricEnrollResponseDto
                        {
                            Success = false,
                            Message = "Failed to save fingerprint enrollment"
                        };
                    }
                }
                else
                {
                    await LogEventAsync(userId, "enrollment_failed", false, slotId, errorMessage);
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = errorMessage ?? "Enrollment failed"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing enrollment for user {UserId}", userId);
                await LogEventAsync(userId, "enrollment_failed", false, slotId, ex.Message);
                throw;
            }
        }

        // =====================================================
        // VERIFICATION OPERATIONS
        // =====================================================

        public async Task<BiometricVerifyResponseDto> StartVerificationAsync(int userId, int? quizId = null)
        {
            try
            {
                _logger.LogInformation("Starting fingerprint verification for user {UserId}", userId);

                // Get user's fingerprint slot
                var slotId = await _biometricRepository.GetFingerprintSlotByUserIdAsync(userId);
                
                if (!slotId.HasValue)
                {
                    await LogEventAsync(userId, "verification_failed", false, null, "User has no fingerprint enrolled");
                    return new BiometricVerifyResponseDto
                    {
                        Success = false,
                        Matched = false,
                        Message = "User has no fingerprint enrolled"
                    };
                }

                // Check ESP32 connection
                if (!_esp32Service.IsConnected)
                {
                    await LogEventAsync(userId, "verification_failed", false, slotId, "ESP32 device not connected");
                    return new BiometricVerifyResponseDto
                    {
                        Success = false,
                        Matched = false,
                        Message = "Fingerprint device not connected"
                    };
                }

                // Log verification started
                await LogEventAsync(userId, "verification_started", true, slotId.Value);

                // Send command to ESP32
                var esp32Response = await _esp32Service.SendVerifyCommandAsync(slotId.Value, userId);

                if (!esp32Response.Success)
                {
                    await LogEventAsync(userId, "verification_failed", false, slotId.Value, esp32Response.Message);
                    return new BiometricVerifyResponseDto
                    {
                        Success = false,
                        Matched = false,
                        Message = esp32Response.Message
                    };
                }

                return new BiometricVerifyResponseDto
                {
                    Success = true,
                    Matched = false, // Will be updated when verification completes
                    Message = "Verification started. Please scan your finger."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting verification for user {UserId}", userId);
                await LogEventAsync(userId, "verification_failed", false, null, ex.Message);
                throw;
            }
        }

        public async Task<BiometricVerifyResponseDto> CompleteVerificationAsync(int userId, bool matched, string? errorMessage = null)
        {
            try
            {
                var slotId = await _biometricRepository.GetFingerprintSlotByUserIdAsync(userId);

                if (matched)
                {
                    // Update last verified timestamp
                    await _biometricRepository.UpdateLastVerifiedAtAsync(userId);
                    await LogEventAsync(userId, "verification_completed", true, slotId);
                    
                    _logger.LogInformation("Fingerprint verification succeeded for user {UserId}", userId);
                    
                    return new BiometricVerifyResponseDto
                    {
                        Success = true,
                        Matched = true,
                        Message = "Fingerprint verified successfully"
                    };
                }
                else
                {
                    await LogEventAsync(userId, "verification_failed", false, slotId, errorMessage);
                    
                    return new BiometricVerifyResponseDto
                    {
                        Success = false,
                        Matched = false,
                        Message = errorMessage ?? "Fingerprint verification failed"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing verification for user {UserId}", userId);
                await LogEventAsync(userId, "verification_failed", false, null, ex.Message);
                throw;
            }
        }

        // =====================================================
        // UNENROLLMENT OPERATIONS
        // =====================================================

        public async Task<BiometricEnrollResponseDto> UnenrollFingerprintAsync(int userId, int requestedByUserId)
        {
            try
            {
                _logger.LogInformation("Unenrolling fingerprint for user {UserId}", userId);

                var slotId = await _biometricRepository.GetFingerprintSlotByUserIdAsync(userId);
                
                if (!slotId.HasValue)
                {
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = "User has no fingerprint enrolled"
                    };
                }

                var removed = await _biometricRepository.RemoveFingerprintSlotAsync(userId);
                
                if (removed)
                {
                    await LogEventAsync(userId, "unenrollment", true, slotId.Value);
                    
                    return new BiometricEnrollResponseDto
                    {
                        Success = true,
                        SlotId = slotId.Value,
                        Message = "Fingerprint unenrolled successfully"
                    };
                }
                else
                {
                    await LogEventAsync(userId, "unenrollment", false, slotId.Value, "Failed to remove from database");
                    
                    return new BiometricEnrollResponseDto
                    {
                        Success = false,
                        Message = "Failed to unenroll fingerprint"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unenrolling fingerprint for user {UserId}", userId);
                throw;
            }
        }

        // =====================================================
        // STATUS & INFORMATION
        // =====================================================

        public async Task<BiometricStatusDto> GetDeviceStatusAsync()
        {
            return await _esp32Service.GetDeviceStatusAsync();
        }

        public async Task<AvailableSlotsResponseDto> GetAvailableSlotsAsync()
        {
            var availableSlots = await _biometricRepository.GetAvailableSlotsAsync();
            
            return new AvailableSlotsResponseDto
            {
                AvailableSlots = availableSlots,
                TotalSlots = 127,
                UsedSlots = 127 - availableSlots.Count
            };
        }

        public async Task<bool> IsUserEnrolledAsync(int userId)
        {
            var slotId = await _biometricRepository.GetFingerprintSlotByUserIdAsync(userId);
            return slotId.HasValue;
        }

        public async Task<int?> GetUserFingerprintSlotAsync(int userId)
        {
            return await _biometricRepository.GetFingerprintSlotByUserIdAsync(userId);
        }

        // =====================================================
        // AUDIT LOGS
        // =====================================================

        public async Task<List<BiometricLogDto>> GetBiometricLogsAsync(BiometricLogFilterDto filter)
        {
            var logs = await _biometricRepository.GetBiometricLogsAsync(
                filter.UserId,
                filter.ActionType,
                filter.Success,
                filter.StartDate,
                filter.EndDate,
                filter.Page,
                filter.PageSize
            );

            if (!logs.Any())
                return new List<BiometricLogDto>();

            // Get all unique user IDs
            var userIds = logs.Select(l => l.UserId).Distinct().ToList();

            // Fetch users in batch (avoid N+1 query)
            var users = await _userRepository.GetByIdsAsync(userIds);
            var userMap = users.ToDictionary(u => u.UserId);

            // Map to DTOs
            return logs.Select(log => new BiometricLogDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = userMap.TryGetValue(log.UserId, out var user) ? user.FullName : "Unknown",
                ActionType = log.ActionType,
                Success = log.Success,
                SlotId = log.SlotId,
                ErrorMessage = log.ErrorMessage,
                IpAddress = log.IpAddress,
                CreatedAt = log.CreatedAt
            }).ToList();
        }

        public async Task<int> GetBiometricLogsCountAsync(BiometricLogFilterDto filter)
        {
            return await _biometricRepository.GetBiometricLogsCountAsync(
                filter.UserId,
                filter.ActionType,
                filter.Success,
                filter.StartDate,
                filter.EndDate
            );
        }

        // =====================================================
        // CANCEL OPERATION
        // =====================================================

        public async Task<bool> CancelCurrentOperationAsync()
        {
            try
            {
                var response = await _esp32Service.CancelOperationAsync();
                return response.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling current operation");
                return false;
            }
        }

        // =====================================================
        // HELPER METHODS
        // =====================================================

        private async Task LogEventAsync(int userId, string actionType, bool success, int? slotId = null, string? errorMessage = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                
                var log = new BiometricLog
                {
                    UserId = userId,
                    ActionType = actionType,
                    Success = success,
                    SlotId = slotId,
                    ErrorMessage = errorMessage,
                    IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                    CreatedAt = DateTime.UtcNow
                };

                await _biometricRepository.LogBiometricEventAsync(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging biometric event");
                // Don't throw - logging failure shouldn't break the main operation
            }
        }
    }
}
