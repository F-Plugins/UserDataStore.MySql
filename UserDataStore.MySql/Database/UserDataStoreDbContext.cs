using Microsoft.EntityFrameworkCore;
using OpenMod.EntityFrameworkCore;
using OpenMod.EntityFrameworkCore.Configurator;
using UserDataStore.MySql.Database.Entities;

namespace UserDataStore.MySql.Database;

internal class UserDataStoreDbContext : OpenModDbContext<UserDataStoreDbContext>
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserGrantedPermission> UserGrantedPermissions => Set<UserGrantedPermission>();
    public DbSet<UserGrantedRole> UserGrantedRoles => Set<UserGrantedRole>();
    public DbSet<UserGenericData> UserGenericDatas => Set<UserGenericData>();

    public UserDataStoreDbContext(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public UserDataStoreDbContext(IDbContextConfigurator configurator, IServiceProvider serviceProvider) : base(configurator, serviceProvider)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(e => new { e.Id, e.Type });
            e.OwnsOne(e => e.BanInfo).WithOwner();
            e.HasMany(x => x.GrantedPermissions).WithOne().HasForeignKey(x => new { x.UserId, x.UserType });
            e.HasMany(x => x.GrantedRoles).WithOne().HasForeignKey(x => new { x.UserId, x.UserType });
            e.HasMany(x => x.GenericDatas).WithOne().HasForeignKey(x => new { x.UserId, x.UserType });
        });

        modelBuilder.Entity<UserGrantedPermission>().HasKey(e => new { e.UserId, e.UserType, e.Permission });
        modelBuilder.Entity<UserGrantedRole>().HasKey(e => new { e.UserId, e.UserType, e.RoleId });
        modelBuilder.Entity<UserGenericData>(e =>
        {
            e.HasKey(e => new { e.Key, e.UserId, e.UserType });
            e.Property(e => e.SerializedValue)
                .HasColumnType("json");
        });
    }
}
