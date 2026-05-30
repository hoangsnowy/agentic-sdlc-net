// Shared validation for self-service tenant sign-up. Used by both the Blazor Signup page (form-side)
// and the /tenants/register minimal API endpoint (server-side) so rules cannot drift between the two.

using System.Net.Mail;
using System.Text.RegularExpressions;

namespace AgentOs.Modules.Tenants;

/// <summary>Validation rules for self-service tenant sign-up.</summary>
public static partial class SignupValidation
{
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{1,30}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    /// <summary>Tenant slug: 1–32 chars, lowercase alphanumeric, internal hyphens only.</summary>
    public static (bool Ok, string? Error) ValidateTenantId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return (false, "Workspace ID is required.");
        }
        if (!SlugRegex().IsMatch(id))
        {
            return (false, "Workspace ID must be 1–32 chars, lowercase letters / digits / dash, no leading or trailing dash.");
        }
        return (true, null);
    }

    /// <summary>Password: ≥12 chars and at least one upper, one lower, one digit, one symbol.</summary>
    public static (bool Ok, string? Error) ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 12)
        {
            return (false, "Password must be at least 12 characters.");
        }
        bool upper = false, lower = false, digit = false, symbol = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) { upper = true; }
            else if (char.IsLower(c)) { lower = true; }
            else if (char.IsDigit(c)) { digit = true; }
            else { symbol = true; }
        }
        if (!upper || !lower || !digit || !symbol)
        {
            return (false, "Password must contain at least one uppercase, lowercase, digit and symbol.");
        }
        return (true, null);
    }

    /// <summary>Email format check via <see cref="MailAddress"/>; the address must round-trip unchanged.</summary>
    public static (bool Ok, string? Error) ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email is required.");
        }
        try
        {
            var addr = new MailAddress(email);
            return string.Equals(addr.Address, email, System.StringComparison.Ordinal)
                ? (true, null)
                : (false, "Email format is invalid.");
        }
        catch (System.FormatException)
        {
            return (false, "Email format is invalid.");
        }
    }
}
