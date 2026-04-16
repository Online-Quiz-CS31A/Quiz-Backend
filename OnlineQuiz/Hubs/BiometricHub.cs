using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;

namespace OnlineQuiz.Hubs
{
    [Authorize]
    public class BiometricHub : Hub
    {
        private readonly IBiometricService _biometricService;
        private readonly ILogger<BiometricHub> _logger;

        public BiometricHub(IBiometricService biometricService, ILogger<BiometricHub> logger)
        {
            _biometricService = biometricService;
            _logger = logger;
        }

        // =====================================================
        // CONNECTION MANAGEMENT
        // =====================================================

        public override async Task OnConnectedAsync()
        {
            var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                // Add user to their personal group for targeted notifications
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userIdClaim}");
                _logger.LogInformation("BiometricHub: User {UserId} connected with connection ID {ConnectionId} and added to group", userIdClaim, Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning("BiometricHub: Connection {ConnectionId} has no userId claim", Context.ConnectionId);
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                // Remove user from their personal group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userIdClaim}");
                _logger.LogInformation("BiometricHub: User {UserId} disconnected with connection ID {ConnectionId}", userIdClaim, Context.ConnectionId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        // =====================================================
        // CLIENT → SERVER METHODS
        // =====================================================

        /// <summary>
        /// Request fingerprint enrollment for a user (Teacher/Admin only)
        /// </summary>
        public async Task RequestEnrollment(int userId)
        {
            try
            {
                // Get current user ID
                var currentUserIdStr = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out int currentUserId))
                {
                    _logger.LogWarning("BiometricHub: Enrollment request rejected - invalid or missing userId claim");
                    await Clients.Caller.SendAsync("EnrollmentFailed", new
                    {
                        userId = userId,
                        message = "Unauthorized: Invalid authentication"
                    });
                    return;
                }

                // Check if user has permission (Teacher or Admin only)
                var userRole = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (userRole != "Teacher" && userRole != "Admin")
                {
                    _logger.LogWarning("BiometricHub: Enrollment request rejected - user {CurrentUserId} with role {Role} attempted to enroll user {UserId}", 
                        currentUserId, userRole ?? "None", userId);
                    await Clients.Caller.SendAsync("EnrollmentFailed", new
                    {
                        userId = userId,
                        message = "Unauthorized: Only teachers and admins can enroll fingerprints"
                    });
                    return;
                }

                _logger.LogInformation("BiometricHub: Enrollment requested for user {UserId} by {CurrentUserId} (Role: {Role})", 
                    userId, currentUserId, userRole);

                var result = await _biometricService.StartEnrollmentAsync(userId, currentUserId);

                if (result.Success)
                {
                    // Send to specific user only
                    var userGroup = $"user_{userId}";
                    await Clients.Group(userGroup).SendAsync("EnrollmentStarted", new
                    {
                        userId = userId,
                        slotId = result.SlotId,
                        message = result.Message
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("EnrollmentFailed", new
                    {
                        userId = userId,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricHub: Error requesting enrollment for user {UserId}", userId);
                await Clients.Caller.SendAsync("EnrollmentFailed", new
                {
                    userId = userId,
                    message = "An error occurred while starting enrollment"
                });
            }
        }

        /// <summary>
        /// Request fingerprint verification for a user
        /// </summary>
        public async Task RequestVerification(int userId, int? quizId = null)
        {
            try
            {
                // Get current user ID
                var currentUserIdStr = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out int currentUserId))
                {
                    _logger.LogWarning("BiometricHub: Verification request rejected - invalid or missing userId claim");
                    await Clients.Caller.SendAsync("VerificationFailed", new
                    {
                        userId = userId,
                        matched = false,
                        message = "Unauthorized: Invalid authentication"
                    });
                    return;
                }

                // Authorization: Users can verify themselves, or Teacher/Admin can verify anyone
                var userRole = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (userId != currentUserId && userRole != "Teacher" && userRole != "Admin")
                {
                    _logger.LogWarning("BiometricHub: Verification request rejected - user {CurrentUserId} attempted to verify user {UserId}", 
                        currentUserId, userId);
                    await Clients.Caller.SendAsync("VerificationFailed", new
                    {
                        userId = userId,
                        matched = false,
                        message = "Unauthorized: You can only verify your own fingerprint"
                    });
                    return;
                }

                _logger.LogInformation("BiometricHub: Verification requested for user {UserId} by {CurrentUserId}", userId, currentUserId);

                var result = await _biometricService.StartVerificationAsync(userId, quizId);

                if (result.Success)
                {
                    // Send to specific user only
                    var userGroup = $"user_{userId}";
                    await Clients.Group(userGroup).SendAsync("VerificationStarted", new
                    {
                        userId = userId,
                        quizId = quizId,
                        message = result.Message
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("VerificationFailed", new
                    {
                        userId = userId,
                        matched = false,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricHub: Error requesting verification for user {UserId}", userId);
                await Clients.Caller.SendAsync("VerificationFailed", new
                {
                    userId = userId,
                    matched = false,
                    message = "An error occurred while starting verification"
                });
            }
        }

        /// <summary>
        /// Cancel current biometric operation
        /// </summary>
        public async Task CancelOperation()
        {
            try
            {
                var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("BiometricHub: Cancel operation requested by user {UserId}", currentUserId);

                var success = await _biometricService.CancelCurrentOperationAsync();

                // Send only to the caller who requested cancellation
                await Clients.Caller.SendAsync("OperationCancelled", new
                {
                    success = success,
                    message = success ? "Operation cancelled" : "Failed to cancel operation"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricHub: Error cancelling operation");
                await Clients.Caller.SendAsync("Error", new
                {
                    message = "An error occurred while cancelling operation"
                });
            }
        }

        /// <summary>
        /// Get current device status
        /// </summary>
        public async Task GetDeviceStatus()
        {
            try
            {
                var status = await _biometricService.GetDeviceStatusAsync();

                await Clients.Caller.SendAsync("DeviceStatus", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricHub: Error getting device status");
                await Clients.Caller.SendAsync("Error", new
                {
                    message = "An error occurred while getting device status"
                });
            }
        }

        // =====================================================
        // SERVER → CLIENT EVENTS (called by BiometricListenerService)
        // =====================================================
        // These methods are called by the background service to broadcast events:
        // - EnrollmentStarted
        // - EnrollmentProgress (e.g., "Place finger again")
        // - EnrollmentCompleted
        // - EnrollmentFailed
        // - VerificationStarted
        // - VerificationCompleted
        // - VerificationFailed
        // - DeviceStatusChanged
    }
}
