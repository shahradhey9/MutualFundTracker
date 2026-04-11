using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GWT.Application.DTOs.Auth;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GWT.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository users, IConfiguration config, ILogger<AuthService> logger)
    {
        _users = users;
        _config = config;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            Name = request.Name,
            PasswordHash = HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        await _users.CreateAsync(user, ct);
        _logger.LogInformation("New user registered: {Email}", user.Email);

        var token = GenerateJwt(user);
        return new AuthResponseDto(token, MapToProfile(user));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        _logger.LogInformation("User logged in: {Email}", user.Email);
        return new AuthResponseDto(GenerateJwt(user), MapToProfile(user));
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
                   ?? throw new KeyNotFoundException("User not found.");
        return MapToProfile(user);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string GenerateJwt(User user)
    {
        var secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiry = int.TryParse(_config["Jwt:ExpiryDays"], out var days) ? days : 7;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 350_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string? storedHash)
    {
        if (storedHash is null) return false;
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 350_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static UserProfileDto MapToProfile(User user) =>
        new(user.Id, user.Email, user.Name, user.CreatedAt);
}
