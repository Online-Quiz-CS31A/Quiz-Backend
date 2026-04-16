using Microsoft.AspNetCore.SignalR;
using OnlineQuiz.DTOs;
using OnlineQuiz.Hubs;
using OnlineQuiz.IServices;

namespace OnlineQuiz.Services
{
    public class BiometricListenerService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BiometricListenerService> _logger;
        private IHubContext<BiometricHub>? _hubContext;
        private IESP32Service? _esp32Service;
        private IServiceScope? _scope;

        public BiometricListenerService(
            IServiceProvider serviceProvider,
            ILogger<BiometricListenerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BiometricListenerService starting...");

            // Create a scope to resolve scoped services and keep it alive
            _scope = _serviceProvider.CreateScope();
            _hubContext = _scope.ServiceProvider.GetRequiredService<IHubContext<BiometricHub>>();
            _esp32Service = _scope.ServiceProvider.GetRequiredService<IESP32Service>();

            // Subscribe to ESP32 events
            _esp32Service.OnEnrollmentCompleted += HandleEnrollmentCompleted;
            _esp32Service.OnVerificationCompleted += HandleVerificationCompleted;
            _esp32Service.OnDeviceStatusChanged += HandleDeviceStatusChanged;

            _logger.LogInformation("BiometricListenerService started and subscribed to ESP32 events");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BiometricListenerService stopping...");

            // Unsubscribe from ESP32 events
            if (_esp32Service != null)
            {
                _esp32Service.OnEnrollmentCompleted -= HandleEnrollmentCompleted;
                _esp32Service.OnVerificationCompleted -= HandleVerificationCompleted;
                _esp32Service.OnDeviceStatusChanged -= HandleDeviceStatusChanged;
            }

            // Dispose the scope
            _scope?.Dispose();

            _logger.LogInformation("BiometricListenerService stopped");

            return Task.CompletedTask;
        }

        // =====================================================
        // EVENT HANDLERS
        // =====================================================

        private async void HandleEnrollmentCompleted(object? sender, ESP32ResponseDto response)
        {
            try
            {
                _logger.LogInformation("BiometricListenerService: Enrollment completed - Success: {Success}, Slot: {SlotId}, UserId: {UserId}", 
                    response.Success, response.SlotId, response.UserId);

                // Create a new scope to get BiometricService
                using var scope = _serviceProvider.CreateScope();
                var biometricService = scope.ServiceProvider.GetRequiredService<IBiometricService>();

                // Complete enrollment in database
                if (response.UserId.HasValue && response.SlotId.HasValue)
                {
                    await biometricService.CompleteEnrollmentAsync(
                        response.UserId.Value, 
                        response.SlotId.Value, 
                        response.Success, 
                        response.Message);
                }

                // Send SignalR notification to SPECIFIC USER only (not broadcast)
                if (_hubContext != null && response.UserId.HasValue)
                {
                    var userGroup = $"user_{response.UserId.Value}";
                    
                    if (response.Success)
                    {
                        await _hubContext.Clients.Group(userGroup).SendAsync("EnrollmentCompleted", new
                        {
                            success = true,
                            userId = response.UserId,
                            slotId = response.SlotId,
                            message = response.Message
                        });
                    }
                    else
                    {
                        await _hubContext.Clients.Group(userGroup).SendAsync("EnrollmentFailed", new
                        {
                            success = false,
                            userId = response.UserId,
                            slotId = response.SlotId,
                            message = response.Message,
                            errorCode = response.ErrorCode
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricListenerService: Error handling enrollment completed event");
            }
        }

        private async void HandleVerificationCompleted(object? sender, ESP32ResponseDto response)
        {
            try
            {
                _logger.LogInformation("BiometricListenerService: Verification completed - Success: {Success}, Slot: {SlotId}, UserId: {UserId}", 
                    response.Success, response.SlotId, response.UserId);

                // Send SignalR notification to SPECIFIC USER only (not broadcast)
                if (_hubContext != null && response.UserId.HasValue)
                {
                    var userGroup = $"user_{response.UserId.Value}";
                    
                    if (response.Success)
                    {
                        await _hubContext.Clients.Group(userGroup).SendAsync("VerificationCompleted", new
                        {
                            success = true,
                            matched = true,
                            userId = response.UserId,
                            slotId = response.SlotId,
                            message = response.Message
                        });
                    }
                    else
                    {
                        await _hubContext.Clients.Group(userGroup).SendAsync("VerificationFailed", new
                        {
                            success = false,
                            matched = false,
                            userId = response.UserId,
                            slotId = response.SlotId,
                            message = response.Message,
                            errorCode = response.ErrorCode
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricListenerService: Error handling verification completed event");
            }
        }

        private async void HandleDeviceStatusChanged(object? sender, string status)
        {
            try
            {
                _logger.LogInformation("BiometricListenerService: Device status changed to {Status}", status);

                // Device status is not user-specific, but still limit to authenticated users
                // This broadcasts to all connected clients (device availability is public info)
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("DeviceStatusChanged", new
                    {
                        status = status,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BiometricListenerService: Error handling device status changed event");
            }
        }
    }
}
