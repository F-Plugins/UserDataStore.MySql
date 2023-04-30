using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenMod.API.Ioc;
using OpenMod.API.Plugins;
using OpenMod.API.Prioritization;
using OpenMod.API.Users;
using System.Text.Json;
using UserDataStore.MySql.Database;
using UserDataStore.MySql.Database.Entities;

namespace UserDataStore.MySql;

// most of the code is from https://github.com/openmod/openmod/blob/main/framework/OpenMod.Core/Users/UserDataStore.cs
[ServiceImplementation(Lifetime = ServiceLifetime.Singleton, Priority = Priority.Lowest)]
internal class MySqlUserDataStore : IUserDataStore
{
    private readonly IPluginAccessor<UserDataStorePlugin> _pluginAccessor;

    public MySqlUserDataStore(IPluginAccessor<UserDataStorePlugin> pluginAccessor)
    {
        _pluginAccessor = pluginAccessor;
    }

    public Task<UserData?> GetUserDataAsync(string userId, string userType)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(nameof(userId));
        }

        if (string.IsNullOrEmpty(userType))
        {
            throw new ArgumentException(nameof(userType));
        }

        UserData? data = null;

        /* 
         * There is a reason for this...
         * OpenMod saves the command context in thread
         * Therefore if i use async await that will result in switching to another thread
         * I will query the data from another thread and wait for it to be finished in this
         * Not sure how bad this is
         */

        Task.Run(async () =>
        {
            await using var context = GetDbContext();
            var user = await context.Users
                .AsNoTracking()
                .Include(u => u.GrantedRoles)
                .Include(u => u.GrantedPermissions)
                .Include(u => u.GenericDatas)
                .FirstOrDefaultAsync(u => u.Id == userId && u.Type == userType);
            data = user?.ToUserData();
        }).Wait();

        return Task.FromResult(data);
    }

    public async Task<T?> GetUserDataAsync<T>(string userId, string userType, string key)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(nameof(userId));
        }

        if (string.IsNullOrEmpty(userType))
        {
            throw new ArgumentException(nameof(userType));
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(nameof(key));
        }

        await using var context = GetDbContext();
        var data = await context.UserGenericDatas.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId && d.UserType == userType && d.Key == key);

        if (data is null)
            return default;

        if (data is T obj)
            return obj;

        return default;
    }

    public async Task<IReadOnlyCollection<UserData>> GetUsersDataAsync(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentException(nameof(type));
        }

        await using var context = GetDbContext();
        return await context.Users
            .Include(u => u.GrantedRoles)
            .Include(u => u.GrantedPermissions)
            .Include(u => u.GenericDatas)
            .Select(u => u.ToUserData())
            .ToListAsync();
    }

    public async Task SetUserDataAsync<T>(string userId, string userType, string key, T? value)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(nameof(userId));
        }

        if (string.IsNullOrEmpty(userType))
        {
            throw new ArgumentException(nameof(userType));
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(nameof(key));
        }

        await using var context = GetDbContext();
        var data = await context.UserGenericDatas.FindAsync(new { userId, userType, key });
        var json = value is null ? null : JsonSerializer.Serialize(value);

        if (data is null)
        {
            if (json is null)
                return;

            await context.UserGenericDatas.AddAsync(new UserGenericData(key, json, userId, userType));
            await context.SaveChangesAsync();
            return;
        }

        if (value is null)
        {
            context.UserGenericDatas.Remove(data);
            await context.SaveChangesAsync();
            return;
        }

        data.SerializedValue = json;
        await context.SaveChangesAsync();
    }

    public async Task SetUserDataAsync(UserData userData)
    {
        if (userData == null)
        {
            throw new ArgumentNullException(nameof(userData));
        }

        if (string.IsNullOrWhiteSpace(userData.Id))
        {
            throw new ArgumentException(
                $"User data missing required property: {nameof(UserData.Id)}", nameof(userData));
        }

        if (string.IsNullOrWhiteSpace(userData.Type))
        {
            throw new ArgumentException(
                $"User data missing required property: {nameof(UserData.Type)}", nameof(userData));
        }

        await using var context = GetDbContext();
        var user = await context.Users
            .Include(u => u.GrantedRoles)
            .Include(u => u.GrantedPermissions) // generic data is not needed 
            .FirstOrDefaultAsync(u => u.Id == userData.Id && u.Type == userData.Type);
        if (user is null)
        {
            user ??= new User(userData.Id!, userData.Type!);
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        user.Update(userData);
        context.Users.Update(user);
        await context.SaveChangesAsync();
    }

    private UserDataStoreDbContext GetDbContext() => _pluginAccessor.Instance?.LifetimeScope.Resolve<UserDataStoreDbContext>() ?? throw new Exception("The plugin is not loaded. Make sure that there are no errors while loading");
}
