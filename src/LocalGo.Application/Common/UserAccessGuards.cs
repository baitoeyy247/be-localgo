using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;

namespace LocalGo.Application.Common;

public static class UserAccessGuards
{
    public static void EnsureCanSignIn(User user)
    {
        if (user.Status == UserStatus.Suspended)
        {
            throw new AppException("บัญชีนี้ถูกระงับชั่วคราว ติดต่อผู้ดูแลระบบ", 403);
        }
    }

    public static void EnsureCanUseApi(User user)
    {
        EnsureCanSignIn(user);
    }
}
