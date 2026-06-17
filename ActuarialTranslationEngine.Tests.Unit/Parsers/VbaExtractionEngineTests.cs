using System.Collections.Generic;
using System.IO;
using Xunit;
using ActuarialTranslationEngine.Engine.Parsers;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Tests.Unit.Parsers
{
    public class VbaExtractionEngineTests
    {
        [Fact]
        public void ExtractVbaCodeStreams_NonOpenXmlStream_SwallowsExceptionAndReturnsEmpty()
        {
            // Arrange
            var engine = new VbaExtractionEngine();
            using var invalidStream = new MemoryStream(new byte[] { 0, 1, 2, 3 });

            // Act
            var result = engine.ExtractVbaCodeStreams(invalidStream);

            // Assert
            Assert.Empty(result);
        }
    }
}
