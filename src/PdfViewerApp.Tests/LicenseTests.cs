using System;
using System.Reflection;
using Xunit;
using PdfViewerApp;

namespace PdfViewerApp.Tests
{
    public class LicenseTests
    {
        [Fact]
        public void GenerateActivationKeyForMachine_ShouldReturnFormattedKey()
        {
            string machineId = "TEST-MACH-ID-1234";
            string key = ActivationLicense.GenerateActivationKeyForMachine(machineId);
            
            Assert.NotNull(key);
            Assert.StartsWith("PDFPRO-", key);
            // Verify structure matches PDFPRO-XXXX-XXXX-XXXX-XXXX
            string[] parts = key.Split('-');
            Assert.Equal(5, parts.Length); // PDFPRO + 4 groups of 4 chars
            Assert.Equal("PDFPRO", parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                Assert.Equal(4, parts[i].Length);
            }
        }

        [Fact]
        public void ValidateKey_ShouldReturnTrueForValidKey()
        {
            string machineId = "ABCD-EFGH-IJKL";
            string key = ActivationLicense.GenerateActivationKeyForMachine(machineId);

            // Access private static method ValidateKey via Reflection
            var method = typeof(ActivationLicense).GetMethod("ValidateKey", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            bool isValid = (bool)method.Invoke(null, new object[] { key, machineId })!;
            Assert.True(isValid, "Key should be valid for the generated machine ID");
        }

        [Fact]
        public void ValidateKey_ShouldReturnFalseForInvalidKey()
        {
            string machineId = "ABCD-EFGH-IJKL";
            string key = "PDFPRO-AAAA-BBBB-CCCC-DDDD";

            // Access private static method ValidateKey via Reflection
            var method = typeof(ActivationLicense).GetMethod("ValidateKey", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            bool isValid = (bool)method.Invoke(null, new object[] { key, machineId })!;
            Assert.False(isValid, "Invalid key should fail validation");
        }
    }
}
