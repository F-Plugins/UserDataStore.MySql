namespace UserDataStore.MySql.Database.Entities;

internal class UserBan
{
    public DateTime? ExpireDate { get; set; }

    public string? InstigatorType { get; set; }

    public string? InstigatorId { get; set; }

    public string? Reason { get; set; }
}