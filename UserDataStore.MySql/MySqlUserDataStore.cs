using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenMod.API;
using OpenMod.API.Eventing;
using OpenMod.API.Ioc;
using OpenMod.API.Plugins;
using OpenMod.API.Prioritization;
using OpenMod.API.Users;
using OpenMod.Core.Helpers;
using OpenMod.Core.Plugins.Events;
using SilK.Unturned.Extras.Dispatcher;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;
using UserDataStore.MySql.Database;
using UserDataStore.MySql.Database.Entities;

namespace UserDataStore.MySql;

// most of the code is from https://github.com/openmod/openmod/blob/main/framework/OpenMod.Core/Users/UserDataStore.cs
[ServiceImplementation(Lifetime = ServiceLifetime.Singleton, Priority = Priority.Lowest)]
internal class MySqlUserDataStore : IUserDataStore, IDisposable
{
    private readonly ConcurrentDictionary<(string, string), UserData> _cachedUserData = new();
    private readonly IPluginAccessor<UserDataStorePlugin> _pluginAccessor;
    private readonly IActionDispatcher _dispatcher;
    private readonly List<IDisposable> _eventDisposables;
    private System.Timers.Timer? _timer;
    private bool UseCache => Configuration.GetSection("Cache:UseCache").Get<bool>();
    private double RefreshInterval => Configuration.GetSection("Cache:RefreshInterval").Get<double>();

    public MySqlUserDataStore(IPluginAccessor<UserDataStorePlugin> pluginAccessor, IActionDispatcher dispatcher, IRuntime runtime, IEventBus eventBus)
    {
        _pluginAccessor = pluginAccessor;
        _dispatcher = dispatcher;

        _eventDisposables = new()
        {
            eventBus.Subscribe(runtime, (IServiceProvider __, object? _, PluginConfigurationChangedEvent @event) =>
            {
                if (@event.Plugin.GetType() != typeof(UserDataStorePlugin).GetType())
                    return Task.CompletedTask;

                if (_timer is not null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                }

                if (UseCache)
                    InitializeCacheTimer();

                return Task.CompletedTask;
            }),
            eventBus.Subscribe(runtime, (IServiceProvider __, object? _, PluginLoadedEvent @event) =>
            {
                if(@event.Plugin.GetType() != typeof(UserDataStorePlugin).GetType())
                    return Task.CompletedTask;

                if(UseCache)
                    InitializeCacheTimer();

                return Task.CompletedTask;
            })
        };
    }

    public async Task<UserData?> GetUserDataAsync(string userId, string userType)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(nameof(userId));
        }

        if (string.IsNullOrEmpty(userType))
        {
            throw new ArgumentException(nameof(userType));
        }

        return await _dispatcher.Enqueue(async () =>
        {
            UserData? data = null;
            bool useCache = UseCache;

            if (useCache && _cachedUserData.TryGetValue((userId, userType), out data))
            {
                return data;
            }

            /* 
             * There is a reason for this...
             * OpenMod saves the command context in thread
             * Therefore if i use async await that will result in switching to another thread
             * I will query the data from another thread and wait for it to be finished in this
             * Not sure how bad this is
             */
            await using var context = GetDbContext();
            var user = await context.Users
                .AsNoTracking()
                .Include(u => u.GrantedRoles)
                .Include(u => u.GrantedPermissions)
                .Include(u => u.GenericDatas)
                .FirstOrDefaultAsync(u => u.Id == userId && u.Type == userType);
            data = user?.ToUserData();

            if (useCache && data is not null)
                _cachedUserData.TryAdd((userId, userType), data);

            return data;
        });
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

        return await _dispatcher.Enqueue(async () =>
        {
            await using var context = GetDbContext();
            var data = await context.UserGenericDatas.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId && d.UserType == userType && d.Key == key);

            if (data is null)
                return default;

            if (data is T obj)
                return obj;

            return default;
        });
    }

    public async Task<IReadOnlyCollection<UserData>> GetUsersDataAsync(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentException(nameof(type));
        }

        return await _dispatcher.Enqueue(async () =>
        {
            await using var context = GetDbContext();
            return await context.Users
                .Include(u => u.GrantedRoles)
                .Include(u => u.GrantedPermissions)
                .Include(u => u.GenericDatas)
                .Where(u => u.Type == type)
                .Select(u => u.ToUserData())
                .ToListAsync();
        });
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

        await _dispatcher.Enqueue(async () =>
        {
            await using var context = GetDbContext();
            var data = await context.UserGenericDatas.FindAsync(new { userId, userType, key });
            var json = value is null ? null : JsonSerializer.Serialize(value);

            _cachedUserData.TryRemove((userId, userType), out _);

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
        });
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

        await _dispatcher.Enqueue(async () =>
        {
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
            _cachedUserData.TryRemove((userData.Id ?? "", userData.Type ?? ""), out _);
        });
    }

    private void InitializeCacheTimer()
    {
        _timer = new()
        {
            Interval = TimeSpan.FromSeconds(RefreshInterval).TotalMilliseconds
        };
        _timer.Elapsed += ClearCache;
        _timer.Start();
    }

    private void ClearCache(object sender, ElapsedEventArgs e)
    {
        _cachedUserData.Clear();
    }

    private UserDataStoreDbContext GetDbContext() => _pluginAccessor.Instance?.LifetimeScope.Resolve<UserDataStoreDbContext>() ?? throw new Exception("The plugin is not loaded. Make sure that there are no errors while loading");

    private IConfiguration Configuration => _pluginAccessor.Instance?.LifetimeScope.Resolve<IConfiguration>() ?? throw new Exception("The plugin is not loaded. Make sure that there are no errors while loading");

    public void Dispose()
    {
        _eventDisposables.DisposeAll();
        _cachedUserData.Clear();
        _timer?.Stop();
        _timer?.Dispose();
    }
}
