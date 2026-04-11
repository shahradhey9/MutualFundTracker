namespace GWT.Application.DTOs.Auth;

public record RegisterRequestDto(string Email, string Password, string? Name);
