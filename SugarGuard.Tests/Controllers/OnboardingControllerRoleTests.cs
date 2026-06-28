using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Controllers;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Dto;
using IChildAccessService = SugarGuard.API.Services.IChildAccessService;
using ICurrentUserContext = SugarGuard.API.Services.ICurrentUserContext;
using IOnboardingService = SugarGuard.API.Application.Interfaces.IOnboardingService;
using IChildrenService = SugarGuard.API.Application.Interfaces.IChildrenService;
using IDiabetesSettingsService = SugarGuard.API.Application.Interfaces.IDiabetesSettingsService;

namespace SugarGuard.Tests.Controllers;

/// <summary>
/// Тесты для <see cref="OnboardingController"/>.
/// <para>
/// H-1 (release 1.0.0): контроллер должен отклонять попытки Doctor
/// (и ChildDevice) создать профиль ребёнка — 403 Forbidden.
/// Создавать ребёнка могут только Parent и Admin-роли.
/// </para>
/// </summary>
public class OnboardingControllerRoleTests
{
    private readonly Mock<IOnboardingService> _onboarding = new();
    private readonly Mock<IChildrenService> _childrenService = new();
    private readonly Mock<IDiabetesSettingsService> _diabetesSettings = new();
    private readonly Mock<IChildAccessService> _childAccess = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly OnboardingController _sut;

    public OnboardingControllerRoleTests()
    {
        _sut = new OnboardingController(
            _onboarding.Object,
            _childrenService.Object,
            _diabetesSettings.Object,
            _childAccess.Object,
            _currentUser.Object,
            NullLogger<OnboardingController>.Instance);
    }

    /// <summary>
    /// H-1: Doctor не может создать профиль ребёнка.
    /// Врач привязывается через InviteCode, а не через онбординг.
    /// </summary>
    [Fact]
    public async Task CreateChildOnboarding_DoctorRole_Returns403Forbidden()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _currentUser.Setup(x => x.GetRole()).Returns(UserRole.Doctor);

        var request = new CreateChildOnboardingRequest
        {
            FirstName = "Иван",
            LastName = "Петров",
            DateOfBirth = new DateOnly(2015, 5, 1),
            DiabetesType = "Type1"
        };

