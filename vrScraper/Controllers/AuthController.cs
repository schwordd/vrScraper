using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using vrScraper.Services;

namespace vrScraper.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ISettingService _settingService;
    private static readonly PasswordHasher<string> _hasher = new();

    public AuthController(ISettingService settingService)
    {
        _settingService = settingService;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status()
    {
        var setting = await _settingService.GetSetting("AuthPasswordHash");
        var hash = setting?.Value ?? "";
        return Ok(new { isSetup = !string.IsNullOrEmpty(hash), isAuthenticated = User.Identity?.IsAuthenticated ?? false });
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        var existing = await _settingService.GetSetting("AuthPasswordHash");
        if (!string.IsNullOrEmpty(existing?.Value))
            return BadRequest(new { error = "Password already set" });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
            return BadRequest(new { error = "Password must be at least 4 characters" });

        var hash = _hasher.HashPassword("admin", request.Password);
        await _settingService.SaveSetting("AuthPasswordHash", hash);

        // Auto-login after setup
        await SignIn();
        return Ok(new { success = true });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var setting = await _settingService.GetSetting("AuthPasswordHash");
        var hash = setting?.Value ?? "";
        if (string.IsNullOrEmpty(hash))
            return BadRequest(new { error = "No password set" });

        var result = _hasher.VerifyHashedPassword("admin", hash, request.Password ?? "");
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Wrong password" });

        // Re-hash if needed
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = _hasher.HashPassword("admin", request.Password!);
            await _settingService.SaveSetting("AuthPasswordHash", newHash);
        }

        await SignIn();
        return Ok(new { success = true });
    }

    [HttpGet("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var setting = await _settingService.GetSetting("AuthPasswordHash");
        var hash = setting?.Value ?? "";
        if (string.IsNullOrEmpty(hash))
            return BadRequest(new { error = "No password set" });

        var result = _hasher.VerifyHashedPassword("admin", hash, request.OldPassword ?? "");
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Wrong current password" });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            return BadRequest(new { error = "New password must be at least 4 characters" });

        var newHash = _hasher.HashPassword("admin", request.NewPassword);
        await _settingService.SaveSetting("AuthPasswordHash", newHash);
        return Ok(new { success = true });
    }

    private async Task SignIn()
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }

    public record LoginRequest(string? Password);
    public record SetupRequest(string? Password);
    public record ChangePasswordRequest(string? OldPassword, string? NewPassword);
}
