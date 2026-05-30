// Unit tests for SignupValidation. Covers tenant slug regex, password complexity, and email format
// boundary cases so the rule changes do not regress silently.

using AgentOs.Modules.Tenants;
using Shouldly;
using Xunit;

namespace AgentOs.Tests.Identity;

public sealed class SignupValidationTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a1b2c3")]
    [InlineData("0123456789-0123456789-0123456789")] // 32 chars
    public void ValidateTenantId_AcceptsValidSlugs(string id)
    {
        var (ok, error) = SignupValidation.ValidateTenantId(id);
        ok.ShouldBeTrue($"expected '{id}' valid, got '{error}'");
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")] // 2 chars (regex allows 1 or 3+)
    [InlineData("-acme")] // leading dash
    [InlineData("acme-")] // trailing dash
    [InlineData("Acme")] // uppercase
    [InlineData("acme corp")] // space
    [InlineData("acme_corp")] // underscore
    [InlineData("acme.corp")] // dot
    [InlineData("0123456789-0123456789-01234567890")] // 33 chars
    public void ValidateTenantId_RejectsInvalidSlugs(string? id)
    {
        var (ok, error) = SignupValidation.ValidateTenantId(id);
        ok.ShouldBeFalse();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Abcdefghij1!")] // exactly 12, all classes
    [InlineData("Sup3rSecure!Pass")] // 16
    [InlineData("P@ssw0rd1234")] // 12
    public void ValidatePassword_AcceptsStrongPasswords(string pwd)
    {
        var (ok, error) = SignupValidation.ValidatePassword(pwd);
        ok.ShouldBeTrue($"expected '{pwd}' valid, got '{error}'");
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short1!A")] // 8 chars only
    [InlineData("alllowercase1!")] // no upper
    [InlineData("ALLUPPERCASE1!")] // no lower
    [InlineData("NoDigitsHere!!")] // no digit
    [InlineData("NoSymbolsHere12")] // no symbol
    public void ValidatePassword_RejectsWeakPasswords(string? pwd)
    {
        var (ok, error) = SignupValidation.ValidatePassword(pwd);
        ok.ShouldBeFalse();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("alice@example.com")]
    [InlineData("a.b+c@sub.domain.tld")]
    public void ValidateEmail_AcceptsWellFormed(string email)
    {
        var (ok, error) = SignupValidation.ValidateEmail(email);
        ok.ShouldBeTrue($"expected '{email}' valid, got '{error}'");
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nouser.com")]
    [InlineData("user@")]
    [InlineData("user space@example.com")]
    public void ValidateEmail_RejectsMalformed(string? email)
    {
        var (ok, _) = SignupValidation.ValidateEmail(email);
        ok.ShouldBeFalse();
    }
}
