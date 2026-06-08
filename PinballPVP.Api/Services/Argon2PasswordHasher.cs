using Isopoh.Cryptography.Argon2;

namespace PinballPVP.Api.Services.Password;

public class Argon2PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return Argon2.Hash(password);
    }

    public bool Verify(string encodedHash, string password)
    {
        return Argon2.Verify(encodedHash, password);
    }
}