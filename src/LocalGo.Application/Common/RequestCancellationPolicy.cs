using LocalGo.Domain.Enums;

namespace LocalGo.Application.Common;

/// <summary>F-N3: single source of truth for when a requester may cancel a job (mirrored in FE request-cancel-policy.ts).</summary>
public static class RequestCancellationPolicy
{
    public static bool CanCancel(ServiceRequestStatus requestStatus, AppointmentStatus? appointmentStatus)
    {
        if (appointmentStatus is AppointmentStatus.InProgress or AppointmentStatus.Completed)
        {
            return false;
        }

        return requestStatus is ServiceRequestStatus.Draft
            or ServiceRequestStatus.Open
            or ServiceRequestStatus.HasOffers
            or ServiceRequestStatus.ProviderSelected
            or ServiceRequestStatus.Scheduled;
    }
}
