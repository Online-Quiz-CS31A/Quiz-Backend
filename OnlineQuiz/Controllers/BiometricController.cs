using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BiometricController : ControllerBase
    {
        private readonly IBiometricService _biometricService;
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<BiometricController> _logger;

        public BiometricController(
            IBiometricService biometricService,
            IActivityLogService activityLogService,
            ILogger<BiometricController> logger)
        {
            _biometricService = biometricService;
            _activityLogService = activityLogService;
            _logger = logger;
        }

        // =====================================================
        // ENROLLMENT ENDPOINTS
        // =====================================================

        /// <summary>
        /// Start fingerprint enrollment for a user (Teacher/Admin only)
        /// </summary>
        /// <param name="userId">User ID to enroll</param>
        /// <returns>Enrollment status</returns>
        [HttpPost("enroll/{userId}")]
        [ProducesResponseType(typeof(BiometricEnrollResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BiometricEnrollResponseDto>> EnrollFingerprint(int userId)
        {
            try
            {
                // Get current user ID
                var currentUserId = JwtTokenGenerator.GetUserId(User);
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Check if user has permission (Teacher or Admin)
                var userRole = JwtTokenGenerator.GetUserRole(User);
                if (userRole != "Teacher" && userRole != "Admin")
                {
                    return Forbid();
                }

                var result = await _biometricService.StartEnrollmentAsync(userId, currentUserId.Value);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = currentUserId.Value,
                    Action = "ENROLL",
                    Entity = "Biometric",
                    EntityId = userId,
                    Description = $"Started fingerprint enrollment for user {userId}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enrolling fingerprint for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while enrolling fingerprint" });
            }
        }

        /// <summary>
        /// Remove fingerprint enrollment for a user (Teacher/Admin only)
        /// </summary>
        /// <param name="userId">User ID to unenroll</param>
        /// <returns>Unenrollment status</returns>
        [HttpDelete("unenroll/{userId}")]
        [ProducesResponseType(typeof(BiometricEnrollResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BiometricEnrollResponseDto>> UnenrollFingerprint(int userId)
        {
            try
            {
                // Get current user ID
                var currentUserId = JwtTokenGenerator.GetUserId(User);
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Check if user has permission (Teacher or Admin)
                var userRole = JwtTokenGenerator.GetUserRole(User);
                if (userRole != "Teacher" && userRole != "Admin")
                {
                    return Forbid();
                }

                var result = await _biometricService.UnenrollFingerprintAsync(userId, currentUserId.Value);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = currentUserId.Value,
                    Action = "DELETE",
                    Entity = "Biometric",
                    EntityId = userId,
                    Description = $"Removed fingerprint enrollment for user {userId}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unenrolling fingerprint for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while unenrolling fingerprint" });
            }
        }

        // =====================================================
        // VERIFICATION ENDPOINTS
        // =====================================================

        /// <summary>
        /// Start fingerprint verification for a user
        /// </summary>
        /// <param name="userId">User ID to verify</param>
        /// <param name="quizId">Optional quiz ID for context</param>
        /// <returns>Verification status</returns>
        [HttpPost("verify/{userId}")]
        [ProducesResponseType(typeof(BiometricVerifyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BiometricVerifyResponseDto>> VerifyFingerprint(int userId, [FromQuery] int? quizId = null)
        {
            try
            {
                // Get current user ID
                var currentUserId = JwtTokenGenerator.GetUserId(User);
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Users can only verify their own fingerprint (unless admin/teacher)
                var userRole = JwtTokenGenerator.GetUserRole(User);
                if (userId != currentUserId.Value && userRole != "Teacher" && userRole != "Admin")
                {
                    return Forbid();
                }

                var result = await _biometricService.StartVerificationAsync(userId, quizId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = currentUserId.Value,
                    Action = "VERIFY",
                    Entity = "Biometric",
                    EntityId = userId,
                    Description = $"Started fingerprint verification for user {userId}" + (quizId.HasValue ? $" (Quiz: {quizId})" : ""),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying fingerprint for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while verifying fingerprint" });
            }
        }

        // =====================================================
        // STATUS & INFORMATION ENDPOINTS
        // =====================================================

        /// <summary>
        /// Get ESP32 device status
        /// </summary>
        /// <returns>Device status</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(BiometricStatusDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<BiometricStatusDto>> GetDeviceStatus()
        {
            try
            {
                var status = await _biometricService.GetDeviceStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device status");
                return StatusCode(500, new { message = "An error occurred while getting device status" });
            }
        }

        /// <summary>
        /// Get available fingerprint slots (Teacher/Admin only)
        /// </summary>
        /// <returns>List of available slots</returns>
        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(AvailableSlotsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AvailableSlotsResponseDto>> GetAvailableSlots()
        {
            try
            {
                // Check if user has permission (Teacher or Admin)
                var userRole = JwtTokenGenerator.GetUserRole(User);
                if (userRole != "Teacher" && userRole != "Admin")
                {
                    return Forbid();
                }

                var slots = await _biometricService.GetAvailableSlotsAsync();
                return Ok(slots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available slots");
                return StatusCode(500, new { message = "An error occurred while getting available slots" });
            }
        }

        /// <summary>
        /// Check if a user has fingerprint enrolled
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>Enrollment status</returns>
        [HttpGet("is-enrolled/{userId}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> IsUserEnrolled(int userId)
        {
            try
            {
                // Get current user ID
                var currentUserId = JwtTokenGenerator.GetUserId(User);
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Users can only check their own status (unless admin/teacher)
                var userRole = JwtTokenGenerator.GetUserRole(User);
                if (userId != currentUserId.Value && userRole != "Teacher" && userRole != "Admin")
                {
                    return Forbid();
                }

                var isEnrolled = await _biometricService.IsUserEnrolledAsync(userId);
                var slotId = await _biometricService.GetUserFingerprintSlotAsync(userId);

                return Ok(new
                {
                    userId = userId,
                    isEnrolled = isEnrolled,
                    slotId = slotId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking enrollment status for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while checking enrollment status" });
            }
        }

        // =====================================================
        // AUDIT LOG ENDPOINTS
        // =====================================================

        /// <summary>
        /// Get biometric audit logs (Admin only, or own logs for users)
        /// </summary>
        /// <param name="filter">Filter parameters</param>
        /// <returns>List of biometric logs</returns>
        [HttpGet("logs")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetBiometricLogs([FromQuery] BiometricLogFilterDto filter)
        {
            try
            {
                // Get current user ID
                var currentUserId = JwtTokenGenerator.GetUserId(User);
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var userRole = JwtTokenGenerator.GetUserRole(User);

                // Non-admin users can only see their own logs
                if (userRole != "Admin" && userRole != "Teacher")
                {
                    filter.UserId = currentUserId.Value;
                }

                var logs = await _biometricService.GetBiometricLogsAsync(filter);
                var totalCount = await _biometricService.GetBiometricLogsCountAsync(filter);

                return Ok(new
                {
                    data = logs,
                    pagination = new
                    {
                        page = filter.Page,
                        pageSize = filter.PageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting biometric logs");
                return StatusCode(500, new { message = "An error occurred while getting biometric logs" });
            }
        }

        // =====================================================
        // CONTROL ENDPOINTS
        // =====================================================

        /// <summary>
        /// Cancel current biometric operation
        /// </summary>
        /// <returns>Cancellation status</returns>
        [HttpPost("cancel")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> CancelOperation()
        {
            try
            {
                var success = await _biometricService.CancelCurrentOperationAsync();
                
                return Ok(new
                {
                    success = success,
                    message = success ? "Operation cancelled" : "Failed to cancel operation"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling operation");
                return StatusCode(500, new { message = "An error occurred while cancelling operation" });
            }
        }
    }
}
