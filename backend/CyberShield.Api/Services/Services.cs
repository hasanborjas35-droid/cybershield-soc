using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CyberShield.Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "CyberShield";
    public string Audience { get; set; } = "CyberShield.Clients";
    public int ExpiryMinutes { get; set; } = 60;
}

public class TokenService
{
    private readonly JwtOptions _opt;
    public TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> opt) => _opt = opt.Value;

    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", user.Id.ToString()),
            new System.Security.Claims.Claim("email", user.Email),
            new System.Security.Claims.Claim("name", user.FullName),
            new System.Security.Claims.Claim("role", user.Role.ToString()),
        };
        var jwt = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, DateTime.UtcNow, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

/// <summary>PBKDF2-SHA256 password hashing. Format: v1.{iterations}.{saltBase64}.{hashBase64}</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iter)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}

public static class QueryExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T>(items, total, page, pageSize);
    }
}

public static class Roles
{
    public const string Admin = nameof(UserRole.Administrator);
    public const string Analyst = nameof(UserRole.SecurityAnalyst);
    public const string Viewer = nameof(UserRole.Viewer);
    public const string AdminOrAnalyst = Admin + "," + Analyst;
}
