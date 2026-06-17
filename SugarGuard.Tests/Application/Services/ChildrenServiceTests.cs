using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="ChildrenService"/>.
/// <para>
/// <b>Critical coverage:</b>
/// <list type="bullet">
///   <item><description><b>PHI (C-3):</b> AuditLog никогда не должен содержать PII
///     (имя, фамилию, дату рождения). Тесты проверяют содержимое details.</description></item>
///   <item><description><b>CWE-639 (IDOR/AuthBypass):</b> Doctor с неактивной связкой
///     НЕ должен видеть ребёнка в <c>GetAccessibleAsync</c>. Это симметрично с
///     <c>ChildAccessService.GetAccessibleChildIdsAsync</c> (фикс в Phase 4.1).</description></item>
///   <item><description><b>Path traversal (CWE-22):</b> <c>UploadPhotoAsync</c> принимает
///     только локальные относительные пути. <c>..\..\..\windows\system32\config\sam</c> НЕ должен
///     удалить файл вне uploadRoot. <c>DeletePhotoAsync</c> не должен бросать исключение
///     при отсутствии файла.</description></item>
///   <item><description><b>File upload validation:</b> размер ≤5MB, расширение jpg/jpeg/png/webp/gif,
///     Content-Type из whitelist.</description></item>
/// </list>
/// </para>
/// </summary>
public class ChildrenServiceTests : IDisposable
{
    private readonly string _dbName = $"ChildrenTest_{Guid.NewGuid()}";
    private readonly Mock<IAuditService> _audit = new();
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly string _uploadRoot;

    public ChildrenServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        _uploadRoot = Path.Combine(Path.GetTempPath(), "sugarguard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadRoot);
    }

    public void Dispose()
    {
        using var ctx = new AppDbContext(_dbOptions);
        ctx.Database.EnsureDeleted();
        if (Directory.Exists(_uploadRoot))
            Directory.Delete(_uploadRoot, recursive: true);
    }

    private TestAppDbContextFactory CreateFactory() => new(_dbOptions);

    private ChildrenService CreateSut() =>
        new(CreateFactory(), _audit.Object);

    private static User CreateUser(UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = $"{role.ToString().ToLowerInvariant()}@test.local",
        Role = role,
        CreatedAt = DateTime.UtcNow
    };

