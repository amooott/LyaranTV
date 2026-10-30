using System;
using System.Security.Cryptography;
using System.Text;

namespace LyaranTV
{
    internal static class CredentialVault
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LyaranTV.credentials.v1");

        public static string Protect(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return ""; }
            catch (FormatException) { return ""; }
        }
    }
}
