using Microsoft.AspNetCore.Mvc;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Reference;

namespace VehicleGo.Web.Controllers;

[Route("api/reference")]
public sealed class ReferenceController : ApiControllerBase
{
    [HttpGet("displacement-brackets")]
    public async Task<IActionResult> DisplacementBrackets(
        [FromServices] IQueryHandler<GetDisplacementBracketsQuery, IReadOnlyList<DisplacementBracketDto>> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new GetDisplacementBracketsQuery(), cancellationToken), Ok);
}
