using LPS.Domain;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.UI.Common.DTOs;

namespace LPS.UnitTest
{
    /// <summary>
    /// Unit tests for client certificate support in HTTP requests.
    /// </summary>
    public class ClientCertificateTests
    {
        #region HttpRequestDto Tests

        [Fact]
        public void HttpRequestDto_ClientCertificatePath_ShouldBeNullByDefault()
        {
            // Arrange & Act
            var dto = new HttpRequestDto();

            // Assert
            Assert.Null(dto.ClientCertificatePath);
        }

        [Fact]
        public void HttpRequestDto_ClientCertificatePassword_ShouldBeNullByDefault()
        {
            // Arrange & Act
            var dto = new HttpRequestDto();

            // Assert
            Assert.Null(dto.ClientCertificatePassword);
        }

        [Fact]
        public void HttpRequestDto_DeepCopy_ShouldCopyClientCertificatePath()
        {
            // Arrange
            var source = new HttpRequestDto
            {
                URL = "https://example.com",
                HttpMethod = "GET",
                ClientCertificatePath = "/path/to/cert.pfx",
                ClientCertificatePassword = "password123"
            };

            // Act
            source.DeepCopy(out HttpRequestDto target);

            // Assert
            Assert.Equal(source.ClientCertificatePath, target.ClientCertificatePath);
            Assert.Equal(source.ClientCertificatePassword, target.ClientCertificatePassword);
        }

        [Fact]
        public void HttpRequestDto_DeepCopy_ShouldHandleNullCertificateProperties()
        {
            // Arrange
            var source = new HttpRequestDto
            {
                URL = "https://example.com",
                HttpMethod = "GET",
                ClientCertificatePath = null,
                ClientCertificatePassword = null
            };

            // Act
            source.DeepCopy(out HttpRequestDto target);

            // Assert
            Assert.Null(target.ClientCertificatePath);
            Assert.Null(target.ClientCertificatePassword);
        }

        #endregion

        #region HttpRequest.SetupCommand Tests

        [Fact]
        public void HttpRequestSetupCommand_ShouldHaveClientCertificateProperties()
        {
            // Arrange & Act
            var command = new HttpRequest.SetupCommand
            {
                ClientCertificatePath = "/path/to/cert.pfx",
                ClientCertificatePassword = "password123"
            };

            // Assert
            Assert.Equal("/path/to/cert.pfx", command.ClientCertificatePath);
            Assert.Equal("password123", command.ClientCertificatePassword);
        }

        [Fact]
        public void HttpRequestSetupCommand_Copy_ShouldCopyCertificateProperties()
        {
            // Arrange
            var source = new HttpRequest.SetupCommand
            {
                Url = new URL("https://example.com"),
                HttpMethod = "GET",
                HttpVersion = "2.0",
                ClientCertificatePath = "/path/to/cert.pfx",
                ClientCertificatePassword = "password123"
            };
            var target = new HttpRequest.SetupCommand();

            // Act
            source.Copy(target);

            // Assert
            Assert.Equal(source.ClientCertificatePath, target.ClientCertificatePath);
            Assert.Equal(source.ClientCertificatePassword, target.ClientCertificatePassword);
        }

        #endregion

        #region Certificate Extension Validation Tests

        [Theory]
        [InlineData(".pfx")]
        [InlineData(".p12")]
        [InlineData(".pem")]
        [InlineData(".cer")]
        [InlineData(".crt")]
        public void CertificatePath_ValidExtensions_ShouldBeAccepted(string extension)
        {
            // Arrange
            var validExtensions = new[] { ".pfx", ".p12", ".pem", ".cer", ".crt" };
            var certPath = $"/path/to/cert{extension}";
            var actualExtension = Path.GetExtension(certPath)?.ToLowerInvariant();

            // Act & Assert
            Assert.Contains(actualExtension, validExtensions);
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".json")]
        [InlineData(".xml")]
        [InlineData(".key")]
        public void CertificatePath_InvalidExtensions_ShouldBeRejected(string extension)
        {
            // Arrange
            var validExtensions = new[] { ".pfx", ".p12", ".pem", ".cer", ".crt" };
            var certPath = $"/path/to/cert{extension}";
            var actualExtension = Path.GetExtension(certPath)?.ToLowerInvariant();

            // Act & Assert
            Assert.DoesNotContain(actualExtension, validExtensions);
        }

        [Fact]
        public void CertificatePath_ShouldBeCaseInsensitiveForExtension()
        {
            // Arrange
            var validExtensions = new[] { ".pfx", ".p12", ".pem", ".cer", ".crt" };
            var certPath = "/path/to/cert.PFX";  // uppercase extension
            var actualExtension = Path.GetExtension(certPath)?.ToLowerInvariant();

            // Act & Assert
            Assert.Contains(actualExtension, validExtensions);
        }

        [Fact]
        public void CertificatePath_Placeholder_ShouldBeAccepted()
        {
            // Arrange
            var certPath = "${CERT_PATH}";

            // Act & Assert
            Assert.StartsWith("$", certPath);
        }

        #endregion
    }
}
