using System.Text.Json.Serialization;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;
using FlarePurge.Core.Status;

namespace FlarePurge.Core.Json;

[JsonSerializable(typeof(Account))]
[JsonSerializable(typeof(TokenVerification))]
[JsonSerializable(typeof(CachePurgeRequest))]
[JsonSerializable(typeof(CachePurgeResult))]
[JsonSerializable(typeof(ApiResponse<Account>))]
[JsonSerializable(typeof(ApiResponse<TokenVerification>))]
[JsonSerializable(typeof(ApiResponse<CachePurgeResult>))]
[JsonSerializable(typeof(ApiResponse<Zone>))]
[JsonSerializable(typeof(ApiResponse<Zone[]>))]
[JsonSerializable(typeof(ApiResponse<Account[]>))]
[JsonSerializable(typeof(StoredAccount))]
[JsonSerializable(typeof(AccountStoreData))]
[JsonSerializable(typeof(Preferences))]
[JsonSerializable(typeof(FavoriteZone))]
[JsonSerializable(typeof(RemoteStatus))]
[JsonSerializable(typeof(ZoneCacheEntry))]
[JsonSerializable(typeof(ZoneCacheData))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
public partial class CoreJsonContext : JsonSerializerContext;
