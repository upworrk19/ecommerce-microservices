using Commerce.Identity.Auth;
using Commerce.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Identity.Endpoints;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record TokenResponse(string AccessToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", async (RegisterRequest req, IdentityDbContext db) =>
        {
            if (Validate(req.Email, req.Password) is { } errs)
                return Results.ValidationProblem(errs);

            var email = req.Email.Trim().ToLowerInvariant();

            if (await db.Users.AnyAsync(u => u.Email == email))
                return Results.Conflict(new { title = "Email already registered" });
            var user = new UserEntity { Email = email, PasswordHash = PasswordHasher.Hash(req.Password) };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/users/{user.Id}", new { user.Id, user.Email });
        });

        app.MapPost("/auth/login", async (LoginRequest req, IdentityDbContext db, JwtTokenService jwt) =>
        {
            if (Validate(req.Email, req.Password) is { } errs)
                return Results.ValidationProblem(errs);

            var email = req.Email.Trim().ToLowerInvariant();

            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();
            return Results.Ok(new TokenResponse(jwt.Create(user.Id, user.Email)));
        });
    }

    private static Dictionary<string, string[]>? Validate(string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            errors["email"] = ["A valid email is required."];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters."];
        return errors.Count > 0 ? errors : null;
    }
}
