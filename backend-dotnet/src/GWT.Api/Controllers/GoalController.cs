using GWT.Application.DTOs.Goals;
using GWT.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/goals")]
[Authorize]
public class GoalController : ControllerBase
{
    private readonly IGoalService _goals;

    public GoalController(IGoalService goals) => _goals = goals;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetGoals(CancellationToken ct)
    {
        var goals = await _goals.GetGoalsAsync(CurrentUserId, ct);
        return Ok(new { goals });
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequestDto request, CancellationToken ct)
    {
        var goal = await _goals.CreateGoalAsync(CurrentUserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, goal);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateGoal(Guid id, [FromBody] UpdateGoalRequestDto request, CancellationToken ct)
    {
        var goal = await _goals.UpdateGoalAsync(CurrentUserId, id, request, ct);
        return Ok(goal);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken ct)
    {
        await _goals.DeleteGoalAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
