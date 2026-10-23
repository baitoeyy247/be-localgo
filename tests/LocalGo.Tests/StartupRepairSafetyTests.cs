using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Tests;

public sealed class StartupRepairSafetyTests
{
    private static User U(string line, string name) => new()
    {
        Id = Guid.NewGuid(), LineUserId = line, DisplayName = name,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static AdminAppService Admin(LocalGo.Infrastructure.Persistence.LocalGoDbContext db, string env) =>
        new(db, new ReviewAppService(db, TestDbFactory.NoopNotifications()), environmentName: env);

    [Theory]
    [InlineData("Staging", null, false)]
    [InlineData("Staging", "", false)]
    [InlineData("Staging", "false", false)]
    [InlineData("Development", null, false)]
    [InlineData("Staging", "true", true)]
    [InlineData("Production", "true", false)]
    public void Startup_repair_is_opt_in_and_never_in_production(string env, string? flag, bool expected) =>
        Assert.Equal(expected, StartupMaintenance.ShouldRunStartupRepair(env, flag));

    [Fact]
    public async Task Real_line_user_survives_repair_reset_and_purge()
    {
        await using var db = TestDbFactory.CreateContext();
        var real = U("U88eabcdef0123456789", "Baiitoeyyyyy");
        var realApptName = U("Uffff0000", "Appt Flow lookalike");
        var persona = U($"{AdminAppService.TestUserLinePrefix}req-01", "QA ผู้ขอ 01");
        var junk = U("dev-apptflow-1", "Appt Flow tester");
        db.Users.AddRange(real, realApptName, persona, junk);
        await db.SaveChangesAsync();

        var admin = Admin(db, "Staging");
        await admin.RepairSitPlaytestDataAsync(CancellationToken.None, seedSampleReviews: false);
        await admin.ResetTestDataAsync(CancellationToken.None);
        await admin.PurgeAutomatedTestUsersAsync(CancellationToken.None);

        var ids = await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
        Assert.Contains(real.Id, ids);
        Assert.Contains(realApptName.Id, ids);
        Assert.Contains(persona.Id, ids);
        Assert.DoesNotContain(junk.Id, ids);
    }

    [Fact]
    public async Task Production_refuses_destructive_maintenance()
    {
        await using var db = TestDbFactory.CreateContext();
        var real = U("U1234", "Real");
        db.Users.Add(real);
        await db.SaveChangesAsync();
        var admin = Admin(db, "Production");
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.RepairSitPlaytestDataAsync(CancellationToken.None, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.ResetTestDataAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.PurgeAutomatedTestUsersAsync(CancellationToken.None));
        Assert.True(await db.Users.AnyAsync(u => u.Id == real.Id));
    }
}
