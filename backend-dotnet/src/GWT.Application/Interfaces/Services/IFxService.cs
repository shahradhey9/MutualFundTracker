namespace GWT.Application.Interfaces.Services;

public record FxRateDto(decimal Rate, string From, string To, bool IsLive);

public interface IFxService
{
    Task<FxRateDto> GetRateAsync(string from, string to, CancellationToken ct = default);
}
