using CyberShield.Api.Data;
using CyberShield.Api.Dtos;
using CyberShield.Api.Models;
using CyberShield.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberShield.Api.Controllers;

public static class UserMapping
{
    public static UserDto ToDto(this User u) => new(
        u.Id, u.FullName, u.Email, u.Role, u.Status, u.MfaEnabled,
        u.Permissions.Select(p => p.Code).OrderBy(p => p).ToArray(), u.LastActiveAt, u.CreatedAt);

    public static string[] DefaultPermissions(UserRole role) => role switch
    {
        UserRole.Administrator => new[] { "IAM:ALL", "NET:CTL", "SIEM:ALL", "INC:ALL" },
        UserRole.SecurityAnalyst => new[] { "SIEM:RD", "INC:WR" },
        _ => new[] { "AUDIT:RD" },
    };
}

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    public AuthController(AppDbContext db, TokenService tokens) { _db = db; _tokens = tokens; }

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Email == email);
        // Same response for unknown user / wrong password to avoid account enumeration.
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });
        if (user.Status != UserStatus.Active)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = $"Account is {user.Status}." });

        user.LastActiveAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var (token, exp) = _tokens.Create(user);
        return new LoginResponse(token, exp, user.ToDto());
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = int.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? Unauthorized() : user.ToDto();
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.AdminOrAnalyst)]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        [FromQuery] UserRole? role, [FromQuery] UserStatus? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var q = _db.Users.AsNoTracking().Include(u => u.Permissions).AsQueryable();
        if (role is not null) q = q.Where(u => u.Role == role);
        if (status is not null) q = q.Where(u => u.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        var paged = await q.OrderBy(u => u.FullName).ToPagedAsync(page, pageSize);
        return new PagedResult<UserDto>(paged.Items.Select(u => u.ToDto()).ToList(), paged.Total, paged.Page, paged.PageSize);
    }

    [HttpGet("summary")]
    public async Task<UserSummaryDto> Summary()
    {
        var users = _db.Users.AsNoTracking();
        return new UserSummaryDto(
            await users.CountAsync(),
            await users.CountAsync(u => u.Role == UserRole.Administrator),
            await users.CountAsync(u => u.Role == UserRole.SecurityAnalyst && u.Status == UserStatus.Active),
            await users.CountAsync(u => u.Status != UserStatus.Active));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var u = await _db.Users.AsNoTracking().Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id);
        return u is null ? NotFound() : u.ToDto();
    }

    /// <summary>Provision a new user. Returns a one-time temporary password; the account starts as Pending.</summary>
    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<CreateUserResponse>> Create(CreateUserRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "A user with this email already exists." });

        var temp = PasswordHasher.GenerateTemporaryPassword();
        var user = new User
        {
            FullName = req.FullName.Trim(), Email = email, Role = req.Role, Status = UserStatus.Pending,
            PasswordHash = PasswordHasher.Hash(temp), MfaEnabled = true,
            Permissions = (req.Permissions is { Length: > 0 } ? req.Permissions : UserMapping.DefaultPermissions(req.Role))
                .Distinct().Select(p => new UserPermission { Code = p }).ToList(),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new CreateUserResponse(user.ToDto(), temp));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest req)
    {
        var user = await _db.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        user.FullName = req.FullName.Trim();
        user.Role = req.Role;
        user.MfaEnabled = req.MfaEnabled;
        if (req.Permissions is not null)
        {
            _db.UserPermissions.RemoveRange(user.Permissions);
            user.Permissions = req.Permissions.Distinct().Select(p => new UserPermission { Code = p }).ToList();
        }
        await _db.SaveChangesAsync();
        return user.ToDto();
    }

    [HttpPatch("{id:int}/status"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserDto>> SetStatus(int id, UpdateStatusRequest<UserStatus> req)
    {
        var user = await _db.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (user.Id == int.Parse(User.FindFirst("sub")!.Value) && req.Status != UserStatus.Active)
            return BadRequest(new { error = "You cannot suspend your own account." });
        user.Status = req.Status;
        await _db.SaveChangesAsync();
        return user.ToDto();
    }

    [HttpPost("{id:int}/reset-password"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<object>> ResetPassword(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        var temp = PasswordHasher.GenerateTemporaryPassword();
        user.PasswordHash = PasswordHasher.Hash(temp);
        await _db.SaveChangesAsync();
        return new { temporaryPassword = temp };
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == int.Parse(User.FindFirst("sub")!.Value))
            return BadRequest(new { error = "You cannot delete your own account." });
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        // Alerts.AssignedToUserId uses NoAction in SQL Server, so clear assignments first.
        await _db.Alerts.Where(a => a.AssignedToUserId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.AssignedToUserId, (int?)null));
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
