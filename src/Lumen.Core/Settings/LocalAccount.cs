using System.Security.Cryptography;
using System.Text;

namespace Lumen.Core.Settings;

public static class LocalAccount
{
    public static string? ValidateUsername(string? raw)
    {
        var name = (raw ?? "").Trim();
        if (name.Length < 2)
        {
            return "Escribe un usuario de al menos 2 letras. Sin email.";
        }

        if (name.Contains('@', StringComparison.Ordinal))
        {
            return "No uses email. Solo un nombre, tipo martin o sala.";
        }

        if (name.Any(char.IsWhiteSpace) && name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
        {
            return "Un nombre corto. Así te ven los otros PCs.";
        }

        if (name.Length > 24)
        {
            return "Máximo 24 caracteres.";
        }

        return null;
    }

    public static string? ValidatePassword(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 4)
        {
            return "La contraseña: mínimo 4 caracteres.";
        }

        if (raw.Length > 64)
        {
            return "Contraseña demasiado larga.";
        }

        return null;
    }

    public static string HashPassword(string password, string salt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + "\n" + password));
        return Convert.ToHexString(bytes);
    }
}
