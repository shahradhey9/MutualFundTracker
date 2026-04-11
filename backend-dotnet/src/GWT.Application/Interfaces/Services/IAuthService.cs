using GWT.Application.DTOs.Auth;

namespace GWT.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
