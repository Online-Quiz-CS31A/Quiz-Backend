using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;

namespace OnlineQuiz.Repository
{
    public class BiometricRepository : IBiometricRepository
    {
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<BiometricRepository> _logger;

        public BiometricRepository(SupabaseService supabaseService, ILogger<BiometricRepository> logger)
        {
            _supabaseService = supabaseService;
            _logger = logger;
        }

        // =====================================================
        // FINGERPRINT SLOT MANAGEMENT
        // =====================================================

        public async Task<bool> AssignFingerprintSlotAsync(int userId, int slotId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.UserId == userId)
                    .Set(u => u.FingerprintSlotId, slotId)
                    .Set(u => u.FingerprintEnrolledAt, DateTime.UtcNow)
                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning fingerprint slot {SlotId} to user {UserId}", slotId, userId);
                return false;
            }
        }

        public async Task<int?> GetFingerprintSlotByUserIdAsync(int userId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.UserId == userId)
                    .Single();

                return response?.FingerprintSlotId ?? null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fingerprint slot for user {UserId}", userId);
                return null;
            }
        }

        public async Task<User?> GetUserByFingerprintSlotAsync(int slotId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.FingerprintSlotId == slotId)
                    .Single();

                return response ?? null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by fingerprint slot {SlotId}", slotId);
                return null;
            }
        }

        public async Task<bool> RemoveFingerprintSlotAsync(int userId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.UserId == userId)
                    .Set(u => u.FingerprintSlotId, (int?)null)
                    .Set(u => u.FingerprintEnrolledAt, (DateTime?)null)
                    .Set(u => u.FingerprintLastVerifiedAt, (DateTime?)null)
                    .Update();

                return response?.Models.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing fingerprint slot for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> IsSlotAvailableAsync(int slotId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.FingerprintSlotId == slotId)
                    .Get();

                return response?.Models?.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if slot {SlotId} is available", slotId);
                return false;
            }
        }

        public async Task<List<int>> GetAvailableSlotsAsync()
        {
            try
            {
                var client = _supabaseService.GetClient();
                try
                {
                    var rpcResult = await client.Rpc("get_available_fingerprint_slots", null);
                    if (rpcResult?.Content != null)
                    {
                        var slots = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, int>>>(rpcResult.Content);
                        return slots?.Select(s => s["slot_id"]).ToList() ?? new List<int>();
                    }
                }
                catch (Exception rpcEx)
                {
                    _logger.LogWarning(rpcEx, "RPC function not available");
                }
                _logger.LogWarning("Using fallback method");
                
                var response = await client
                    .From<User>()
                    .Get();

                var usedSlots = (response?.Models ?? new List<User>())
                    .Where(u => u.FingerprintSlotId.HasValue)
                    .Select(u => u.FingerprintSlotId!.Value)
                    .ToHashSet();

                // Generate list of available slots (1-127)
                var availableSlots = Enumerable.Range(1, 127)
                    .Where(slot => !usedSlots.Contains(slot))
                    .ToList();

                return availableSlots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available fingerprint slots");
                return new List<int>();
            }
        }

        public async Task<int?> GetNextAvailableSlotAsync()
        {
            try
            {
                var client = _supabaseService.GetClient();
                try
                {
                    var rpcResult = await client.Rpc("get_next_available_fingerprint_slot", null);
                    if (rpcResult?.Content != null)
                    {
                        var slotId = System.Text.Json.JsonSerializer.Deserialize<int?>(rpcResult.Content);
                        return slotId;
                    }
                }
                catch (Exception rpcEx)
                {
                    _logger.LogWarning(rpcEx, "RPC function not available");
                }
                
                // Fallback
                var availableSlots = await GetAvailableSlotsAsync();
                
                if (availableSlots.Count == 0)
                {
                    return null;
                }
                
                return availableSlots.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next available fingerprint slot");
                throw;
            }
        }

        public async Task<bool> UpdateLastVerifiedAtAsync(int userId)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<User>()
                    .Where(u => u.UserId == userId)
                    .Set(u => u.FingerprintLastVerifiedAt, DateTime.UtcNow)
                    .Update();

                return response?.Models.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last verified timestamp for user {UserId}", userId);
                return false;
            }
        }

        // =====================================================
        // BIOMETRIC LOG MANAGEMENT
        // =====================================================

        public async Task<int> LogBiometricEventAsync(BiometricLog log)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
                var response = await client
                    .From<BiometricLog>()
                    .Insert(log, options);

                return response.Models.FirstOrDefault()?.Id ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging biometric event for user {UserId}", log.UserId);
                throw;
            }
        }

        public async Task<List<BiometricLog>> GetBiometricLogsAsync(
            int? userId = null,
            string? actionType = null,
            bool? success = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                var client = _supabaseService.GetClient();
                
                // Start with base query
                var query = client
                    .From<BiometricLog>()
                    .Order("CreatedAt", Postgrest.Constants.Ordering.Descending);

                // Build filter query - Supabase doesn't support chaining, so fetch and filter in memory
                // This is a limitation of the Supabase C# client
                var response = await query.Get();
                var logs = response.Models.AsEnumerable();

                // Apply filters in memory (Supabase C# client limitation)
                if (userId.HasValue)
                    logs = logs.Where(l => l.UserId == userId.Value);

                if (!string.IsNullOrEmpty(actionType))
                    logs = logs.Where(l => l.ActionType == actionType);

                if (success.HasValue)
                    logs = logs.Where(l => l.Success == success.Value);

                if (startDate.HasValue)
                    logs = logs.Where(l => l.CreatedAt >= startDate.Value);

                if (endDate.HasValue)
                    logs = logs.Where(l => l.CreatedAt <= endDate.Value);

                // Apply pagination
                var offset = (page - 1) * pageSize;
                return logs.Skip(offset).Take(pageSize).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting biometric logs");
                throw;
            }
        }

        public async Task<int> GetBiometricLogsCountAsync(
            int? userId = null,
            string? actionType = null,
            bool? success = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            try
            {
                var client = _supabaseService.GetClient();
                
                // Get all logs and filter in memory
                var response = await client
                    .From<BiometricLog>()
                    .Get();

                var logs = response.Models.AsEnumerable();

                // Apply filters in memory
                if (userId.HasValue)
                    logs = logs.Where(l => l.UserId == userId.Value);

                if (!string.IsNullOrEmpty(actionType))
                    logs = logs.Where(l => l.ActionType == actionType);

                if (success.HasValue)
                    logs = logs.Where(l => l.Success == success.Value);

                if (startDate.HasValue)
                    logs = logs.Where(l => l.CreatedAt >= startDate.Value);

                if (endDate.HasValue)
                    logs = logs.Where(l => l.CreatedAt <= endDate.Value);

                return logs.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting biometric logs count");
                throw;
            }
        }

        public async Task<BiometricLog?> GetBiometricLogByIdAsync(int id)
        {
            try
            {
                var client = _supabaseService.GetClient();
                var response = await client
                    .From<BiometricLog>()
                    .Where(l => l.Id == id)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting biometric log {LogId}", id);
                throw;
            }
        }
    }
}
