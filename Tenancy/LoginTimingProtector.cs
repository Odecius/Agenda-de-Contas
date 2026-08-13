using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace AgendadorContas.Tenancy;

public sealed class LoginTimingProtector
{
    private readonly AppUser _dummyUser = new() { Id = Guid.Empty };
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly string _dummyHash;

    public LoginTimingProtector(IPasswordHasher<AppUser> passwordHasher)
    {
        _passwordHasher = passwordHasher;
        _dummyHash = passwordHasher.HashPassword(_dummyUser, Guid.NewGuid().ToString("N"));
    }

    public void Verify(string suppliedPassword) =>
        _ = _passwordHasher.VerifyHashedPassword(_dummyUser, _dummyHash, suppliedPassword);
}
