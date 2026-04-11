using FluentValidation;
using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/funds")]
public class FundsController : ControllerBase
{
    private readonly IFundService _funds;
    private readonly IValidator<EnsureFundRequestDto> _ensureValidator;

    public FundsController(IFundService funds, IValidator<EnsureFundRequestDto> ensureValidator)
    {
        _funds = funds;
        _ensureValidator = ensureValidator;
    }

    /// <summary>Full-text search for funds. region=INDIA returns AMFI mutual funds; region=GLOBAL returns Yahoo Finance results.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Region region = Region.INDIA,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var results = await _funds.SearchAsync(q, region, ct);
        // Wrap as { results: [...] } to match the frontend contract
        return Ok(new { results });
    }

    /// <summary>Get the latest NAV for a single fund by ticker.</summary>
    [HttpGet("nav/{ticker}")]
    public async Task<IActionResult> GetNav(
        string ticker,
        [FromQuery] Region region = Region.INDIA,
        CancellationToken ct = default)
    {
        var nav = await _funds.GetNavAsync(ticker, region, ct);
        return Ok(nav);
    }

    /// <summary>Upsert a fund into the catalogue (required before adding a holding).</summary>
    [HttpPost("ensure")]
    [Authorize]
    public async Task<IActionResult> EnsureFund([FromBody] EnsureFundRequestDto request, CancellationToken ct)
    {
        var validation = await _ensureValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { error = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)) });

        var result = await _funds.EnsureFundAsync(request, ct);
        return Ok(result);
    }
}
