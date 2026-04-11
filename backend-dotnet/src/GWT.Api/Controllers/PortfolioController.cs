using FluentValidation;
using GWT.Application.DTOs.Portfolio;
using GWT.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolio;
    private readonly IValidator<AddHoldingRequestDto> _addHoldingValidator;

    public PortfolioController(
        IPortfolioService portfolio,
        IValidator<AddHoldingRequestDto> addHoldingValidator)
    {
        _portfolio = portfolio;
        _addHoldingValidator = addHoldingValidator;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get the authenticated user's full portfolio with live NAVs.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPortfolio(CancellationToken ct)
    {
        var items = await _portfolio.GetPortfolioAsync(CurrentUserId, ct);
        // Wrap as { holdings: [...] } to match the frontend contract
        return Ok(new { holdings = items });
    }

    /// <summary>Add a new holding or consolidate into an existing position for the same fund.</summary>
    [HttpPost("holdings")]
    public async Task<IActionResult> AddHolding([FromBody] AddHoldingRequestDto request, CancellationToken ct)
    {
        var validation = await _addHoldingValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { error = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)) });

        var holding = await _portfolio.UpsertHoldingAsync(CurrentUserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, holding);
    }

    /// <summary>Partially update an existing holding (units, avgCost, purchaseAt).</summary>
    [HttpPatch("holdings/{id:guid}")]
    public async Task<IActionResult> UpdateHolding(Guid id, [FromBody] UpdateHoldingRequestDto request, CancellationToken ct)
    {
        var holding = await _portfolio.UpdateHoldingAsync(CurrentUserId, id, request, ct);
        return Ok(holding);
    }

    /// <summary>Delete a holding.</summary>
    [HttpDelete("holdings/{id:guid}")]
    public async Task<IActionResult> DeleteHolding(Guid id, CancellationToken ct)
    {
        await _portfolio.DeleteHoldingAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
