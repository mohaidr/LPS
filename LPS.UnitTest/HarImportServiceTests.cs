using System.Text;
using LPS.UI.Core.Web.Models;
using LPS.UI.Core.Web.Services;
using Xunit;

namespace LPS.UnitTest
{
    public class HarImportServiceTests
    {
        private static System.IO.Stream Stream(string json) => new System.IO.MemoryStream(Encoding.UTF8.GetBytes(json));

        private const string TwoRequestHar = """
            {
              "log": {
                "entries": [
                  {
                    "_resourceType": "document",
                    "startedDateTime": "2024-01-01T00:00:00.000Z",
                    "time": 10,
                    "request": { "method": "GET", "url": "https://api.example.com/products", "headers": [] },
                    "response": { "status": 200, "headers": [], "content": { "mimeType": "application/json" } }
                  },
                  {
                    "_resourceType": "fetch",
                    "startedDateTime": "2024-01-01T00:00:01.000Z",
                    "time": 10,
                    "request": {
                      "method": "POST", "url": "https://api.example.com/login", "headers": [],
                      "postData": { "mimeType": "application/json", "text": "{\"user\":\"a\"}" }
                    },
                    "response": { "status": 200, "headers": [], "content": { "mimeType": "application/json" } }
                  }
                ]
              }
            }
            """;

        [Fact]
        public async Task ImportAsync_ConvertsHarIntoPlan_ReusingTheSameConverterAsLpsRecord()
        {
            var service = new HarImportService();
            var options = new HarImportOptions { PlanName = "Checkout", RoundName = "Captured", NumberOfClients = "25" };

            var result = await service.ImportAsync(Stream(TwoRequestHar), options, CancellationToken.None);

            Assert.Equal(2, result.CapturedRequestCount);
            Assert.Equal(2, result.ImportedRequestCount);
            Assert.Equal("Checkout", result.Plan.Name);
            Assert.Equal("Captured", result.Plan.Rounds[0].Name);
            Assert.Equal("25", result.Plan.Rounds[0].NumberOfClients);
            Assert.Equal("GET", result.Plan.Rounds[0].Iterations[0].HttpRequest.HttpMethod);
            Assert.Equal("POST", result.Plan.Rounds[0].Iterations[1].HttpRequest.HttpMethod);
            Assert.Empty(result.Warnings);
            Assert.Empty(result.FileUploads);
        }

        [Fact]
        public async Task ImportAsync_FiltersByHostAndMethod_LikeLpsRecordOptions()
        {
            var service = new HarImportService();
            var options = new HarImportOptions { IgnoreMethods = new[] { "post" } };

            var result = await service.ImportAsync(Stream(TwoRequestHar), options, CancellationToken.None);

            Assert.Equal(2, result.CapturedRequestCount);
            Assert.Equal(1, result.ImportedRequestCount);
            Assert.Equal("GET", result.Plan.Rounds[0].Iterations[0].HttpRequest.HttpMethod);
        }

        [Fact]
        public async Task ImportAsync_EverythingFiltered_ReturnsAWarningInsteadOfThrowing()
        {
            var service = new HarImportService();
            var options = new HarImportOptions { OnlyHosts = new[] { "other.example.com" } };

            var result = await service.ImportAsync(Stream(TwoRequestHar), options, CancellationToken.None);

            Assert.Equal(0, result.ImportedRequestCount);
            Assert.Contains(result.Warnings, warning => warning.Contains("0 remained after filtering"));
        }

        [Fact]
        public async Task ImportAsync_InvalidJson_ThrowsArgumentException()
        {
            var service = new HarImportService();

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ImportAsync(Stream("not json"), new HarImportOptions(), CancellationToken.None));
        }

        [Fact]
        public async Task ImportAsync_NoEntries_ThrowsArgumentException()
        {
            var service = new HarImportService();

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ImportAsync(Stream("""{ "log": { "entries": [] } }"""), new HarImportOptions(), CancellationToken.None));
        }

        [Fact]
        public async Task ImportAsync_FileUpload_ReportsThePlaceholderPathFromTheConvertedPlan()
        {
            const string multipartHar = """
                {
                  "log": {
                    "entries": [
                      {
                        "_resourceType": "fetch",
                        "startedDateTime": "2024-01-01T00:00:00.000Z",
                        "time": 10,
                        "request": {
                          "method": "POST", "url": "https://api.example.com/upload", "headers": [],
                          "postData": {
                            "mimeType": "multipart/form-data; boundary=x",
                            "params": [ { "name": "avatar", "fileName": "me.png", "contentType": "image/png" } ]
                          }
                        },
                        "response": { "status": 200, "headers": [] }
                      }
                    ]
                  }
                }
                """;

            var service = new HarImportService();
            var result = await service.ImportAsync(Stream(multipartHar), new HarImportOptions(), CancellationToken.None);

            var upload = Assert.Single(result.FileUploads);
            Assert.Equal("avatar", upload.FieldName);
            Assert.Equal("me.png", upload.OriginalFileName);
            Assert.Equal("files/me.png", upload.PlaceholderPath);
            Assert.Contains(result.Warnings, warning => warning.Contains("file upload field(s)"));
        }
    }
}
