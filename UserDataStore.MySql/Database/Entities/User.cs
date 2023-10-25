using OpenMod.API.Users;
using System.Text.Json;

namespace UserDataStore.MySql.Database.Entities;

internal class User
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string? LastDisplayName { get; set; }
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public UserBan? BanInfo { get; set; }
    public List<UserGrantedPermission> GrantedPermissions { get; set; } = new();
    public List<UserGrantedRole> GrantedRoles { get; set; } = new();
    public List<UserGenericData> GenericDatas { get; set; } = new();

    public User(string id, string type)
    {
        Id = id;
        Type = type;
    }

    public void Update(UserData data)
    {
        LastDisplayName = data.LastDisplayName;
        FirstSeen = data.FirstSeen;
        LastSeen = data.LastSeen;
        BanInfo = data.BanInfo == null ? null : new UserBan() { InstigatorId = data.BanInfo.InstigatorId, InstigatorType = data.BanInfo.InstigatorType, ExpireDate = data.BanInfo.ExpireDate, Reason = data.BanInfo.Reason };
        GrantedPermissions = data.Permissions?.Select(p => new UserGrantedPermission(p, Id, Type)).ToList() ?? new List<UserGrantedPermission>();
        GrantedRoles = data.Roles?.Select(r => new UserGrantedRole(r, Id, Type)).ToList() ?? new List<UserGrantedRole>();
        GenericDatas = data.Data?.Select(d => new UserGenericData(d.Key, JsonSerializer.Serialize(d.Value), Id, Type)).ToList() ?? new List<UserGenericData>();
    }

    public UserData ToUserData()
    {
        return new UserData
        {
            Id = Id,
            Type = Type,
            LastDisplayName = LastDisplayName,
            FirstSeen = FirstSeen,
            LastSeen = LastSeen,
            BanInfo = null,
            Permissions = new HashSet<string>(GrantedPermissions.Select(p => p.Permission)),
            Roles = new HashSet<string>(GrantedRoles.Select(p => p.RoleId)),
            Data = GenericDatas.ToDictionary(x => x.Key, x => x.Deserialize<object>())
        };
    }
}