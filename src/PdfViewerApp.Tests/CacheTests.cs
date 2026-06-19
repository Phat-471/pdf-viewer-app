using System;
using System.Reflection;
using Xunit;
using PdfViewerApp;

namespace PdfViewerApp.Tests
{
    public class CacheTests
    {
        [Fact]
        public void MaxBitmapCacheBytes_ShouldBeWithinClampedRange()
        {
            // Fetch private static field MaxBitmapCacheBytes via Reflection
            var field = typeof(PdfDocumentTab).GetField("MaxBitmapCacheBytes", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            
            long value = (long)field.GetValue(null)!;
            
            // Check that it's between 256MB and 1GB
            Assert.True(value >= 268435456L, $"Cache size {value} is below 256MB minimum");
            Assert.True(value <= 1073741824L, $"Cache size {value} is above 1GB maximum");
        }
    }
}
