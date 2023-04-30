namespace UserDataStore.MySql.Database.Entities;

internal class UserGrantedPermission
{
    public string Permission { get; set; }
    public string UserId { get; set; }
    public string UserType { get; set; }

    public UserGrantedPermission(string permission, string userId, string userType)
    {
        Permission = permission;
        UserId = userId;
        UserType = userType;
    }
}
