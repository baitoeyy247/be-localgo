using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class Qa25BudgetTests
{
    [Theory]
    [InlineData("เปิดรับข้อเสนอ")]
    [InlineData("เปิดรับราคา")]
    public void N10_open_budget_thai_labels_are_accepted(string budgetText)
    {
        FieldRules.RequireBudget(BudgetRules.NormalizeBudgetText(budgetText, null));
    }

    [Fact]
    public void N10_open_budget_type_code_is_accepted()
    {
        var normalized = BudgetRules.NormalizeBudgetText(null, BudgetRules.BudgetTypeOpen);
        FieldRules.RequireBudget(normalized);
        Assert.Equal(FieldRules.OpenBudgetCanonical, normalized);
    }

    [Fact]
    public async Task N10_create_request_with_default_open_budget_publishes()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenBidRequestAsync(db);
        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);

        var created = await sut.CreateAsync(
            seed.Requester.Id,
            new CreateServiceRequestDto(
                seed.Request.CategoryId,
                "QA25 default open budget",
                null,
                "Bangkok",
                13.7563,
                100.5018,
                5000,
                DateTime.UtcNow.AddDays(1),
                null,
                "เปิดรับข้อเสนอ",
                Publish: true,
                BudgetType: BudgetRules.BudgetTypeOpen),
            CancellationToken.None);

        Assert.Equal(FieldRules.OpenBudgetCanonical, created.BudgetText);
    }
}
