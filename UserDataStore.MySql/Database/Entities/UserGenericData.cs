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

    public T? Deserialize<T>() => SerializedValue is null ? default : JsonSerializer.Deserialize<T>(SerializedValue);
}
