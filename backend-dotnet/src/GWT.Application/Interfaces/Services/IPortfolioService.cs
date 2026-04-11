using GWT.Application.DTOs.Portfolio;

namespace GWT.Application.Interfaces.Services;

public interface IPortfolioService
{
    Task<List<PortfolioItemDto>> GetPortfolioAsync(Guid userId, CancellationToken ct = default);
    Task<HoldingDto> UpsertHoldingAsync(Guid userId, AddHoldingRequestDto request, CancellationToken ct = default);
    Task<HoldingDto> UpdateHoldingAsync(Guid userId, Guid holdingId, UpdateHoldingRequestDto request, CancellationToken ct = default);
    Task DeleteHoldingAsync(Guid userId, Guid holdingId, CancellationToken ct = default);
}
