using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IActivityLogService _activityLogService;

        public AuthController(IAuthService authService, IActivityLogService activityLogService)
        {
            _authService = authService;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Login with email and password
        /// </summary>
        /// <param name="loginRequest">Login credentials</param>
        /// <returns>User details and JWT token</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto loginRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var loginResponse = await _authService.LoginAsync(loginRequest);

                if (loginResponse == null)
                {
                    return Unauthorized(new { error = "Invalid email or password" });
                }

                // Store JWT token in HTTP-only cookie for web clients
                // Determine if the request is secure (handling proxies like AWS ELB)
                var isHttps = Request.IsHttps;
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true, // Prevents JavaScript access (XSS protection)
                    Secure = isHttps, // Only send over HTTPS
                    // CRITICAL: SameSite=None requires Secure=true (HTTPS)
                    // For HTTP (localhost), use Lax instead
                    SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                    Path = "/", // Make cookie available for all paths
                    Expires = loginResponse.TokenExpiration
                };

                Response.Cookies.Append("jwt", loginResponse.Token, cookieOptions);
                Console.WriteLine($"JWT token stored in cookie for user: {loginResponse.User.Email}");

                // Log the LOGIN activity
                try
                {
                    await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                    {
                        UserId = loginResponse.User.UserId,
                        Action = ActivityLogConstants.Actions.LOGIN,
                        Entity = ActivityLogConstants.Entities.Auth,
                        Description = $"User {loginResponse.User.Email} logged in successfully",
                        NewValues = new { Email = loginResponse.User.Email, Role = loginResponse.User.RoleName },
                        IpAddress = ActivityLogHelper.GetIpAddress(HttpContext),
                        UserAgent = ActivityLogHelper.GetUserAgent(HttpContext)
                    });
                }
                catch (Exception logEx)
                {
                    // Don't fail the login if logging fails
                    Console.WriteLine($"Failed to log LOGIN activity: {logEx.Message}");
                }

                return Ok(loginResponse);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("inactive"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred during login", details = ex.Message });
            }
        }

        /// <summary>
        /// Logout (client-side token removal)
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout()
        {
            // Extract user ID from JWT token (if present and valid)
            var userId = JwtTokenGenerator.GetUserId(User);

            // Delete the JWT cookie
            Response.Cookies.Delete("jwt");
            Console.WriteLine("JWT cookie deleted");

            // Log the LOGOUT activity (only if we have a valid user ID)
            if (userId.HasValue && userId.Value > 0)
            {
                try
                {
                    await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                    {
                        UserId = userId.Value,
                        Action = ActivityLogConstants.Actions.LOGOUT,
                        Entity = ActivityLogConstants.Entities.Auth,
                        Description = "User logged out",
                        IpAddress = ActivityLogHelper.GetIpAddress(HttpContext),
                        UserAgent = ActivityLogHelper.GetUserAgent(HttpContext)
                    });
                }
                catch (Exception logEx)
                {
                    // Don't fail the logout if logging fails
                    Console.WriteLine($"Failed to log LOGOUT activity: {logEx.Message}");
                }
            }
            
            return Ok(new { message = "Logout successful. Token removed from cookies." });
        }

        /// <summary>
        /// Verify JWT token and get current user details
        /// </summary>
        /// <returns>Current user details</returns>
        [HttpGet("verify-me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserResponseDto>> VerifyMe()
        {
            try
            {
                // Log all claims for debugging
                var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                Console.WriteLine("=== JWT Claims ===");
                foreach (var claim in claims)
                {
                    Console.WriteLine(claim);
                }
                Console.WriteLine("==================");

                // Extract user ID from JWT token claims
                var userId = JwtTokenGenerator.GetUserId(User);

                if (userId == null)
                {
                    Console.WriteLine("ERROR: Could not extract UserId from token claims");
                    return Unauthorized(new { 
                        error = "Invalid token claims", 
                        details = "UserId claim not found in token",
                        availableClaims = claims
                    });
                }

                Console.WriteLine($"Extracted UserId: {userId}");

                // Get user details
                var user = await _authService.GetCurrentUserAsync(userId.Value);

                if (user == null)
                {
                    Console.WriteLine($"ERROR: User {userId} not found in database");
                    return NotFound(new { error = "User not found" });
                }

                Console.WriteLine($"Successfully retrieved user: {user.Email}");
                return Ok(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in VerifyMe: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { 
                    error = "An error occurred while verifying the token", 
                    details = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
        /// <summary>
        /// Change password
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                var userId = JwtTokenGenerator.GetUserId(User);
                if (userId == null)
                {
                    return Unauthorized(new { error = "Invalid token" });
                }

                await _authService.ChangePasswordAsync(userId.Value, changePasswordDto);

                // Log activity
                try
                {
                    await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                    {
                        UserId = userId.Value,
                        Action = ActivityLogConstants.Actions.UPDATE,
                        Entity = ActivityLogConstants.Entities.Auth,
                        Description = "User changed password",
                        IpAddress = ActivityLogHelper.GetIpAddress(HttpContext),
                        UserAgent = ActivityLogHelper.GetUserAgent(HttpContext)
                    });
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"Failed to log password change: {logEx.Message}");
                }

                return Ok(new { message = "Password changed successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while changing password", details = ex.Message });
            }
        }
    }
}
