using System.Reflection.Emit;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Database;

// This DbContext is used by ASP.NET Core Identity to manage user authentication and authorization data, such as users, roles, claims, and logins. By inheriting from IdentityDbContext, it provides all the necessary functionality for handling identity-related operations in the application.
public sealed class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Set the default schema for all identity-related tables to "Identity". This helps to organize the database and avoid naming conflicts with other tables that may exist in the same database.
        builder.HasDefaultSchema(Schemas.Identity);        

        // Map the identity-related entities to specific table names. This allows for better readability and consistency in the database schema, making it clear that these tables are related to ASP.NET Core Identity.
        builder.Entity<IdentityUser>().ToTable("asp_net_users");
        builder.Entity<IdentityRole>().ToTable("asp_net_roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("asp_net_user_roles");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("asp_net_role_claims");
        builder.Entity<IdentityUserClaim<string>>().ToTable("asp_net_user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("asp_net_user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("asp_net_user_tokens");

        builder.Entity<RefreshToken>(e =>
            {
                e.HasKey(rt => rt.Id);
                e.Property(rt => rt.UserId).HasMaxLength(300);
                e.Property(rt => rt.Token).HasMaxLength(1000);
                e.HasIndex(rt => rt.Token).IsUnique();
                e.HasOne(rt => rt.User)
                    .WithMany()
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

            }
        );
    }
}
