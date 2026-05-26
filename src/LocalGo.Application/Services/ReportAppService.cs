using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class ReportAppService(ILocalGoDbContext db)
{
    public async Task<Report> CreateAsync(Guid userId, CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReportTargetType>(request.TargetType, true, out var targetType))
        {
            throw new AppException("Invalid target type.", 400);
        }

        if (!Enum.TryParse<ReportReason>(request.Reason, true, out var reason))
        {
            throw new AppException("Invalid reason.", 400);
        }

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            TargetType = targetType,
            TargetId = request.TargetId,
            Reason = reason,
            Description = request.Description,
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return report;
    }
}
