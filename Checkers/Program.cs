using Checkers.Core;
using Checkers.Hubs;
using Checkers.Services;
using Checkers.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy.WithOrigins(["https://localhost:3000", "https://checkersclient-jaz80p5ft-bohdans-projects-77abd7a4.vercel.app"]) // Дозволяємо тільки React
              .AllowAnyMethod()                     // GET, POST, PUT, DELETE...
              .AllowAnyHeader()                     // Content-Type, Authorization...
              .AllowCredentials();                  // <--- ВАЖЛИВО! Дозволяє передавати Cookies
    });
});
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSignalR();
builder.Services.AddSingleton<AppDbContext>();
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJWTService, JWTService>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IUserService, UserService>();
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var key = Encoding.UTF8.GetBytes(jwtSettings?.Key ?? "MySuperSecretKeyAEROSMITH6673INXS");
builder.Services.AddAuthentication(item =>
{
    item.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    item.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Для localhost
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true, // Перевіряти Issuer
        ValidIssuer = jwtSettings?.Issuer,
        ValidateAudience = true, // Перевіряти Audience (для кого)
        ValidAudience = jwtSettings?.Audience,
        ValidateLifetime = true, // Перевіряти чи не протух токен
        ClockSkew = TimeSpan.Zero // Забираємо затримку в 5 хв
    };

    // !!! МАГІЯ КУКІВ ТУТ !!!
    // За замовчуванням .NET шукає токен в Header. Ми вчимо його шукати в Cookie.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Якщо токен є в куках з ім'ям "accessToken", беремо його звідти
            if (context.Request.Cookies.ContainsKey("accessToken"))
            {
                context.Token = context.Request.Cookies["accessToken"];
            }
            return Task.CompletedTask;
        }
    };
});
var app = builder.Build();
app.UseCors("ReactPolicy");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");
app.Run();
