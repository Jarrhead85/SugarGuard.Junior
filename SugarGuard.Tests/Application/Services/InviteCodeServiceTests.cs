using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Data;
using SugarGuard.API.Security;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class InviteCodeServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"InviteCode_{Guid.NewGuid():N}")
        .Options;

    public void Dispose()
    {
        using var database = new AppDbContext(_dbOptions);
        database.Database.EnsureDeleted();
    }

    [Fact]
    public async Task ClaimAsync_DoctorInvite_CreatesDoctorChildLinkAndClaimsCode()
    {
        var doctorId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await using (var database = new AppDbContext(_dbOptions))
        {
            database.Users.Add(new User
            {
                UserId = doctorId,
                EmailForLogin = "doctor@example.test",
                Role = UserRole.Doctor,
                IsActive = true,
                IsEmailVerified = true
            });
            database.Children.Add(new Child
            {
                ChildId = childId,
                FirstName = "Иван",
                LastName = "Иванов",
                DateOfBirth = new DateOnly(2015, 1, 1),
                DiabetesType = "T1"
            });
            database.InviteCodes.Add(new InviteCode
            {
                ChildId = childId,
                Code = "ABCD2345",
                TargetRole = UserRole.Doctor,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await database.SaveChangesAsync();
        }

        var result = await CreateService().ClaimAsync("ABCD-2345", doctorId);

        Assert.True(result.Success);
        Assert.Equal(childId, result.ChildId);
        Assert.Equal("DoctorChildLink", result.LinkType);

        await using var verificationDatabase = new AppDbContext(_dbOptions);
        var link = await verificationDatabase.DoctorChildLinks.SingleAsync();
        var invite = await verificationDatabase.InviteCodes.SingleAsync();
        Assert.Equal(doctorId, link.DoctorUserId);
        Assert.Equal(childId, link.ChildId);
        Assert.Equal("Claimed", invite.Status);
        Assert.Equal(doctorId, invite.ClaimedByUserId);
    }

    private InviteCodeService CreateService()
    {
        var crypto = new Mock<ICryptoService>();
        crypto.Setup(service => service.Decrypt(It.IsAny<string>())).Returns(string.Empty);

        return new InviteCodeService(
            new AppDbContext(_dbOptions),
            Mock.Of<IAuditService>(),
            crypto.Object,
            NullLogger<InviteCodeService>.Instance);
    }
}
