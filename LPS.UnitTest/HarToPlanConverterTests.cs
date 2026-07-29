using System.Collections.Generic;
using LPS.Domain.LPSSession;
using LPS.UI.Core.Recording;
using LPS.UI.Core.Recording.Har;
using Xunit;

namespace LPS.UnitTest
{
    public class HarToPlanConverterTests
    {
        private static HarRoot MultipartHar(params HarPostParam[] parts)
        {
            return new HarRoot
            {
                Log = new HarLog
                {
                    Entries = new List<HarEntry>
                    {
                        new HarEntry
                        {
                            ResourceType = "fetch",
                            Request = new HarRequest
                            {
                                Method = "POST",
                                Url = "https://api.example.com/upload",
                                PostData = new HarPostData
                                {
                                    MimeType = "multipart/form-data; boundary=x",
                                    Params = new List<HarPostParam>(parts)
                                }
                            }
                        }
                    }
                }
            };
        }

        [Fact]
        public void Convert_MapsMultipartTextAndFileParts()
        {
            var har = MultipartHar(
                new HarPostParam { Name = "title", Value = "Hello" },
                new HarPostParam { Name = "avatar", FileName = "me.png", ContentType = "image/png" });

            var result = new HarToPlanConverter().Convert(har, new CaptureFilter(), "P");
            var payload = result.Plan.Rounds[0].Iterations[0].HttpRequest.Payload;

            Assert.Equal(Payload.PayloadType.Multipart, payload.Type);

            Assert.Single(payload.Multipart.Fields);
            Assert.Equal("title", payload.Multipart.Fields[0].Name);
            Assert.Equal("Hello", payload.Multipart.Fields[0].Value);

            Assert.Single(payload.Multipart.Files);
            Assert.Equal("avatar", payload.Multipart.Files[0].Name);
            Assert.Equal("files/me.png", payload.Multipart.Files[0].Path);

            Assert.Single(result.FileUploads);
            Assert.Equal("me.png", result.FileUploads[0].OriginalFileName);
        }

        [Fact]
        public void FileUpload_SetPath_UpdatesTheFileFieldPath()
        {
            var har = MultipartHar(new HarPostParam { Name = "doc", FileName = "a.pdf", ContentType = "application/pdf" });

            var result = new HarToPlanConverter().Convert(har, new CaptureFilter(), "P");
            result.FileUploads[0].SetPath("C:/data/real.pdf");

            var file = result.Plan.Rounds[0].Iterations[0].HttpRequest.Payload.Multipart.Files[0];
            Assert.Equal("C:/data/real.pdf", file.Path);
        }

        [Fact]
        public void Convert_JsonBody_IsRawPayload()
        {
            var har = new HarRoot
            {
                Log = new HarLog
                {
                    Entries = new List<HarEntry>
                    {
                        new HarEntry
                        {
                            ResourceType = "fetch",
                            Request = new HarRequest
                            {
                                Method = "POST",
                                Url = "https://api.example.com/login",
                                PostData = new HarPostData
                                {
                                    MimeType = "application/json",
                                    Text = "{\"user\":\"a\"}"
                                }
                            }
                        }
                    }
                }
            };

            var result = new HarToPlanConverter().Convert(har, new CaptureFilter(), "P");
            var payload = result.Plan.Rounds[0].Iterations[0].HttpRequest.Payload;

            Assert.Equal(Payload.PayloadType.Raw, payload.Type);
            Assert.Equal("{\"user\":\"a\"}", payload.Raw);
            Assert.Empty(result.FileUploads);
        }
    }
}
