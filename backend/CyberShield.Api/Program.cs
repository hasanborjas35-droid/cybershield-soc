using System.Text;
using System.Text.Json.Serialization;
using CyberShield.Api.Data;
using CyberShield.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Serve the CyberShield front-end (the "public" folder of the project) from the same origin as the API.
// Override with Frontend__Path; falls back to ./wwwroot when the folder is not found.
var probe = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "public"));
var frontendPath = Environment.GetEnvironmentVariable("Frontend__Path") ?? (Directory.Exists(probe) ? probe : null);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = frontendPath });
var config = builder.Configuration;

// ---------- Database (SQL Server) ----------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure(3)));

// ---------- JWT authentication ----------
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set (>= 32 characters). Use the Jwt__Key environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (frontend origins) ----------
var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- MVC + Swagger ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CyberShield SOC API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Paste the token returned by POST /api/auth/login",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>(),
    });
});

var app = builder.Build();

// ---------- Create / migrate DB and seed ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (config.GetValue("Database:AutoMigrate", true)) await db.Database.MigrateAsync();
    if (config.GetValue("Database:Seed", true))
        await DbSeeder.SeedAsync(db, config["Database:SeedPassword"] ?? "ChangeMe!123");
}

if (config.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CyberShield SOC API v1");
        // Pre-fill the login endpoint so testers can get a token easily
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseCors("Frontend");       // CORS must be before auth + routing
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503))
   .AllowAnonymous();

app.Run();
