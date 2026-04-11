using GWT.Application.DTOs.Funds;
using GWT.Domain.Enums;

namespace GWT.Application.Interfaces.Services;

public interface IFundService
{
    Task<List<FundSearchResultDto>> SearchAsync(string query, Region region, CancellationToken ct = default);
    Task<FundNavDto> GetNavAsync(string ticker, Region region, CancellationToken ct = default);
    Task<FundMetaDto> EnsureFundAsync(EnsureFundRequestDto request, CancellationToken ct = default);
}
