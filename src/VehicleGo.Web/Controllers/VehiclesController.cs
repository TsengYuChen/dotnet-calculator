using Microsoft.AspNetCore.Mvc;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Vehicles;
using VehicleGo.Web.Contracts;

namespace VehicleGo.Web.Controllers;

/// <summary>Vehicle endpoints (design §1.4). Each action calls exactly one handler.</summary>
[Route("api/vehicles")]
public sealed class VehiclesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? sort,
        [FromServices] IQueryHandler<ListVehiclesQuery, IReadOnlyList<VehicleDto>> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new ListVehiclesQuery(sort), cancellationToken), Ok);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetVehicleQuery, VehicleDto> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new GetVehicleQuery(id), cancellationToken), Ok);

    [HttpPost]
    public async Task<IActionResult> Create(
        VehicleInput input,
        [FromServices] ICommandHandler<CreateVehicleCommand, VehicleDto> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = await handler.HandleAsync(new CreateVehicleCommand(input.ToDetails()), cancellationToken);
        return Respond(result, vehicle => CreatedAtAction(nameof(Get), new { id = vehicle.Id }, vehicle));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateVehicleRequest request,
        [FromServices] ICommandHandler<UpdateVehicleCommand, VehicleDto> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await handler.HandleAsync(new UpdateVehicleCommand(id, request.Version!.Value, request.ToDetails()), cancellationToken);
        return Respond(result, Ok);
    }

    [HttpPost("{id:guid}/trash")]
    public async Task<IActionResult> Trash(
        Guid id,
        [FromServices] ICommandHandler<TrashVehicleCommand> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new TrashVehicleCommand(id), cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(
        Guid id,
        [FromServices] ICommandHandler<RestoreVehicleCommand> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new RestoreVehicleCommand(id), cancellationToken));

    /// <summary>Permanent deletion; only for a vehicle in the trash.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteVehicleCommand> handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.HandleAsync(new DeleteVehicleCommand(id), cancellationToken));
}
