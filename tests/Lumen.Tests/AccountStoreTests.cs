using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class AccountStoreTests
{
    [Fact]
    public void Create_then_sign_in()
    {
        var path = Path.Combine(Path.GetTempPath(), "lumen-accounts-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new AccountStore(path);
            var created = store.Create("martin", "clave123", "clave123");
            Assert.True(created.Ok);
            Assert.Equal("martin", created.Username);

            var taken = store.Create("Martin", "otra", "otra");
            Assert.False(taken.Ok);

            var bad = store.SignIn("martin", "nope");
            Assert.False(bad.Ok);

            var ok = store.SignIn("martin", "clave123");
            Assert.True(ok.Ok);
            Assert.Equal("martin", ok.Username);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Create_rejects_email_and_mismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), "lumen-accounts-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new AccountStore(path);
        Assert.False(store.Create("yo@mail.com", "clave123", "clave123").Ok);
        Assert.False(store.Create("sala", "clave123", "otra").Ok);
    }
}
