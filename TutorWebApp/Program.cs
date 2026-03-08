using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutorApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 👇 ДОБАВЛЯЕМ CORS С РАЗРЕШЕНИЕМ null (для file://)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()  // Разрешает любые источники, включая null
              .AllowAnyMethod()
              .AllowAnyHeader();
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

// Swagger
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseCors("AllowAll");  

// 👇 ВКЛЮЧАЕМ SWAGGER В РАЗРАБОТКЕ
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tutor API V1");
        c.RoutePrefix = string.Empty; // Открывает Swagger по корневому URL (https://localhost:7122/)
    });
}

app.UseHttpsRedirection();
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "OK");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Проверяем, создана ли БД
    if (!dbContext.Database.CanConnect())
    {
        // Создаем только если БД не существует
        dbContext.Database.EnsureCreated();
    }

    // Инициализируем только если таблицы пустые
    if (!dbContext.Users.Any()) // проверьте свои таблицы
    {
        DbInitializer.Initialize(dbContext);
    }
}

app.Run();
