using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Shared.Auth;

public static class JwtAuthExtensions
{
    /// <summary>Validates HMAC JWTs issued by Identity. Used by Catalog, Order, Gateway.</summary>
    public static WebApplicationBuilder AddCommerceJwtAuth(this WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
        {
            // Identity issues explicit ClaimTypes.NameIdentifier + email claims; downstream
            // services read those via User.FindFirstValue(...). Default inbound mapping keeps
            // them intact, so NameClaimType/RoleClaimType need no override here.
            // ClockSkew left at the 5-minute default — fine for this demo.
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = cfg["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = cfg["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(cfg["Jwt:Secret"]!)),
                ValidateLifetime = true
            };
        });
        builder.Services.AddAuthorization();
        return builder;
    }
}
