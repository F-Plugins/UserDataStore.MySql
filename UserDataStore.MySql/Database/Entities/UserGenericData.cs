using System.Text.Json;

namespace UserDataStore.MySql.Database.Entities;

internal class UserGenericData
{
    public string Key { get; set; }
    public string? SerializedValue { get; set; }
    public string UserId { get; set; }
    public string UserType { get; set; }

    public UserGenericData(string key, string? serializedValue, string userId, string userType)
    {
        Key = key;
        SerializedValue = serializedValue;
        UserId = userId;
        UserType = userType;
    }

    public object? Deserialize() => SerializedValue is null ? null : JsonSerializer.Deserialize<object>(SerializedValue);
}
