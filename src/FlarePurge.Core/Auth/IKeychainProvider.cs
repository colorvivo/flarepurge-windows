using System.Collections.Generic;

namespace FlarePurge.Core.Auth;

public interface IKeychainProvider
{
    void Save(string token, string forAccount);
    string? LoadToken(string forAccount);
    void Delete(string account);
    void DeleteAll();
    IReadOnlyList<string> ListAccounts();
}
