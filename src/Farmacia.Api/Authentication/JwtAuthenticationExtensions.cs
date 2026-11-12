using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Farmacia.Api.Authentication;

internal static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Validation uses the same JwtOptions (and signing key) that Infrastructure uses to issue tokens.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names as they are in the token ("sub", "role") instead of the long legacy URIs.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    // Only the algorithm we sign with: blocks algorithm-switching attacks such as "alg": "none".
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    // The default 5 minutes would turn a 15-minute token into a 20-minute one.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = ClaimNames.Role,
                };

                // Browsers cannot send headers when opening a WebSocket, so the SignalR client sends the access token
                // in the query string. Accepted only on the hub's path, never on the REST endpoints.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(Realtime.PharmacyHub.Path)
                            && context.Request.Query["access_token"] is [{ Length: > 0 } token])
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(Policies.Configure);
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PasswordChangedHandler>();

        // The current pharmacy (tenant) is read from the token of each request.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPharmacy, HttpContextCurrentPharmacy>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}
