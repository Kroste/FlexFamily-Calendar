using System.Security.Claims;
using System.Text;
using FlexFamilyCalendar.Api.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlexFamilyCalendar.Api.Auth;

/// <summary>Erstellt signierte JWTs (HS256) für angemeldete Benutzer.</summary>
public class TokenService(IConfiguration config)
{
    public string Create(UserEntity user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
            })
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
