namespace LocalGo.Api.Mapping;

public static class DependencyInjection
{
    public static IServiceCollection AddApiMapping(this IServiceCollection services) =>
        services.AddAutoMapper(_ => { }, typeof(BidProfile));
}