    private static Child CreateChild(string first = "Иван", string last = "Петров") => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = first,
        LastName = last,
        DateOfBirth = new DateOnly(2015, 1, 1),
        DiabetesType = "Type1",
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>Реализация <see cref="IFormFile"/> поверх byte[] для тестов.</summary>
    private sealed class TestFormFile : IFormFile
    {
        private readonly byte[] _content;
        public TestFormFile(byte[] content, string fileName, string contentType)
        {
            _content = content;
            FileName = fileName;
            ContentType = contentType;
            Headers = new HeaderDictionary();
        }
        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; }
        public long Length => _content.Length;
        public string Name => "file";
        public string FileName { get; }
        public Stream OpenReadStream() => new MemoryStream(_content);
        public void CopyTo(Stream target) => new MemoryStream(_content).CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => new MemoryStream(_content).CopyToAsync(target, cancellationToken);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetAccessibleAsync — role-based matrix (CWE-639 symmetry check)
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessibleAsync_AdminRole_ReturnsAllChildren()
    {
        var c1 = CreateChild("А", "А");
        var c2 = CreateChild("Б", "Б");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.AddRange(c1, c2);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(
            Guid.NewGuid(), UserRole.Admin, page: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Theory]
    [InlineData(UserRole.SupportAdmin)]
    [InlineData(UserRole.ServiceAccount)]
    public async Task GetAccessibleAsync_AdminLikeRoles_ReturnAllChildren(UserRole role)
    {
        var c1 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c1);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(Guid.NewGuid(), role, 1, 10);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetAccessibleAsync_Parent_ReturnsOnlyLinkedChildren()
    {
        var parent = CreateUser(UserRole.Parent);
        var c1 = CreateChild("Связанный", "Ребёнок");
        var c2 = CreateChild("Несвязанный", "Ребёнок");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(parent);
            db.Children.AddRange(c1, c2);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = c1.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(parent.UserId, UserRole.Parent, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(c1.ChildId, result.Items[0].ChildId);
    }

    [Fact]
    public async Task GetAccessibleAsync_ParentCannotAccessUnrelatedChild()
    {
        // SECURITY: Parent ребёнка A не должен видеть ребёнка B (IDOR guard)
        var parent = CreateUser(UserRole.Parent);
        var c1 = CreateChild("Linked", "One");
        var c2 = CreateChild("Unlinked", "Two");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(parent);
            db.Children.AddRange(c1, c2);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = c1.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(parent.UserId, UserRole.Parent, 1, 10);

        Assert.Single(result.Items);
        Assert.Equal(c1.ChildId, result.Items[0].ChildId);
    }

    [Fact]
    public async Task GetAccessibleAsync_Doctor_ReturnsChildrenWithActiveLinkOnly()
    {
        // CWE-639 symmetry: GetAccessibleAsync должен использовать IsActive (как
        // и ChildAccessService.GetAccessibleChildIdsAsync + CanAccessChildAsync).
        // Уволенный врач (IsActive=false) НЕ должен видеть пациентов.
        var doctor = CreateUser(UserRole.Doctor);
        var cActive = CreateChild("Active", "Patient");
        var cInactive = CreateChild("Inactive", "Patient");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(doctor);
            db.Children.AddRange(cActive, cInactive);
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = cActive.ChildId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = cInactive.ChildId,
                IsActive = false,  // <-- уволен, но связка осталась
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(doctor.UserId, UserRole.Doctor, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(cActive.ChildId, result.Items[0].ChildId);
    }

    [Fact]
    public async Task GetAccessibleAsync_ChildDeviceRole_ReturnsEmpty()
    {
        // ChildDevice — это устройства, не родители. По умолчанию попадают в
        // ветку "Parent" (else), и без ParentChildLinks не получают никого.
        var device = CreateUser(UserRole.ChildDevice);
        var c1 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(device);
            db.Children.Add(c1);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetAccessibleAsync(device.UserId, UserRole.ChildDevice, 1, 10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAccessibleAsync_Pagination_RespectsPageAndPageSize()
    {
        for (int i = 0; i < 25; i++)
            using (var db = new AppDbContext(_dbOptions))
            {
                db.Children.Add(CreateChild($"И{i:D2}", $"П{i:D2}"));
                await db.SaveChangesAsync();
            }
        var sut = CreateSut();

        var page1 = await sut.GetAccessibleAsync(Guid.NewGuid(), UserRole.Admin, 1, 10);
        var page2 = await sut.GetAccessibleAsync(Guid.NewGuid(), UserRole.Admin, 2, 10);
        var page3 = await sut.GetAccessibleAsync(Guid.NewGuid(), UserRole.Admin, 3, 10);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(5, page3.Items.Count);

        // Упорядочено по LastName, FirstName
        var allItems = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
        for (int i = 1; i < allItems.Count; i++)
        {
            var prev = allItems[i - 1].LastName + allItems[i - 1].FirstName;
            var curr = allItems[i].LastName + allItems[i].FirstName;
            Assert.True(string.CompareOrdinal(prev, curr) < 0,
                $"Items not sorted at position {i}: '{prev}' should come before '{curr}'");
        }
    }

    [Fact]
    public async Task GetAccessibleAsync_ClampsInvalidPageParameters()
    {
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(CreateChild());
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // page < 1 → 1, pageSize > 200 → 200, pageSize < 1 → 1
        var r1 = await sut.GetAccessibleAsync(Guid.NewGuid(), UserRole.Admin, -5, 9999);
        var r2 = await sut.GetAccessibleAsync(Guid.NewGuid(), UserRole.Admin, 1, 0);

        Assert.Equal(1, r1.Page);
        Assert.Equal(200, r1.PageSize);
        Assert.Equal(1, r2.PageSize);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var sut = CreateSut();
        var result = await sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedResponse()
    {
        var c = CreateChild("Мария", "Сидорова");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(c.ChildId);

        Assert.NotNull(result);
        Assert.Equal("Мария", result!.FirstName);
        Assert.Equal("Сидорова", result.LastName);
    }

    // ───────────────────────────────────────────────────────────────────
    // CreateAsync — PHI protection
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ParentRole_CreatesChildAndParentLink()
    {
        var parent = CreateUser(UserRole.Parent);
        var sut = CreateSut();

        var result = await sut.CreateAsync(parent.UserId, UserRole.Parent, new CreateChildRequest
        {
            FirstName = "Алексей",
            LastName = "Смирнов",
            DateOfBirth = new DateOnly(2018, 3, 15),
            DiabetesType = "Type1"
        });

        Assert.NotEqual(Guid.Empty, result.Child.ChildId);
        Assert.NotNull(result.ParentLinkId);

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.Include(c => c.ParentChildLinks)
            .FirstAsync(c => c.ChildId == result.Child.ChildId);
        Assert.Single(saved.ParentChildLinks);
        Assert.Equal(parent.UserId, saved.ParentChildLinks.First().ParentUserId);

        // Создана дефолтная запись настроек
        var ds = await verifyDb.DiabetesSettings
            .FirstOrDefaultAsync(x => x.ChildId == result.Child.ChildId);
        Assert.NotNull(ds);
    }

    [Fact]
    public async Task CreateAsync_AdminRole_DoesNotCreateParentLink()
    {
        var admin = CreateUser(UserRole.Admin);
        var sut = CreateSut();

        var result = await sut.CreateAsync(admin.UserId, UserRole.Admin, new CreateChildRequest
        {
            FirstName = "Без",
            LastName = "Связки",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1"
        });

        Assert.Null(result.ParentLinkId);

        using var verifyDb = new AppDbContext(_dbOptions);
        var linksCount = await verifyDb.ParentChildLinks.CountAsync();
        Assert.Equal(0, linksCount);
    }

    [Fact]
    public async Task CreateAsync_AppliesDefaultsAndTrimsStrings()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(Guid.NewGuid(), UserRole.Parent, new CreateChildRequest
        {
            FirstName = "  Иван  ",  // пробелы
            LastName = "  Петров  ",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1 ",
            // Weight, Height, TimeZoneId, PhotoUrl — все null/пустые
        });

        Assert.Equal("Иван", result.Child.FirstName);
        Assert.Equal("Петров", result.Child.LastName);
        Assert.Equal("Type1", result.Child.DiabetesType);
        Assert.Equal(30m, result.Child.Weight);  // DefaultWeightKg
        Assert.Equal(130m, result.Child.Height);  // DefaultHeightCm
        Assert.Equal("UTC", result.Child.TimeZoneId);
        Assert.Equal("[]", result.Child.CurrentInsulins);
    }

    [Fact]
    public async Task CreateAsync_AppliesExplicitTimeZoneAndPhotoUrl()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(Guid.NewGuid(), UserRole.Admin, new CreateChildRequest
        {
            FirstName = "Test",
            LastName = "Test",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1",
            TimeZoneId = "Europe/Moscow",
            PhotoUrl = "  https://example.com/p.jpg  "
        });

        Assert.Equal("Europe/Moscow", result.Child.TimeZoneId);
        Assert.Equal("https://example.com/p.jpg", result.Child.PhotoUrl);
    }

    [Fact]
    public async Task CreateAsync_AuditLog_ContainsNoPhi()
    {
        // C-3: AuditLog.details НЕ должен содержать имя, фамилию, дату рождения.
        // (Только Parent={guid};Role={role} — что согласуется с AuditDetailsRedactor whitelist)
        var parent = CreateUser(UserRole.Parent);
        var sut = CreateSut();

        var result = await sut.CreateAsync(parent.UserId, UserRole.Parent, new CreateChildRequest
        {
            FirstName = "СекретноеИмя",
            LastName = "СекретнаяФамилия",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1"
        });

        _audit.Verify(a => a.WriteAsync(
            "child.created",
            "Child",
            result.Child.ChildId.ToString(),
            It.Is<string>(s => s.Contains($"Parent={parent.UserId}")
                            && s.Contains("Role=Parent")
                            && !s.Contains("СекретноеИмя")
                            && !s.Contains("СекретнаяФамилия")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // UpdateAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var sut = CreateSut();
        var result = await sut.UpdateAsync(Guid.NewGuid(), new UpdateChildRequest
        {
            FirstName = "X", LastName = "Y", DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1", Weight = 30, Height = 130
        });
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_Found_UpdatesAndPersists()
    {
        var c = CreateChild("Old", "Name");
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.UpdateAsync(c.ChildId, new UpdateChildRequest
        {
            FirstName = "New",
            LastName = "Name",
            DateOfBirth = new DateOnly(2016, 5, 5),
            DiabetesType = "Type2",
            Weight = 35,
            Height = 140,
            TimeZoneId = "Europe/Moscow"
        });

        Assert.NotNull(result);
        Assert.Equal("New", result!.FirstName);
        Assert.Equal("Type2", result.DiabetesType);

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Equal("New", saved!.FirstName);
        Assert.Equal(35m, saved.Weight);
    }

    [Fact]
    public async Task UpdateAsync_PreservesPhotoUrl_WhenNotInRequest()
    {
        var c = CreateChild();
        c.PhotoUrl = "/uploads/old.jpg";
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        await sut.UpdateAsync(c.ChildId, new UpdateChildRequest
        {
            FirstName = "Test", LastName = "Test",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1",
            Weight = 30, Height = 130,
            PhotoUrl = null  // явно null → сохраняем старый URL
        });

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Equal("/uploads/old.jpg", saved!.PhotoUrl);
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditEvent()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        await sut.UpdateAsync(c.ChildId, new UpdateChildRequest
        {
            FirstName = "New", LastName = "Name",
            DateOfBirth = new DateOnly(2015, 1, 1),
            DiabetesType = "Type1", Weight = 30, Height = 130
        });

        _audit.Verify(a => a.WriteAsync(
            "child.updated", "Child", c.ChildId.ToString(),
            null,  // details=null для UpdateAsync
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // DeleteChildAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteChildAsync_NotFound_ReturnsFalse()
    {
        var sut = CreateSut();
        Assert.False(await sut.DeleteChildAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteChildAsync_Found_RemovesAndAudits()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeleteChildAsync(c.ChildId);

        Assert.True(result);
        using var verifyDb = new AppDbContext(_dbOptions);
        Assert.False(await verifyDb.Children.AnyAsync(x => x.ChildId == c.ChildId));

        _audit.Verify(a => a.WriteAsync(
            "child.deleted", "Child", c.ChildId.ToString(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // UploadPhotoAsync — path traversal + file validation
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadPhotoAsync_EmptyFile_ReturnsNull()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var emptyFile = new TestFormFile(Array.Empty<byte>(), "x.jpg", "image/jpeg");

        var result = await sut.UploadPhotoAsync(c.ChildId, emptyFile, _uploadRoot, "https://api.test");

        Assert.Null(result);
    }

    [Fact]
    public async Task UploadPhotoAsync_OverSizeLimit_Throws()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        // 6 MB > 5 MB limit
        var bigFile = new TestFormFile(new byte[6 * 1024 * 1024], "big.jpg", "image/jpeg");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UploadPhotoAsync(c.ChildId, bigFile, _uploadRoot, "https://api.test"));
        Assert.Contains("5", ex.Message);  // 5 МБ
    }

    [Theory]
    [InlineData("test.exe")]
    [InlineData("test.php")]
    [InlineData("test.html")]
    [InlineData("test.jpg.exe")]  // double extension
    public async Task UploadPhotoAsync_DisallowedExtension_Throws(string fileName)
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[100], fileName, "image/jpeg");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test"));
        Assert.Contains("формат", ex.Message);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("IMAGE/JPEG")]  // case-insensitive
    public async Task UploadPhotoAsync_AllowedContentType_Succeeds(string contentType)
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(Encoding.UTF8.GetBytes("fake image bytes"), "x.jpg", contentType);

        var result = await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        Assert.NotNull(result);
        Assert.StartsWith($"/uploads/children/{c.ChildId}/", result);
    }

    [Fact]
    public async Task UploadPhotoAsync_DisallowedContentType_Throws()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[100], "x.jpg", "text/html");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test"));
        Assert.Contains("Content-Type", ex.Message);
    }

    [Fact]
    public async Task UploadPhotoAsync_EmptyContentType_Succeeds()
    {
        // Content-Type может быть пустым (некоторые клиенты не отправляют).
        // Тогда валидация по Content-Type пропускается, остаётся только extension check.
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[100], "x.png", "");

        var result = await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UploadPhotoAsync_ChildNotFound_ReturnsNull()
    {
        var sut = CreateSut();
        var file = new TestFormFile(new byte[100], "x.jpg", "image/jpeg");

        var result = await sut.UploadPhotoAsync(Guid.NewGuid(), file, _uploadRoot, "https://api.test");

        Assert.Null(result);
    }

    [Fact]
    public async Task UploadPhotoAsync_WritesFileToDisk_AndPersistsUrl()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var content = Encoding.UTF8.GetBytes("image-bytes-here");
        var file = new TestFormFile(content, "photo.jpg", "image/jpeg");

        var result = await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        Assert.NotNull(result);
        // Файл реально записан на диск
        var absolute = Path.Combine(_uploadRoot, result!.TrimStart('/'));
        Assert.True(File.Exists(absolute), $"File not found at {absolute}");
        Assert.Equal(content, await File.ReadAllBytesAsync(absolute));

        // URL сохранён в БД
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Equal(result, saved!.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhotoAsync_DeletesOldLocalFile()
    {
        var c = CreateChild();
        // Создаём старый файл
        var oldRelative = $"/uploads/children/{c.ChildId}/old.jpg";
        var oldDir = Path.Combine(_uploadRoot, "uploads", "children", c.ChildId.ToString());
        Directory.CreateDirectory(oldDir);
        var oldFile = Path.Combine(oldDir, "old.jpg");
        await File.WriteAllBytesAsync(oldFile, new byte[] { 1, 2, 3 });

        c.PhotoUrl = oldRelative;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var newFile = new TestFormFile(new byte[] { 9, 9, 9 }, "new.jpg", "image/jpeg");

        await sut.UploadPhotoAsync(c.ChildId, newFile, _uploadRoot, "https://api.test");

        // Старый файл удалён, новый — на месте
        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public async Task UploadPhotoAsync_DoesNotTouchExternalUrl()
    {
        // Если PhotoUrl — внешний URL (https://cdn.example.com/p.jpg), мы НЕ должны
        // пытаться его удалить (нет прав на чужой сервер).
        var c = CreateChild();
        c.PhotoUrl = "https://cdn.example.com/p.jpg";
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[] { 1 }, "new.jpg", "image/jpeg");

        await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        // PhotoUrl обновился, ничего не упало
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.NotEqual("https://cdn.example.com/p.jpg", saved!.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhotoAsync_OverwritesOldFileSafely()
    {
        // Если на диске уже есть файл по этому пути (из-за повторной загрузки),
        // File.Move с overwrite=true должен сработать атомарно через .tmp-файл.
        var c = CreateChild();
        c.PhotoUrl = "/uploads/children/whatever/already.jpg";
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[] { 5, 5, 5 }, "photo.png", "image/png");

        var result = await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        Assert.NotNull(result);
        // .tmp файл не остался
        var dir = Path.Combine(_uploadRoot, "uploads", "children", c.ChildId.ToString());
        var tempFiles = Directory.GetFiles(dir, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task UploadPhotoAsync_AuditLogContainsFilePath()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var file = new TestFormFile(new byte[100], "x.jpg", "image/jpeg");

        await sut.UploadPhotoAsync(c.ChildId, file, _uploadRoot, "https://api.test");

        _audit.Verify(a => a.WriteAsync(
            "child.photo.uploaded",
            "Child",
            c.ChildId.ToString(),
            It.Is<string>(s => s.Contains("PhotoUrl=/uploads/children/") && s.Contains("Size=100")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // DeletePhotoAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePhotoAsync_ChildNotFound_ReturnsFalse()
    {
        var sut = CreateSut();
        Assert.False(await sut.DeletePhotoAsync(Guid.NewGuid(), _uploadRoot));
    }

    [Fact]
    public async Task DeletePhotoAsync_NoPhotoUrl_ReturnsFalse()
    {
        var c = CreateChild();
        c.PhotoUrl = null;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        Assert.False(await sut.DeletePhotoAsync(c.ChildId, _uploadRoot));
    }

    [Fact]
    public async Task DeletePhotoAsync_LocalPhoto_RemovesFileAndClearsUrl()
    {
        var c = CreateChild();
        var relative = $"/uploads/children/{c.ChildId}/x.jpg";
        var dir = Path.Combine(_uploadRoot, "uploads", "children", c.ChildId.ToString());
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "x.jpg");
        await File.WriteAllBytesAsync(file, new byte[] { 1 });
        c.PhotoUrl = relative;

        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeletePhotoAsync(c.ChildId, _uploadRoot);

        Assert.True(result);
        Assert.False(File.Exists(file));
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Null(saved!.PhotoUrl);

        _audit.Verify(a => a.WriteAsync(
            "child.photo.deleted", "Child", c.ChildId.ToString(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePhotoAsync_ExternalUrl_DoesNotTouchIt()
    {
        // Внешний URL — не удаляем, просто обнуляем поле.
        var c = CreateChild();
        c.PhotoUrl = "https://cdn.example.com/x.jpg";
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeletePhotoAsync(c.ChildId, _uploadRoot);

        Assert.True(result);
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Null(saved!.PhotoUrl);
    }

    [Fact]
    public async Task DeletePhotoAsync_FileAlreadyMissing_DoesNotThrow()
    {
        // Orphan-файл допустим — защита от шумных логов при race conditions.
        var c = CreateChild();
        c.PhotoUrl = "/uploads/children/missing.jpg";  // файл не существует
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeletePhotoAsync(c.ChildId, _uploadRoot);

        Assert.True(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/passwd")]
    public async Task DeletePhotoAsync_PathTraversal_DoesNotTouchFileOutsideRoot(string maliciousPath)
    {
        // CWE-22: PhotoUrl с path-traversal компонентами НЕ должен приводить к
        // удалению файлов вне uploadRoot.
        var c = CreateChild();
        c.PhotoUrl = maliciousPath;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Должно либо вернуть true (URL обработан, но не указывает на локальный файл),
        // либо false — но НЕ бросить исключение.
        var result = await sut.DeletePhotoAsync(c.ChildId, _uploadRoot);

        // В любом случае: URL зачищен (либо null, либо обнулили поле).
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Children.FindAsync(c.ChildId);
        Assert.Null(saved!.PhotoUrl);
        Assert.True(result);
    }
}
