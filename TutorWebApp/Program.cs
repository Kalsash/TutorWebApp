using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutorApi.Data;
using TutorWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 👇 УНИВЕРСАЛЬНЫЙ CORS - поддерживает всё
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            // 1. Всегда разрешаем null и file:// (для локальной разработки)
            if (origin == "null" || origin.StartsWith("file://"))
                return true;

            // 2. Все локальные адреса для разработки
            if (origin.Contains("localhost") ||
                origin.Contains("127.0.0.1") ||
                origin.StartsWith("http://localhost") ||
                origin.StartsWith("https://localhost"))
                return true;

            // 3. ДОМЕНЫ НА RENDER
            if (origin.Contains("tutor-api-web.onrender.com") ||
                origin.Contains("tutor-api.onrender.com") ||
                origin.Contains("tutor-server.onrender.com"))
                return true;

            // 4. 👇 НОВОЕ: GitHub Pages
            if (origin.Contains("kalsash.github.io") ||
                origin.StartsWith("https://kalsash.github.io") ||
                origin.EndsWith(".github.io"))  // на случай других проектов
                return true;

            // 5. Для любых origins в development режиме
            if (builder.Environment.IsDevelopment())
                return true;

            // Если ничего не подошло - отклоняем
            return false;
        })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // 👈 ВАЖНО: добавляем поддержку кук/авторизации
    });
});

// ДОБАВЛЯЕМ SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tutor API",
        Version = "v1",
        Description = "API для репетитора по информатике"
    });

    //Добавляем поддержку JWT в Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IValidationService, ValidationService>();


// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

var app = builder.Build();
app.UseCors("AllowAll");  

// 👇 ВКЛЮЧАЕМ SWAGGER В РАЗРАБОТКЕ
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tutor API V1");
        c.RoutePrefix = string.Empty;
    });
}

// ВАЖНО: Убираем UseHttpsRedirection на Render
// Render сам терминирует HTTPS
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "OK");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(dbContext);
}

app.Run();