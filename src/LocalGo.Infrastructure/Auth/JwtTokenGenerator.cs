using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LocalGo.Application.Abstractions.Auth;
using LocalGo.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LocalGo.Infrastructure.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtSettings> options) : IJwtTokenGenerator
{
    public string GenerateToken(User user)
    {
        var settings = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("lineUserId", user.LineUserId),
            new(ClaimTypes.Role, user.SystemRole.ToString()),
            new("systemRole", user.SystemRole.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(settings.ExpiresDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
