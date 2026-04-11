namespace GWT.Application.DTOs.Funds;

public record AmfiFundRawDto(
    string SchemeCode,
    string SchemeName,
    string Amc,
    string? Isin,
    decimal Nav,
    DateTime NavDate
);
