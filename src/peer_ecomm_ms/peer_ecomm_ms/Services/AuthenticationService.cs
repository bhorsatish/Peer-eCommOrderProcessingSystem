using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace eComm_ms.Services
{
    public class AuthenticationService
    {
        private static readonly Dictionary<string, string> _userDatabase = new Dictionary<string, string>();

        private const int SaltSize = 16; // 128 bit salt
        private const int KeySize = 32;  // 256 bit hash
        private const int Iterations = 100000; // High iteration count slows down brute-force attacks
        private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;

        /// <summary>
        /// Generates a salted hash for a new password
        /// </summary>
        public static string HashPassword(string password)
        {
            // 1. Generate a secure random salt
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 2. Hash the password with the salt using PBKDF2
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                _hashAlgorithmName,
                KeySize
            );

            // 3. Combine the salt and hash into a single string for database storage
            // Format: Base64(salt) : Base64(hash)
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies a plain-text password against a stored salted hash
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            // 1. Split the stored string back into its salt and hash components
            string[] parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] storedPasswordHash = Convert.FromBase64String(parts[1]);

            // 2. Hash the provided login password with the exact same salt and parameters
            byte[] providedPasswordHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                _hashAlgorithmName,
                KeySize
            );

            // 3. Compare the hashes
            // IMPORTANT: We use FixedTimeEquals to prevent "Timing Attacks"
            return CryptographicOperations.FixedTimeEquals(storedPasswordHash, providedPasswordHash);
        }
    }
}
