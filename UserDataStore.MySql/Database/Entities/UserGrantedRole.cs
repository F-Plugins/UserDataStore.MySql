namespace UserDataStore.MySql.Database.Entities;

internal class UserGrantedRole
{
    public string RoleId { get; set; }
    public string UserId { get; set; }
    public string UserType { get; set; }

    public UserGrantedRole(string roleId, string userId, string userType)
    {
        RoleId = roleId;
        UserId = userId;
        UserType = userType;
    }
}
