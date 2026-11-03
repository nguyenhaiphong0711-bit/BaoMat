using System.Globalization;
using LMS.Models;

namespace LMS.Security;

public static class AuthClaims
{
    public const string PasswordVersion = "lms:password-version";

    public static string PasswordVersionFor(User user) =>
        (user.PasswordChangedAt?.ToUniversalTime().Ticks ?? 0L).ToString(CultureInfo.InvariantCulture);
}
