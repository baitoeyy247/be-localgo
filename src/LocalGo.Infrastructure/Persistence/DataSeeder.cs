using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Infrastructure.Persistence;

public static class DataSeeder
{
    private static readonly (string Name, string Slug)[] Categories =
    [
        ("อาหารและเครื่องดื่ม", "food-and-drink"),
        ("รับ-ส่งของ", "delivery"),
        ("ซักรีด", "laundry"),
        ("ติวเตอร์และสอนพิเศษ", "tutoring"),
        ("งานพิมพ์และเอกสาร", "printing-and-documents"),
        ("งานออกแบบและสร้างสรรค์", "design-and-creative"),
        ("ซ่อมอุปกรณ์อิเล็กทรอนิกส์", "device-repair"),
        ("ขนย้าย", "moving"),
        ("ทำความสะอาด", "cleaning"),
        ("สุขภาพและความงาม", "health-and-beauty"),
        ("หอพักและที่พักอาศัย", "dormitory"),
        ("บริการอื่น ๆ", "other"),
    ];

    public static async Task SeedAsync(LocalGoDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.ServiceCategories.ToListAsync(cancellationToken);
        var bySlug = existing.ToDictionary(c => c.Slug, c => c, StringComparer.OrdinalIgnoreCase);

        var order = 0;
        var mutated = false;
        foreach (var (name, slug) in Categories)
        {
            if (bySlug.TryGetValue(slug, out var category))
            {
                if (category.Name != name || category.SortOrder != order || !category.IsActive)
                {
                    category.Name = name;
                    category.SortOrder = order;
                    category.IsActive = true;
                    category.UpdatedAt = now;
                    mutated = true;
                }
            }
            else
            {
                db.ServiceCategories.Add(new ServiceCategory
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = slug,
                    SortOrder = order,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                mutated = true;
            }

            order++;
        }

        if (mutated)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
