using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OnlineQuiz.Utilities
{
    public static class JwtTokenGenerator
    {
        /// <summary>
        /// Generate a JWT token with user claims
        /// </summary>
        public static string GenerateToken(
            int userId,
            string email,
            int roleId,
            string roleName,
            string secretKey,
            string issuer,
            string audience,
            int expirationHours)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("RoleId", roleId.ToString()),
                new Claim("RoleName", roleName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validate a JWT token and return the ClaimsPrincipal
        /// </summary>
        public static ClaimsPrincipal? ValidateToken(
            string token,
            string secretKey,
            string issuer,
            string audience)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a specific claim value from ClaimsPrincipal
        /// </summary>
        public static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
        {
            return principal.FindFirst(claimType)?.Value;
        }

        /// <summary>
        /// Extract user ID from ClaimsPrincipal
        /// </summary>
        public static int? GetUserId(ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// Extract role ID from ClaimsPrincipal
        /// </summary>
        public static int? GetRoleId(ClaimsPrincipal principal)
        {
            var roleIdClaim = principal.FindFirst("RoleId")?.Value;
            if (int.TryParse(roleIdClaim, out int roleId))
            {
                return roleId;
            }
            return null;
        }

        /// <summary>
        /// Extract role name from ClaimsPrincipal
        /// </summary>
        public static string? GetUserRole(ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Role)?.Value 
                ?? principal.FindFirst("RoleName")?.Value;
        }
    }
}
