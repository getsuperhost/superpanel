using System;

namespace SuperPanel.WebAPI.Tests
{
    public static class TestHelpers
    {
        public static string GenerateUniqueUsername(string prefix = "testuser")
        {
            return $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        public static string GenerateUniqueEmail(string prefix = "test", string domain = "example.com")
        {
            return $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}@{domain}";
        }

        public static string GenerateUniqueIp()
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 12);
            return $"ip-{guid}";
        }
    }
}