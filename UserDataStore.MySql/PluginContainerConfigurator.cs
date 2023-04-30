using OpenMod.API.Plugins;
using OpenMod.EntityFrameworkCore.MySql.Extensions;
using UserDataStore.MySql.Database;

namespace UserDataStore.MySql;

internal class PluginContainerConfigurator : IPluginContainerConfigurator
{
    public void ConfigureContainer(IPluginServiceConfigurationContext context)
    {
        context.ContainerBuilder.AddMySqlDbContext<UserDataStoreDbContext>();
    }
}
