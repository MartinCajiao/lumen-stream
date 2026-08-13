using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lumen.Core.Paths;

namespace Lumen.Core.Settings;

public sealed class StoredAccount
{
    public string Username { get; set; } = "";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}

public readonly record struct AccountResult(bool Ok, string? Error, string? Username = null);

public sealed class AccountStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public AccountStore(string? path = null) =>
        _path = path ?? LumenPaths.AccountsFile;

    public AccountResult Create(string? username, string? password, string? confirm)
    {
        var userError = LocalAccount.ValidateUsername(username);
        if (userError is not null)
        {
            return new(false, userError);
        }

        var passError = LocalAccount.ValidatePassword(password);
        if (passError is not null)
        {
            return new(false, passError);
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            return new(false, "Las contraseñas no coinciden.");
        }

        var name = username!.Trim();
        var accounts = Load();
        if (accounts.Any(a => a.Username.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return new(false, "Ese usuario ya existe. Entra con Ya tengo cuenta.");
        }

        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        accounts.Add(new StoredAccount
        {
            Username = name,
            Salt = salt,
            PasswordHash = LocalAccount.HashPassword(password!, salt),
            CreatedUtc = DateTimeOffset.UtcNow
        });
        Save(accounts);
        return new(true, null, name);
    }

    public AccountResult SignIn(string? username, string? password)
    {
        var name = (username ?? "").Trim();
        if (name.Length == 0)
        {
            return new(false, "Escribe tu usuario.");
        }

        var account = Load().FirstOrDefault(a => a.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (account is null)
        {
            return new(false, "No hay una cuenta con ese usuario. Crea una.");
        }

        var hash = LocalAccount.HashPassword(password ?? "", account.Salt);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hash),
                Encoding.UTF8.GetBytes(account.PasswordHash)))
        {
            return new(false, "Contraseña incorrecta.");
        }

        return new(true, null, account.Username);
    }

    public bool Any() => Load().Count > 0;

    public bool Exists(string username) =>
        Load().Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    private List<StoredAccount> Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StoredAccount>>(File.ReadAllText(_path), Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save(List<StoredAccount> accounts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(accounts, Json));
    }
}
