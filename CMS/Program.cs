//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.EntityFrameworkCore;
//using System.Text;
//using LMS.Data;
//using QuestPDF.Infrastructure;
//using System.Text.Json.Serialization;
//using LMS.Services;

//var builder = WebApplication.CreateBuilder(args);

//// ✅ Enable community license for QuestPDF
//QuestPDF.Settings.License = LicenseType.Community;

//// ✅ Register controllers with cycle-safe JSON serialization
//builder.Services.AddControllers().AddJsonOptions(options =>
//{
//    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
//    options.JsonSerializerOptions.WriteIndented = true;
//});

//// ✅ Register the DbContext using SQL Server
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//// ✅ Configure JWT Authentication
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.RequireHttpsMetadata = false;
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = false,
//            ValidateAudience = false,
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(
//                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
//        };
//    });
//builder.Services.AddScoped<IFeeService, FeeService>();


//// ✅ Authorization policies for role-based access
//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
//    options.AddPolicy("InstructorOnly", policy => policy.RequireRole("Instructor"));
//    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
//});

//// ✅ Enable CORS for React app
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowReactApp", policy =>
//    {
//        policy.WithOrigins("http://localhost:3000", "https://lms.andhrauniversity-sde.com")
//              .AllowAnyHeader()
//              .AllowAnyMethod();
//    });
//});
//builder.Services.AddTransient<SqlScriptExecutor>();






//var app = builder.Build();
//using (var scope = app.Services.CreateScope())
//{
//    var executor = scope.ServiceProvider.GetRequiredService<SqlScriptExecutor>();
//    await executor.ExecuteAllSqlFilesAsync();
//}

//// ✅ Middleware Pipeline
//app.UseRouting();
//app.UseCors("AllowReactApp");
//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllers();
//app.UseStaticFiles(); // REQUIRED to serve wwwroot/*


//// ✅ Log route hits
//app.Use(async (context, next) =>
//{
//    Console.WriteLine($"➡️ Route hit: {context.Request.Method} {context.Request.Path}");
//    await next();
//});

//app.Run();



using System.Text;
using System.Text.Json.Serialization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LMS.Data;
using LMS.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ✅ Enable community license for QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// ✅ Register controllers with cycle-safe JSON serialization
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.WriteIndented = true;
});

// ✅ Register the DbContext using SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Configure JWT Authentication
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true, // ✅ must be true
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // For SignalR WebSocket (SessionHub)
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/sessionhub"))
                {
                    context.Token = accessToken;
                }
                else
                {
                    // Normal API auth via Authorization: Bearer <token>
                    var token = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(token) && token.StartsWith("Bearer "))
                    {
                        context.Token = token.Substring("Bearer ".Length);
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

// ✅ Global authorization: everything requires auth by default unless [AllowAnonymous]
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ✅ Your services
builder.Services.AddScoped<IFeeService, FeeService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddTransient<SqlScriptExecutor>();
builder.Services.AddHttpClient();

// ✅ CORS – simple, works for web + APK (capacitor)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()   // works with web + capacitor://localhost
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ✅ SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();

var app = builder.Build();

// (Optional) Swagger if you want it in dev
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

// ✅ Execute SQL scripts (if any) on startup
using (var scope = app.Services.CreateScope())
{
    var executor = scope.ServiceProvider.GetRequiredService<SqlScriptExecutor>();
    await executor.ExecuteAllSqlFilesAsync();
}

app.UseRouting();

// ✅ CORS before auth
app.UseCors("AllowAll");

// ✅ Auth
app.UseAuthentication();
app.UseAuthorization();

// ✅ Static files (wwwroot)
app.UseStaticFiles();

// ✅ Map controllers & SignalR hub
app.MapControllers();
app.MapHub<SessionHub>("/sessionhub");

app.Run();
