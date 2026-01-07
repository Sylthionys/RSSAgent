using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace RssAgent.Core.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiProtector
{
    public string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            var bytes = Convert.FromBase64String(cipherText);
            var unprotected = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch
        {
            return string.Empty;
        }
    }
}
