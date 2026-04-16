using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;

namespace OnlineQuiz.Services
{
    public class MockESP32Service : IESP32Service
    {
        private readonly ILogger<MockESP32Service> _logger;
        private readonly IConfiguration _configuration;
        private bool _isConnected;
        private string _currentMode = "Idle"; // Idle, Enrollment, Verification
        private int? _activeSlotId;
        private int? _activeUserId; // Track which user is being enrolled/verified

        // Configuration
        private readonly double _successRate;
        private readonly int _delaySeconds;

        public MockESP32Service(ILogger<MockESP32Service> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _isConnected = true; // Mock is always "connected"

            // Load configuration
            _successRate = _configuration.GetValue<double>("Biometric:MockSuccessRate", 0.9);
            _delaySeconds = _configuration.GetValue<int>("Biometric:MockDelaySeconds", 3);

            _logger.LogInformation("MockESP32Service initialized (Success Rate: {SuccessRate}, Delay: {Delay}s)", 
                _successRate, _delaySeconds);
        }

        // Events
        public event EventHandler<ESP32ResponseDto>? OnEnrollmentCompleted;
        public event EventHandler<ESP32ResponseDto>? OnVerificationCompleted;
        public event EventHandler<string>? OnDeviceStatusChanged;

        public bool IsConnected => _isConnected;

        // =====================================================
        // CONNECTION MANAGEMENT
        // =====================================================

        public async Task<bool> ConnectAsync()
        {
            _logger.LogInformation("Mock ESP32: Connecting...");
            await Task.CompletedTask;
            _isConnected = true;
            OnDeviceStatusChanged?.Invoke(this, "Connected");
            return true;
        }

        public async Task DisconnectAsync()
        {
            _logger.LogInformation("Mock ESP32: Disconnecting...");
            await Task.CompletedTask;
            _isConnected = false;
            _currentMode = "Idle";
            OnDeviceStatusChanged?.Invoke(this, "Disconnected");
        }

        // =====================================================
        // COMMAND METHODS
        // =====================================================

        public async Task<ESP32ResponseDto> SendEnrollCommandAsync(int slotId, int userId)
        {
            _logger.LogInformation("Mock ESP32: Starting enrollment for slot {SlotId}, user {UserId}", slotId, userId);
            
            if (!_isConnected)
            {
                return new ESP32ResponseDto
                {
                    Success = false,
                    Message = "Device not connected",
                    ErrorCode = 1001
                };
            }

            _currentMode = "Enrollment";
            _activeSlotId = slotId;
            _activeUserId = userId;
            OnDeviceStatusChanged?.Invoke(this, "Enrollment");

            // Simulate async hardware operation
            _ = Task.Run(async () =>
            {
                try
                {
                    // Simulate first scan
                    await Task.Delay(TimeSpan.FromSeconds(_delaySeconds / 2.0));
                    _logger.LogInformation("Mock ESP32: First finger scan captured for slot {SlotId}", slotId);

                    // Simulate second scan
                    await Task.Delay(TimeSpan.FromSeconds(_delaySeconds / 2.0));
                    _logger.LogInformation("Mock ESP32: Second finger scan captured for slot {SlotId}", slotId);

                    // Simulate processing and result
                    var success = new Random().NextDouble() < _successRate;
                    
                    var response = new ESP32ResponseDto
                    {
                        Success = success,
                        UserId = userId,
                        SlotId = slotId,
                        Message = success 
                            ? $"Fingerprint enrolled successfully in slot {slotId}" 
                            : "Enrollment failed - poor scan quality or sensor error",
                        ErrorCode = success ? null : 2001,
                        Timestamp = DateTime.UtcNow
                    };

                    _logger.LogInformation(success 
                        ? "Mock ESP32: Enrollment succeeded for slot {SlotId}" 
                        : "Mock ESP32: Enrollment failed for slot {SlotId}", slotId);

                    _currentMode = "Idle";
                    _activeSlotId = null;
                    _activeUserId = null;
                    OnDeviceStatusChanged?.Invoke(this, "Idle");
                    OnEnrollmentCompleted?.Invoke(this, response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mock ESP32: Error during enrollment simulation");
                    _currentMode = "Idle";
                    _activeSlotId = null;
                    _activeUserId = null;
                }
            });

            return new ESP32ResponseDto
            {
                Success = true,
                Message = "Enrollment started - waiting for finger scans",
                UserId = userId,
                SlotId = slotId
            };
        }

        public async Task<ESP32ResponseDto> SendVerifyCommandAsync(int slotId, int userId)
        {
            _logger.LogInformation("Mock ESP32: Starting verification for slot {SlotId}, user {UserId}", slotId, userId);
            
            if (!_isConnected)
            {
                return new ESP32ResponseDto
                {
                    Success = false,
                    Message = "Device not connected",
                    ErrorCode = 1001
                };
            }

            _currentMode = "Verification";
            _activeSlotId = slotId;
            _activeUserId = userId;
            OnDeviceStatusChanged?.Invoke(this, "Verification");

            // Simulate async hardware operation
            _ = Task.Run(async () =>
            {
                try
                {
                    // Simulate finger scan and verification
                    await Task.Delay(TimeSpan.FromSeconds(_delaySeconds));
                    _logger.LogInformation("Mock ESP32: Finger scan captured for verification against slot {SlotId}", slotId);

                    // Simulate verification result
                    var success = new Random().NextDouble() < _successRate;
                    
                    var response = new ESP32ResponseDto
                    {
                        Success = success,
                        UserId = userId,
                        SlotId = slotId,
                        Message = success 
                            ? $"Fingerprint matched with slot {slotId}" 
                            : "Fingerprint does not match or sensor error",
                        ErrorCode = success ? null : 3001,
                        Timestamp = DateTime.UtcNow
                    };

                    _logger.LogInformation(success 
                        ? "Mock ESP32: Verification succeeded for slot {SlotId}" 
                        : "Mock ESP32: Verification failed for slot {SlotId}", slotId);

                    _currentMode = "Idle";
                    _activeSlotId = null;
                    _activeUserId = null;
                    OnDeviceStatusChanged?.Invoke(this, "Idle");
                    OnVerificationCompleted?.Invoke(this, response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mock ESP32: Error during verification simulation");
                    _currentMode = "Idle";
                    _activeSlotId = null;
                    _activeUserId = null;
                }
            });

            return new ESP32ResponseDto
            {
                Success = true,
                Message = "Verification started - waiting for finger scan",
                UserId = userId,
                SlotId = slotId
            };
        }

        public Task<ESP32ResponseDto> CancelOperationAsync()
        {
            _logger.LogInformation("Mock ESP32: Cancelling current operation");
            
            _currentMode = "Idle";
            _activeSlotId = null;
            _activeUserId = null;
            OnDeviceStatusChanged?.Invoke(this, "Idle");

            return Task.FromResult(new ESP32ResponseDto
            {
                Success = true,
                Message = "Operation cancelled"
            });
        }

        public Task<BiometricStatusDto> GetDeviceStatusAsync()
        {
            return Task.FromResult(new BiometricStatusDto
            {
                IsConnected = _isConnected,
                CurrentMode = _currentMode,
                ActiveUserId = _activeUserId
            });
        }
    }
}
