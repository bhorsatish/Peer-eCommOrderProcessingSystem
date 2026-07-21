using eComm_ms.Services;

namespace eComm_ms.Tests.Services
{
    public class AuthenticationServiceTests
    {
        [Fact]
        public void HashPassword_ProducesStoredHashInSaltColonHashFormat()
        {
            var hash = AuthenticationService.HashPassword("SuperSecret123");

            var parts = hash.Split(':');
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);
        }

        [Fact]
        public void HashPassword_ProducesDifferentHashesForSamePassword_DueToRandomSalt()
        {
            var hash1 = AuthenticationService.HashPassword("SamePassword");
            var hash2 = AuthenticationService.HashPassword("SamePassword");

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
        {
            var stored = AuthenticationService.HashPassword("CorrectHorseBatteryStaple");

            var result = AuthenticationService.VerifyPassword("CorrectHorseBatteryStaple", stored);

            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_ReturnsFalse_ForIncorrectPassword()
        {
            var stored = AuthenticationService.HashPassword("CorrectHorseBatteryStaple");

            var result = AuthenticationService.VerifyPassword("WrongPassword", stored);

            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_ReturnsFalse_ForMalformedStoredHash()
        {
            var result = AuthenticationService.VerifyPassword("AnyPassword", "not-a-valid-hash-format");

            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_IsCaseSensitive()
        {
            var stored = AuthenticationService.HashPassword("MyPassword");

            var result = AuthenticationService.VerifyPassword("mypassword", stored);

            Assert.False(result);
        }
    }
}
