using healthapp.Context;
using healthapp.Interfaces;
using healthapp.Middlewares;
using healthapp.Repositories;
using healthapp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. CORS Ayarı
builder.Services.AddCors(options => {
    options.AddPolicy("HealthAppPolicy", policy => {
        var origins = builder.Configuration["CORS_ORIGINS"]?.Split(',') ?? Array.Empty<string>();
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2. JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_ACCESS_SECRET"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// 3. Rate Limiting
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("api", opt => {
        opt.Window = TimeSpan.FromMinutes(15);
        opt.PermitLimit = 100;
    });
});

// 4. Database Connection
builder.Services.AddDbContext<PostgresContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Döngüsel referansları (Circular Reference) görmezden gel
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // İsteğe bağlı: JSON çıktısını daha okunaklı yapmak için
        options.JsonSerializerOptions.WriteIndented = true; 
    });
builder.Services.AddSwaggerGen(option =>
{
    // Opsiyonel: API Başlığı ve Versiyonu
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Health App API", Version = "v1" });

    // 1. Security Definition: Swagger'a JWT kullanacağımızı tanımlıyoruz
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Lütfen geçerli bir token giriniz",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    // 2. Security Requirement: Bu güvenliği tüm endpointlere uyguluyoruz
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});
builder.Services.AddOpenApi();

// --- EKLENEN KISIM: DEPENDENCY INJECTION KAYITLARI ---

// Services
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddTransient<IEmailService, EmailService>(); // Email servisi stateless olduğu için Transient uygundur

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<ISpecialityRepository, SpecialityRepository>();

// Background Services (Cron Job Muadili)
builder.Services.AddHostedService<AppointmentReminderService>();

// -----------------------------------------------------

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("HealthAppPolicy");
app.UseRateLimiter();

// Security Headers
app.Use(async (context, next) => {
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
if (!Directory.Exists(uploadPath))
{
    Directory.CreateDirectory(uploadPath);
}

app.Run();