        var result = await _sut.CreateChildOnboardingAsync(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

        // Сервис НЕ должен вызываться — это критично, иначе PHI утечёт в AuditLog.
        _childrenService.Verify(
            x => x.CreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserRole>(),
                It.IsAny<CreateChildRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// ChildDevice создаёт собственный профиль ребёнка из мобильного onboarding.
    /// </summary>
    [Fact]
    public async Task CreateChildOnboarding_ChildDeviceRole_CreatesProfile_WhenNoProfileExists()
    {
        var userId = Guid.NewGuid();
        var newChildId = Guid.NewGuid();

        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _currentUser.Setup(x => x.GetRole()).Returns(UserRole.ChildDevice);
        _childAccess
            .Setup(x => x.GetAccessibleChildIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _childrenService
            .Setup(x => x.CreateAsync(
                userId,
                UserRole.ChildDevice,
                It.IsAny<CreateChildRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateChildResult
            {
                Child = new ChildResponse
                {
                    ChildId = newChildId,
                    FirstName = "Мария",
                    LastName = "Сидорова"
                }
            });
        _onboarding
            .Setup(x => x.GetTotalSteps(UserRole.ChildDevice))
            .Returns(3);

        var request = new CreateChildOnboardingRequest
        {
            FirstName = "Мария",
            LastName = "Сидорова",
            DateOfBirth = new DateOnly(2014, 1, 1),
            DiabetesType = "Type1"
        };

        var result = await _sut.CreateChildOnboardingAsync(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<CreateChildOnboardingResponse>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal(newChildId, body.ChildId);

        _childrenService.Verify(x => x.CreateAsync(
            userId,
            UserRole.ChildDevice,
            It.IsAny<CreateChildRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _childrenService.Verify(x => x.UpdateAsync(
            It.IsAny<Guid>(),
            It.IsAny<UpdateChildRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _onboarding.Verify(x => x.CompleteStepAsync(userId, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Повторный мобильный onboarding ChildDevice обновляет существующий профиль, а не создаёт дубль.
    /// </summary>
    [Fact]
    public async Task CreateChildOnboarding_ChildDeviceRole_UpdatesExistingProfile_WhenProfileExists()
    {
        var userId = Guid.NewGuid();
        var existingChildId = Guid.NewGuid();

        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _currentUser.Setup(x => x.GetRole()).Returns(UserRole.ChildDevice);
        _childAccess
            .Setup(x => x.GetAccessibleChildIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingChildId });
        _childrenService
            .Setup(x => x.GetByIdAsync(existingChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildResponse
            {
                ChildId = existingChildId,
                FirstName = "Мария",
                LastName = "Сидорова",
                DateOfBirth = new DateOnly(2014, 1, 1),
                DiabetesType = "Type1",
                Weight = 35m,
                Height = 145m
            });
        _childrenService
            .Setup(x => x.UpdateAsync(
                existingChildId,
                It.IsAny<UpdateChildRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildResponse
            {
                ChildId = existingChildId,
                FirstName = "Мария",
                LastName = "Петрова",
                DateOfBirth = new DateOnly(2014, 1, 1),
                DiabetesType = "Type1",
                Weight = 35m,
                Height = 145m
            });
        _onboarding
            .Setup(x => x.GetTotalSteps(UserRole.ChildDevice))
            .Returns(3);

        var request = new CreateChildOnboardingRequest
        {
            FirstName = "Мария",
            LastName = "Петрова",
            DateOfBirth = new DateOnly(2014, 1, 1),
            DiabetesType = "Type1"
        };

        var result = await _sut.CreateChildOnboardingAsync(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<CreateChildOnboardingResponse>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal(existingChildId, body.ChildId);

        _childrenService.Verify(x => x.CreateAsync(
            It.IsAny<Guid>(),
            It.IsAny<UserRole>(),
            It.IsAny<CreateChildRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _childrenService.Verify(x => x.UpdateAsync(
            existingChildId,
            It.Is<UpdateChildRequest>(r => r.LastName == "Петрова"),
            It.IsAny<CancellationToken>()), Times.Once);
        _onboarding.Verify(x => x.CompleteStepAsync(userId, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// H-1: Parent — разрешённая роль, контроллер должен делегировать в сервис.
    /// </summary>
    [Fact]
    public async Task CreateChildOnboarding_ParentRole_DelegatesToService()
    {
        var userId = Guid.NewGuid();
        var newChildId = Guid.NewGuid();
        var parentLinkId = Guid.NewGuid();

        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _currentUser.Setup(x => x.GetRole()).Returns(UserRole.Parent);

        _childrenService
            .Setup(x => x.CreateAsync(
                userId,
                UserRole.Parent,
                It.IsAny<CreateChildRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateChildResult
            {
                Child = new ChildResponse
                {
                    ChildId = newChildId,
                    FirstName = "Иван",
                    LastName = "Петров"
                },
                ParentLinkId = parentLinkId
            });

        var request = new CreateChildOnboardingRequest
        {
            FirstName = "Иван",
            LastName = "Петров",
            DateOfBirth = new DateOnly(2015, 5, 1),
            DiabetesType = "Type1"
        };

        var result = await _sut.CreateChildOnboardingAsync(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<CreateChildOnboardingResponse>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal(newChildId, body.ChildId);
        Assert.Equal(parentLinkId, body.LinkId);

        _childrenService.Verify(
            x => x.CreateAsync(
                userId,
                UserRole.Parent,
                It.IsAny<CreateChildRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// H-1: Admin и SupportAdmin и ServiceAccount — разрешённые роли.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SupportAdmin)]
    [InlineData(UserRole.ServiceAccount)]
    public async Task CreateChildOnboarding_AdminRoles_DelegatesToService(UserRole adminRole)
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _currentUser.Setup(x => x.GetRole()).Returns(adminRole);

        _childrenService
            .Setup(x => x.CreateAsync(
                userId,
                adminRole,
                It.IsAny<CreateChildRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateChildResult
            {
                Child = new ChildResponse
                {
                    ChildId = Guid.NewGuid(),
                    FirstName = "Иван",
                    LastName = "Петров"
                },
                ParentLinkId = Guid.NewGuid()
            });

        var request = new CreateChildOnboardingRequest
        {
            FirstName = "Иван",
            LastName = "Петров",
            DateOfBirth = new DateOnly(2015, 5, 1),
            DiabetesType = "Type1"
        };

        var result = await _sut.CreateChildOnboardingAsync(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetStatusAsync — error handling matrix
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_NoAuthenticatedUser_Returns401()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns((Guid?)null);

        var result = await _sut.GetStatusAsync(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetStatus_UserNotFound_Returns404()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _onboarding
            .Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("User not found"));

        var result = await _sut.GetStatusAsync(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetStatus_ServiceReturnsResult_Returns200()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _onboarding
            .Setup(x => x.GetStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingStatusResponse
            {
                IsCompleted = false,
                CurrentStep = OnboardingStep.CreateChild,
                Role = UserRole.Parent.ToString(),
                ProgressPercent = 60,
                HasChild = true,
                ChildId = Guid.NewGuid()
            });

        var result = await _sut.GetStatusAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<OnboardingStatusResponse>(ok.Value);
        Assert.Equal(OnboardingStep.CreateChild, body.CurrentStep);
        Assert.Equal(60, body.ProgressPercent);
        Assert.True(body.HasChild);
    }

    [Fact]
    public async Task GetStatus_UnexpectedException_Returns500()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _onboarding
            .Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _sut.GetStatusAsync(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    // ───────────────────────────────────────────────────────────────────
    // CompleteStepAsync — validation of step range
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteStep_NoAuthenticatedUser_Returns401()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns((Guid?)null);

        var result = await _sut.CompleteStepAsync(1, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task CompleteStep_ValidStep_Returns200()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _onboarding
            .Setup(x => x.CompleteStepAsync(userId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingStatusResponse
            {
                IsCompleted = false,
                CurrentStep = OnboardingStep.CreateChild,
                ProgressPercent = 40
            });

        var result = await _sut.CompleteStepAsync(2, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<OnboardingStatusResponse>(ok.Value);
        Assert.Equal(OnboardingStep.CreateChild, body.CurrentStep);
        Assert.Equal(40, body.ProgressPercent);
    }

    [Fact]
    public async Task CompleteStep_OutOfRange_Returns400()
    {
        // ArgumentOutOfRangeException из сервиса → 400 Bad Request
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _onboarding
            .Setup(x => x.CompleteStepAsync(userId, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("step"));

        var result = await _sut.CompleteStepAsync(999, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CompleteStep_UserNotFound_Returns404()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _onboarding
            .Setup(x => x.CompleteStepAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.CompleteStepAsync(1, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CompleteStep_DoctorAwaitingApproval_DelegatesToService()
    {
        // Doctor: CompleteStep(3) → Service возвращает OnboardingStatusResponse с AwaitAdminApproval
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);
        _onboarding
            .Setup(x => x.CompleteStepAsync(userId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingStatusResponse
            {
                IsCompleted = false,
                CurrentStep = OnboardingStep.AwaitAdminApproval,
                Role = UserRole.Doctor.ToString(),
                IsApprovedByAdmin = false,
                ProgressPercent = 75
            });

        var result = await _sut.CompleteStepAsync(3, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<OnboardingStatusResponse>(ok.Value);
        Assert.Equal(OnboardingStep.AwaitAdminApproval, body.CurrentStep);
        Assert.False(body.IsApprovedByAdmin);
    }

    // ───────────────────────────────────────────────────────────────────
    // SkipAsync — idempotency contract
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Skip_NoAuthenticatedUser_Returns401()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns((Guid?)null);

        var result = await _sut.SkipAsync(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Skip_ValidRequest_Returns204()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(x => x.GetUserId()).Returns(userId);

        var result = await _sut.SkipAsync(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _onboarding.Verify(
            x => x.SkipAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Skip_UserNotFound_Returns404()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _onboarding
            .Setup(x => x.SkipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.SkipAsync(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Skip_UnexpectedException_Returns500()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _onboarding
            .Setup(x => x.SkipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _sut.SkipAsync(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}
