using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class CategoryAppService(ILocalGoDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.ServiceCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description))
            .ToListAsync(cancellationToken);
}
