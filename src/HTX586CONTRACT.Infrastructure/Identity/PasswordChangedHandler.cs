namespace HTX586CONTRACT.Infrastructure.Identity;

using global::HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public sealed class PasswordChangedHandler
    : AuthorizationHandler<PasswordChangedRequirement>
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public PasswordChangedHandler(
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PasswordChangedRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return;

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.IsActive,
                x.MustChangePassword
            })
            .FirstOrDefaultAsync();

        if (user is null || !user.IsActive || user.MustChangePassword)
            return;

        context.Succeed(requirement);
    }
}
