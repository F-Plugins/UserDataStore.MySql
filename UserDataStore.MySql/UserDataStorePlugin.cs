using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenMod.API.Plugins;
using OpenMod.Core.Plugins;
using UserDataStore.MySql.Database;

[assembly: PluginMetadata("Feli.UserDataStore.MySql", DisplayName = "User Data Store MySql", Author = "Feli", Website = "docs.fplugins.com")]

namespace UserDataStore.MySql;

public class UserDataStorePlugin : OpenModUniversalPlugin
{
    public UserDataStorePlugin(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected override async Task OnLoadAsync()
    {
        Logger.LogInformation("If you have problems while installing this plugin please refer to: discord.fplugins.com");
        await LifetimeScope.Resolve<UserDataStoreDbContext>().Database.MigrateAsync();
    }
}