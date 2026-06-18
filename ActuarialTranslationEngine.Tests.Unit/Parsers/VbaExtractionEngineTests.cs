using System.Collections.Generic;
using System.IO;
using Xunit;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Tests.Unit.Parsers
{
    public class VbaExtractionEngineTests
    {
        [Fact]
        public void ExtractVbaCodeStreams_NonExcelStream_ThrowsException()
        {
            // Arrange
            var engine = new VbaExtractionEngine();
            using var invalidStream = new MemoryStream(new byte[] { 0, 1, 2, 3 });

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => engine.ExtractVbaCodeStreams(invalidStream));
        }
    }
}
