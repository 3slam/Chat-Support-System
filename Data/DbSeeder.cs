using ChatSupport.Models;
using ChatSupport.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatSupport.Data;

public static class DbSeeder
{
    /// <summary>Default password for every seeded back-office account.</summary>
    public const string DefaultPassword = "Password123!";

    public static async Task SeedAsync(AppDbContext db)
    {
        // A freshly started SQL Server container can take a while to accept
        // connections; retry the initial migration until it succeeds.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception) when (attempt < 20)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        if (await db.Admins.AnyAsync())
            return;

        var hash = PasswordHasher.Hash(DefaultPassword);

        var admins = new List<Admin>();
        for (int i = 1; i <= 10; i++)
        {
            admins.Add(new Admin
            {
                FullName = $"Admin {i}",
                Email = $"admin{i}@trips.sa",
                PasswordHash = hash,
                Role = AdminRole.Admin
            });
        }

        db.Admins.AddRange(admins);
        await db.SaveChangesAsync();
    }
}
