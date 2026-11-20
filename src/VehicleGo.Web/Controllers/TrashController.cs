using Microsoft.AspNetCore.Mvc;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Vehicles;

namespace VehicleGo.Web.Controllers;

[Route("api/trash")]
public sealed class TrashController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListTrashQuery, IReadOnlyList<TrashItemDto>> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new ListTrashQuery(), cancellationToken), Ok);
}
