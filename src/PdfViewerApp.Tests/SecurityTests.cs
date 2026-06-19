using System;
using Xunit;
using PdfViewerApp;

namespace PdfViewerApp.Tests
{
    public class SecurityTests
    {
        [Fact]
        public void Encrypt_Decrypt_ShouldBeSymmetric()
        {
            string original = "Hello World PDF Pro HPhat 2026!";
            string encrypted = SecurityHelper.Encrypt(original);
            Assert.NotEqual(original, encrypted);
            
            string decrypted = SecurityHelper.Decrypt(encrypted);
            Assert.Equal(original, decrypted);
        }

        [Fact]
        public void Decrypt_InvalidBase64_ShouldReturnEmpty()
        {
            string invalid = "Not@Base64String";
            string result = SecurityHelper.Decrypt(invalid);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void MaskKey_ShouldHideSensitiveCharacters()
        {
            string rawKey = "PDFPRO-ABCD-1234-XYZ";
            string masked = SecurityHelper.MaskKey(rawKey);
            Assert.Equal("PDFPRO-****-****-***", masked);
            
            string shortKey = "abcd";
            Assert.Equal("****", SecurityHelper.MaskKey(shortKey));
            
            string shortKey2 = "ab1234";
            Assert.Equal("ab****", SecurityHelper.MaskKey(shortKey2));
        }
    }
}
