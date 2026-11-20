using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VehicleGo.Application;
using VehicleGo.Application.Abstractions;
using VehicleGo.Application.Auth;
using VehicleGo.Application.Me;
using VehicleGo.Application.Reference;
using VehicleGo.Application.Vehicles;

namespace VehicleGo.UnitTests.Application;

public class DependencyInjectionTests
{
    public static TheoryData<Type, Type> Handlers => new()
    {
        { typeof(ICommandHandler<EnsureUserCommand, Guid>), typeof(EnsureUserCommandHandler) },
        { typeof(ICommandHandler<CreateVehicleCommand, VehicleDto>), typeof(CreateVehicleCommandHandler) },
        { typeof(ICommandHandler<UpdateVehicleCommand, VehicleDto>), typeof(UpdateVehicleCommandHandler) },
        { typeof(ICommandHandler<TrashVehicleCommand>), typeof(TrashVehicleCommandHandler) },
        { typeof(ICommandHandler<RestoreVehicleCommand>), typeof(RestoreVehicleCommandHandler) },
        { typeof(ICommandHandler<DeleteVehicleCommand>), typeof(DeleteVehicleCommandHandler) },
        { typeof(ICommandHandler<UpdatePreferencesCommand, MeDto>), typeof(UpdatePreferencesCommandHandler) },
        { typeof(IQueryHandler<ListVehiclesQuery, IReadOnlyList<VehicleDto>>), typeof(ListVehiclesQueryHandler) },
        { typeof(IQueryHandler<GetVehicleQuery, VehicleDto>), typeof(GetVehicleQueryHandler) },
        { typeof(IQueryHandler<ListTrashQuery, IReadOnlyList<TrashItemDto>>), typeof(ListTrashQueryHandler) },
        { typeof(IQueryHandler<GetMeQuery, MeDto>), typeof(GetMeQueryHandler) },
        { typeof(IQueryHandler<GetDisplacementBracketsQuery, IReadOnlyList<DisplacementBracketDto>>), typeof(GetDisplacementBracketsQueryHandler) },
    };

    private static IServiceCollection Services() => new ServiceCollection().AddApplication();

    [Theory]
    [MemberData(nameof(Handlers))]
    public void 掃描組件註冊每個handler(Type service, Type implementation)
    {
        var descriptor = Assert.Single(Services(), d => d.ServiceType == service);

        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void 只註冊這些handler()
    {
        var handlerServices = Services()
            .Where(d => d.ServiceType.IsGenericType && IsHandlerInterface(d.ServiceType.GetGenericTypeDefinition()))
            .Select(d => d.ServiceType);

        Assert.Equal(Handlers.Select(row => row.Data.Item1).OrderBy(t => t.FullName), handlerServices.OrderBy(t => t.FullName));
    }

    [Fact]
    public void 註冊LoginAdmission() =>
        Assert.Contains(Services(), d => d.ServiceType == typeof(LoginAdmission) && d.Lifetime == ServiceLifetime.Scoped);

    [Fact]
    public void 只有新增和修改車輛的handler寫log()
    {
        var withLogger = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => typeof(ILogger).IsAssignableFrom(p.ParameterType))))
            .OrderBy(t => t.Name);

        Assert.Equal([typeof(CreateVehicleCommandHandler), typeof(UpdateVehicleCommandHandler)], withLogger);
    }

    private static bool IsHandlerInterface(Type definition) =>
        definition == typeof(ICommandHandler<>) || definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>);
}
