using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VehicleGo.Application.Abstractions;
using VehicleGo.Infrastructure.Crypto;
using VehicleGo.Infrastructure.DataProtection;
using VehicleGo.Infrastructure.Persistence;
using VehicleGo.Infrastructure.Persistence.Queries;
using VehicleGo.Infrastructure.Persistence.Repositories;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Infrastructure;

public class InfrastructureDependencyInjectionTests
{
    private static ServiceProvider Build() =>
        InfrastructureServices.Build(AppDbContextFactory.DesignTimeConnectionString, new FakeCurrentUser());

    [Fact]
    public void 註冊Application的對外介面實作()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<VehicleRepository>(services.GetRequiredService<IVehicleRepository>());
        Assert.IsType<UserRepository>(services.GetRequiredService<IUserRepository>());
        Assert.IsType<VehicleQueries>(services.GetRequiredService<IVehicleQueries>());
        Assert.IsType<UnitOfWork>(services.GetRequiredService<IUnitOfWork>());
        Assert.IsType<EmailProtector>(services.GetRequiredService<IEmailProtector>());
        Assert.IsType<RuleTableProvider>(services.GetRequiredService<IRuleTableProvider>());
        Assert.NotNull(services.GetRequiredService<AppDbContext>());
        Assert.NotNull(services.GetRequiredService<DataProtectionKeyContext>());
    }

    [Fact]
    public void Repository和UnitOfWork共用同一個DbContext()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void DataProtection_用DbContext存金鑰_AES_GCM加密_應用程式名稱VehicleGo()
    {
        using var provider = Build();

        var keyManagement = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.IsType<AesGcmXmlEncryptor>(keyManagement.XmlEncryptor);
        Assert.IsType<EntityFrameworkCoreXmlRepository<DataProtectionKeyContext>>(keyManagement.XmlRepository);
        Assert.Equal("VehicleGo", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void 規則表是目前的規則表()
    {
        using var provider = Build();

        Assert.Same(VehicleGo.Domain.Inspections.Rules.RuleTable.Current(), provider.GetRequiredService<IRuleTableProvider>().GetRuleTable());
    }

    [Fact]
    public void MASTER_KEY不合法時解析失敗()
    {
        using var provider = InfrastructureServices.Build(AppDbContextFactory.DesignTimeConnectionString, new FakeCurrentUser(), masterKey: "short");

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MasterKey>());
    }
}
