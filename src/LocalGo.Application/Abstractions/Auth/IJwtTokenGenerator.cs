using LocalGo.Domain.Entities;

namespace LocalGo.Application.Abstractions.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
