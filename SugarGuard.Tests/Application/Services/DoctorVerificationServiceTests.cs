using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Security;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class DoctorVerificationServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"DoctorVerification_{Guid.NewGuid():N}")
        .Options;
    private readonly string _uploadsDirectory = Path.Combine(Path.GetTempPath(), "SugarGuardTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        using var database = new AppDbContext(_dbOptions);
        database.Database.EnsureDeleted();

        if (Directory.Exists(_uploadsDirectory))
        {
            Directory.Delete(_uploadsDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SubmitAsync_NewRequest_PersistsRequestAndDocuments()
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            EmailForLogin = "doctor@example.test",
            Role = UserRole.DoctorPending,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        await using (var database = new AppDbContext(_dbOptions))
        {
            database.Users.Add(doctor);
            await database.SaveChangesAsync();
        }

        var service = CreateService();
        var document = new FormFile(
            new MemoryStream("%PDF-1.7\n"u8.ToArray()),
            0,
            9,
            "documents",
            "certificate.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await service.SubmitAsync(
            doctor.UserId,
            new SubmitDoctorVerificationRequest
            {
                Specialty = "Эндокринолог",
                LicenseNumber = "CERT-123"
            },
            [document]);

        await using var verificationDatabase = new AppDbContext(_dbOptions);
        var request = await verificationDatabase.DoctorVerificationRequests
            .Include(item => item.Documents)
            .SingleAsync();

        Assert.Equal(result.RequestId, request.RequestId);
        Assert.Equal(DoctorVerificationStatus.Submitted, request.Status);
        Assert.Equal(doctor.UserId, request.UserId);
        Assert.Single(request.Documents);
        Assert.Equal("certificate.pdf", request.Documents.Single().OriginalFileName);
    }

    private DoctorVerificationService CreateService()
    {
        var crypto = new Mock<ICryptoService>();
        crypto.Setup(service => service.Encrypt(It.IsAny<string>()))
            .Returns((string value) => $"encrypted:{value}");
        crypto.Setup(service => service.Decrypt(It.IsAny<string>()))
            .Returns((string value) => value.Replace("encrypted:", string.Empty, StringComparison.Ordinal));

        var uploadPaths = new Mock<IUploadPathProvider>();
        uploadPaths.SetupGet(provider => provider.DoctorVerificationDirectory)
            .Returns(_uploadsDirectory);
        uploadPaths.Setup(provider => provider.GetDoctorVerificationFilePath(It.IsAny<string>()))
            .Returns((string fileName) => Path.Combine(_uploadsDirectory, fileName));

        return new DoctorVerificationService(
            new AppDbContext(_dbOptions),
            crypto.Object,
            uploadPaths.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<IEmailService>(),
            NullLogger<DoctorVerificationService>.Instance);
    }
}
