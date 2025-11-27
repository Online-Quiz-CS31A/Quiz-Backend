using DotNetEnv;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OnlineQuiz.Mappings;
using OnlineQuiz.Services;
using Scalar.AspNetCore;
using System.Text;
using System.Threading.RateLimiting;

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization for consistent API responses
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; // Use camelCase
        options.JsonSerializerOptions.WriteIndented = true; // Pretty print in development
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configure Supabase
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") 
    ?? throw new InvalidOperationException("SUPABASE_URL is not set in environment variables");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY") 
    ?? throw new InvalidOperationException("SUPABASE_KEY is not set in environment variables");

var supabaseService = new SupabaseService(supabaseUrl, supabaseKey);
await supabaseService.InitializeAsync();
builder.Services.AddSingleton(supabaseService);

// Configure JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new InvalidOperationException("JWT_SECRET is not set in environment variables");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "OnlineQuizAPI";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "OnlineQuizClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // Remove default 5-minute tolerance
    };

    // Configure to read JWT from cookie as well as Authorization header
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Check if token is in cookie
            if (context.Request.Cookies.ContainsKey("jwt"))
            {
                context.Token = context.Request.Cookies["jwt"];
                Console.WriteLine("JWT token retrieved from cookie");
            }
            // Otherwise it will be read from Authorization header by default
            else if (!string.IsNullOrEmpty(context.Request.Headers["Authorization"]))
            {
                Console.WriteLine("JWT token retrieved from Authorization header");
            }

            return Task.CompletedTask;
        }
    };
});

// Configure Mapster mappings
MapsterConfig.RegisterMappings();

// Configure Mapster
builder.Services.AddMapster();

// Register Repository Layer
builder.Services.AddScoped<OnlineQuiz.IRepository.IUserRepository, OnlineQuiz.Repository.UserRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IStudentRepository, OnlineQuiz.Repository.StudentRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.ITeacherRepository, OnlineQuiz.Repository.TeacherRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IUserRoleRepository, OnlineQuiz.Repository.UserRoleRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.ICourseRepository, OnlineQuiz.Repository.CourseRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IQuizRepository, OnlineQuiz.Repository.QuizRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IEnrollmentRepository, OnlineQuiz.Repository.EnrollmentRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IAuthRepository, OnlineQuiz.Repository.AuthRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IActivityLogRepository, OnlineQuiz.Repository.ActivityLogRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.INotificationRepository, OnlineQuiz.Repository.NotificationRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IExportImportLogRepository, OnlineQuiz.Repository.ExportImportLogRepository>();

// Register Service Layer
builder.Services.AddScoped<OnlineQuiz.IServices.IUserService, OnlineQuiz.Services.UserService>();
builder.Services.AddScoped<OnlineQuiz.IServices.ICourseService, OnlineQuiz.Services.CourseService>();
builder.Services.AddScoped<OnlineQuiz.IServices.IQuizService, OnlineQuiz.Services.QuizService>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IAttemptRepository, OnlineQuiz.Repository.AttemptRepository>();
builder.Services.AddScoped<OnlineQuiz.IRepository.IAttemptAnswerRepository, OnlineQuiz.Repository.AttemptAnswerRepository>();
builder.Services.AddScoped<OnlineQuiz.IServices.IAttemptService, OnlineQuiz.Services.AttemptService>();
builder.Services.AddScoped<OnlineQuiz.IServices.IAnswerService, OnlineQuiz.Services.AnswerService>();
builder.Services.AddScoped<OnlineQuiz.IServices.IAuthService, OnlineQuiz.Services.AuthService>();
builder.Services.AddScoped<OnlineQuiz.IServices.IActivityLogService, OnlineQuiz.Services.ActivityLogService>();
builder.Services.AddScoped<OnlineQuiz.IServices.INotificationService, OnlineQuiz.Services.NotificationService>();
builder.Services.AddScoped<OnlineQuiz.Services.IAnalyticsService, OnlineQuiz.Services.AnalyticsService>();
builder.Services.AddScoped<OnlineQuiz.IServices.IExportImportLogService, OnlineQuiz.Services.ExportImportLogService>();

// Register Background Services
builder.Services.AddHostedService<OnlineQuiz.Services.DeadlineReminderService>();

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiter - applies to all endpoints by default
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Extract user identifier (authenticated user ID or IP address)
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });

    // Authentication endpoints - stricter limits to prevent brute force
    options.AddPolicy("auth", context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            });
    });

    // File upload/export endpoints - lower limits for resource-intensive operations
    options.AddPolicy("file-operations", context =>
    {
        var userId = context.User?.FindFirst("userId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 3
            });
    });

    // Bulk operations - moderate limits
    options.AddPolicy("bulk-operations", context =>
    {
        var userId = context.User?.FindFirst("userId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
    });

    // Quiz attempt submissions - moderate limits
    options.AddPolicy("quiz-submission", context =>
    {
        var userId = context.User?.FindFirst("userId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
    });

    // Configure rejection response
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
            
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfterSeconds = retryAfter.TotalSeconds
            }, cancellationToken);
        }
        else
        {
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later."
            }, cancellationToken);
        }
    };
});

// Configure CORS for Web (Vue) and Mobile (Flutter)
var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',') ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebAndMobile", policy =>
    {
        policy.SetIsOriginAllowed(origin => 
            {
                // Allow localhost for development
                if (string.IsNullOrEmpty(origin)) return true;
                if (origin.StartsWith("http://localhost") || origin.StartsWith("https://localhost")) return true;
                
                // Check against configured allowed origins from environment variable
                foreach (var allowedOrigin in allowedOrigins)
                {
                    var trimmedOrigin = allowedOrigin.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedOrigin) && origin.Equals(trimmedOrigin, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                
                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Authorization", "Content-Type", "X-Total-Count");
    });
});

// Configure services for Scalar
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Online Quiz API", 
        Version = "v1",
        Description = "A comprehensive online quiz platform API powered by Supabase"
    });
    
    // Configure JWT Bearer authentication (for future use)
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure forwarded headers for AWS Elastic Beanstalk
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
// Enable Swagger/Scalar in all environments for API documentation
app.UseSwagger();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/swagger/{documentName}/swagger.json";
    options.WithTitle("Online Quiz API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.UseHttpsRedirection();

app.UseCors("AllowWebAndMobile");

// Enable rate limiting
app.UseRateLimiter();

// Add security headers
app.Use(async (context, next) =>
{
    // Security headers for web clients
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // API-specific headers
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    
    await next();
});

// IMPORTANT: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Redirect root path to Scalar API documentation
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.MapControllers();

app.Run();
