using GWT.Domain.Entities;

namespace GWT.Application.Interfaces.Repositories;

public interface IHoldingRepository
{
    Task<Holding?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Holding?> GetByUserAndFundAsync(Guid userId, string fundId, CancellationToken ct = default);
    Task<List<Holding>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Holding> CreateAsync(Holding holding, CancellationToken ct = default);
    Task<Holding> UpdateAsync(Holding holding, CancellationToken ct = default);
    Task DeleteAsync(Holding holding, CancellationToken ct = default);
}
