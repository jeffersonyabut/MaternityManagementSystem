
using System.Text;

namespace MaternityManagementSystem.Services;

public static class UsernameNormalizer
{
    public static string Normalize(string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        return username
            .Trim()
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();
    }
}
