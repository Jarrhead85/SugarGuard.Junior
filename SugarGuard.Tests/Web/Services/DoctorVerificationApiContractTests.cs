using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using SugarGuard.Web.Services;

namespace SugarGuard.Tests.Web.Services;

public sealed class DoctorVerificationApiContractTests
{
    [Fact]
    public async Task GetPendingDoctorVerificationsAsync_DeserializesSubmittedRequest()
    {
        const string json = """
            [
              {
                "requestId": "11111111-1111-1111-1111-111111111111",
                "userId": "22222222-2222-2222-2222-222222222222",
                "email": "doctor@example.com",
                "status": "Submitted",
                "specialty": "Эндокринолог",
                "licenseNumber": "ACC-123",
                "submittedAt": "2026-08-09T08:30:00Z",
                "documents": [
                  {
                    "documentId": "33333333-3333-3333-3333-333333333333",
                    "fileName": "certificate.pdf",
                    "contentType": "application/pdf",
                    "sizeBytes": 2048,
                    "uploadedAt": "2026-08-09T08:30:00Z"
                  }
                ]
              }
            ]
            """;
        var service = CreateService(HttpStatusCode.OK, json);

        var result = await service.GetPendingDoctorVerificationsAsync();

        var request = Assert.Single(result);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), request.RequestId);
        Assert.Equal("doctor@example.com", request.Email);
        Assert.Equal("Submitted", request.Status);
        Assert.Equal("Эндокринолог", request.Specialty);
        Assert.Equal("ACC-123", request.LicenseNumber);
        Assert.Equal("certificate.pdf", Assert.Single(request.Documents).FileName);
    }

    [Fact]
    public async Task GetPendingDoctorVerificationsAsync_WhenApiFails_ThrowsInsteadOfReturningEmptyQueue()
    {
        var service = CreateService(HttpStatusCode.InternalServerError, "{}");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetPendingDoctorVerificationsAsync());
    }

    private static SugarGuardApiService CreateService(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new StubHttpMessageHandler(statusCode, responseBody);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(factory => factory.CreateClient("SugarGuardApi"))
            .Returns(client);
        var tokenStore = new Mock<ITokenStore>();
        tokenStore.Setup(store => store.GetTokenAsync()).ReturnsAsync("test-access-token");

        return new SugarGuardApiService(
            clientFactory.Object,
            Mock.Of<IConfiguration>(),
            tokenStore.Object);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/doctor-verification/admin/pending", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }
}
