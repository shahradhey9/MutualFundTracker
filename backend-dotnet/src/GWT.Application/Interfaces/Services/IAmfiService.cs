using GWT.Application.DTOs.Funds;

namespace GWT.Application.Interfaces.Services;

public interface IAmfiService
{
    Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
    Task<decimal?> GetNavAsync(string schemeCode, CancellationToken ct = default);
    Task<List<AmfiFundRawDto>> FetchAllNavsAsync(CancellationToken ct = default);
}
