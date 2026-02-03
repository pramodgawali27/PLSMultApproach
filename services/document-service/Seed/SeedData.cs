using Microsoft.EntityFrameworkCore;
using PLS.BuildingBlocks.Data;
using PLS.BuildingBlocks.Entitlements;

static class SeedData
{
    public static async Task InitializeAsync(PlsDbContext db)
    {
        if (await db.Tenants.AnyAsync())
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo Tenant" });
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            ExternalId = Guid.NewGuid().ToString("n"),
            Email = "demo@pls.local",
            DisplayName = "demo"
        });
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanName = "Starter",
            EnabledLanes = (int)LaneFlags.LaneA,
            TokenQuotaMonthly = 10000,
            ActiveFrom = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
