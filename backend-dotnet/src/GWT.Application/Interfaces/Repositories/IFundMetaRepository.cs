using GWT.Domain.Entities;
using GWT.Domain.Enums;

namespace GWT.Application.Interfaces.Repositories;

public interface IFundMetaRepository
{
    Task<FundMeta?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<FundMeta?> GetByTickerAsync(string ticker, CancellationToken ct = default);
    Task<List<FundMeta>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<List<FundMeta>> GetAllHeldFundsAsync(CancellationToken ct = default);
    Task<FundMeta> UpsertAsync(FundMeta fund, CancellationToken ct = default);
    Task UpdateNavBatchAsync(IEnumerable<(string FundId, decimal Nav, DateTime NavDate)> updates, CancellationToken ct = default);
}
