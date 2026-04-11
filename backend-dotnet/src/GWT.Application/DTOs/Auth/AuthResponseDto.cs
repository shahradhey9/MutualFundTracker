namespace GWT.Application.DTOs.Auth;

public record AuthResponseDto(string Token, UserProfileDto User);

public record UserProfileDto(Guid Id, string Email, string? Name, DateTime CreatedAt);
