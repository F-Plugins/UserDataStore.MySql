using OpenMod.EntityFrameworkCore.MySql;

namespace UserDataStore.MySql.Database;

internal class UserDataStoreDbContextFactory : OpenModMySqlDbContextFactory<UserDataStoreDbContext>
{
}
