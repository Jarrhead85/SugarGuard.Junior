using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Controllers;

namespace SugarGuard.Tests.Controllers;

/// <summary>
/// Проверки безопасной загрузки иллюстраций к статьям.
/// </summary>
public sealed class FaqContentControllerTests
{
    [Fact]
    public void UploadImage_UsesDoctorOrAdminPolicy()
    {
        var method = typeof(FaqContentController).GetMethod(nameof(FaqContentController.UploadImage));
        var authorization = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
        Assert.Equal("DoctorOrAdmin", authorization.Policy);
    }

    [Theory]
    [InlineData(nameof(FaqContentController.Update))]
    [InlineData(nameof(FaqContentController.Delete))]
    public void ArticleManagement_UsesDoctorOrAdminPolicy(string methodName)
    {
        var method = typeof(FaqContentController).GetMethod(methodName);
        var authorization = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
        Assert.Equal("DoctorOrAdmin", authorization.Policy);
    }

    [Fact]
    public async Task UploadImage_WhenStorageIsUnavailable_Returns503WithoutFileSystemDetails()
    {
        var pathOfExistingFile = Path.GetTempFileName();
        try
        {
            var uploadPaths = new Mock<IUploadPathProvider>();
            uploadPaths.SetupGet(item => item.ArticleImagesDirectory).Returns(pathOfExistingFile);

            var controller = new FaqContentController(
                Mock.Of<IFaqContentService>(),
                uploadPaths.Object,
                NullLogger<FaqContentController>.Instance);

            await using var stream = new MemoryStream(new byte[]
            {
                137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0
            });
            var file = new FormFile(stream, 0, stream.Length, "file", "illustration.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var result = await controller.UploadImage(file, CancellationToken.None);

            var unavailable = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
            Assert.DoesNotContain(pathOfExistingFile, unavailable.Value?.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(pathOfExistingFile);
        }
    }
}
