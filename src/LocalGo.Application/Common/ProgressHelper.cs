using LocalGo.Domain.Entities;

namespace LocalGo.Application.Common;

public static class ProgressHelper
{
    public static void Touch(ServiceRequest request)
    {
        request.LastProgressAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
    }
}
