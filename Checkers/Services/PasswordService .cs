using Org.BouncyCastle.Utilities;
using System.Security.Cryptography;

namespace Checkers.Services
{
    public interface IPasswordService
    {
        (string hash, string salt) HashPassword (string password);
        bool VerifyPassword(string password, string hash, string salt);
    }
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 32; // 256 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000; // Number of iterations for PBKDF2

        public (string hash, string salt) HashPassword(string password)
        {
            // Generate a random salt
            byte[] saltBytes = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            // Hash the password with the salt
            byte[] hashBytes = HashPasswordWithSalt(password, saltBytes);

            // Convert to Base64 strings for storage
            string hash = Convert.ToBase64String(hashBytes);
            string salt = Convert.ToBase64String(saltBytes);

            return (hash, salt);
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            try
            {
                // Convert stored hash and salt from Base64
                byte[] hashBytes = Convert.FromBase64String(hash);
                byte[] saltBytes = Convert.FromBase64String(salt);

                // Hash the input password with the stored salt
                byte[] computedHash = HashPasswordWithSalt(password, saltBytes);

                // Compare the computed hash with the stored hash
                return CryptographicOperations.FixedTimeEquals(hashBytes, computedHash);
            }
            catch
            {
                return false;
            }
        }
        private byte[] HashPasswordWithSalt(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }
    }
}
