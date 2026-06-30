using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Data;
using SugarGuard.API.Services;
using SugarGuard.API.DTOs;
using SugarGuard.API.Security;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="DoctorNoteService"/>.
/// <para>
/// <b>Critical coverage:</b>
/// <list type="bullet">
///   <item><description><b>Access guard (CWE-639 symmetry):</b>
///     Врач без активной связи <c>doctor_child_links</c> НЕ может создать заметку.
///     Аналогично фиксу в <c>ChildAccessService.GetAccessibleChildIdsAsync</c> (Phase 4.1)
///     и <c>ChildrenService.GetAccessibleAsync</c> (Phase 4.2).</description></item>
///   <item><description><b>Author guard (Update):</b> редактировать заметку
///     может только её автор. Чужой врач → <c>UnauthorizedAccessException</c>.</description></item>
///   <item><description><b>Author-or-Admin guard (Delete):</b> удалять заметку
///     может автор или администратор. Чужой врач без admin-флага → exception.</description></item>
///   <item><description><b>Measurement-child integrity:</b> если <c>MeasurementId</c> указан,
///     измерение должно принадлежать тому же ребёнку. Иначе — <c>KeyNotFoundException</c>.</description></item>
///   <item><description><b>Soft-delete protection:</b> неактивная связь
///     (<c>IsActive = false</c>) — тот же запрет, что и отсутствие связи.</description></item>
///   <item><description><b>Trimming:</b> <c>NoteText</c> тримится в Create и Update.</description></item>
///   <item><description><b>UpdatedAt contract:</b> null при Create, UTC после Update.</description></item>
/// </list>
/// </para>
/// </summary>
public class DoctorNoteServiceTests : IDisposable
{
    private readonly string _dbName = $"DoctorNoteTest_{Guid.NewGuid()}";
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public DoctorNoteServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
    }

    public void Dispose()
    {
        using var ctx = new AppDbContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    private AppDbContext NewDb() => new(_dbOptions);

    private DoctorNoteService CreateSut() =>
        new(NewDb(), NullLogger<DoctorNoteService>.Instance, new PassthroughCryptoService());

    private static User CreateDoctor(string firstName = "Доктор", string lastName = "Иванов") => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = "doctor@test.local",
        Role = Domain.Enums.UserRole.Doctor,
        EncryptedFirstName = firstName,
        EncryptedLastName = lastName,
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    private static Child CreateChild() => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = "Иван",
        LastName = "Петров",
        DateOfBirth = new DateOnly(2015, 1, 1),
        DiabetesType = "Type1",
        CreatedAt = DateTime.UtcNow
    };

    private static DoctorChildLink CreateLink(Guid doctorId, Guid childId, bool isActive = true) => new()
    {
        LinkId = Guid.NewGuid(),
        DoctorUserId = doctorId,
        ChildId = childId,
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive
    };

    private static User CreateParent() => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = $"parent-{Guid.NewGuid():N}@test.local",
        Role = Domain.Enums.UserRole.Parent,
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    private static DoctorNote CreateNote(
        Guid doctorId, Guid childId, Guid? measurementId = null,
        string text = "Текст заметки", bool isImportant = false,
        DateTime? createdAt = null, DateTime? updatedAt = null) => new()
    {
        NoteId = Guid.NewGuid(),
        DoctorUserId = doctorId,
        ChildId = childId,
        MeasurementId = measurementId,
        NoteText = text,
        IsImportant = isImportant,
        CreatedAt = createdAt ?? DateTime.UtcNow,
        UpdatedAt = updatedAt
    };

    // ───────────────────────────────────────────────────────────────────
    // GetByChildAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByChildAsync_NoNotes_ReturnsEmpty()
    {
        var child = CreateChild();
        using (var db = NewDb())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, 1, 10, false);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetByChildAsync_ReturnsNotesForChildOnly()
    {
        var child1 = CreateChild();
        var child2 = CreateChild();
        var doctor = CreateDoctor();
        var n1 = CreateNote(doctor.UserId, child1.ChildId, text: "Для ребёнка 1");
        var n2 = CreateNote(doctor.UserId, child1.ChildId, text: "Тоже ребёнок 1");
        var n3 = CreateNote(doctor.UserId, child2.ChildId, text: "Для ребёнка 2");
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.AddRange(child1, child2);
            db.DoctorNotes.AddRange(n1, n2, n3);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child1.ChildId, 1, 10, false);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal(child1.ChildId, i.ChildId));
    }

    [Fact]
    public async Task GetByChildAsync_OrdersByCreatedAtDescending()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var oldest = CreateNote(doctor.UserId, child.ChildId, text: "oldest",
            createdAt: DateTime.UtcNow.AddDays(-3));
        var middle = CreateNote(doctor.UserId, child.ChildId, text: "middle",
            createdAt: DateTime.UtcNow.AddDays(-2));
        var newest = CreateNote(doctor.UserId, child.ChildId, text: "newest",
            createdAt: DateTime.UtcNow.AddDays(-1));
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.AddRange(oldest, middle, newest);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, 1, 10, false);

        Assert.Equal(newest.NoteId, result.Items[0].NoteId);
        Assert.Equal(middle.NoteId, result.Items[1].NoteId);
        Assert.Equal(oldest.NoteId, result.Items[2].NoteId);
    }

    [Fact]
    public async Task GetByChildAsync_OnlyImportant_FilterApplies()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var imp = CreateNote(doctor.UserId, child.ChildId, text: "важная", isImportant: true);
        var reg = CreateNote(doctor.UserId, child.ChildId, text: "обычная", isImportant: false);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.AddRange(imp, reg);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, 1, 10, onlyImportant: true);

        Assert.Single(result.Items);
        Assert.Equal(imp.NoteId, result.Items[0].NoteId);
        Assert.True(result.Items[0].IsImportant);
    }

    [Fact]
    public async Task GetByChildAsync_Pagination_SkipsAndTakes()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var notes = Enumerable.Range(0, 5)
            .Select(i => CreateNote(doctor.UserId, child.ChildId,
                text: $"n{i}", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.AddRange(notes);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // page=1 size=2 → 2 самые свежие
        var page1 = await sut.GetByChildAsync(child.ChildId, page: 1, pageSize: 2, onlyImportant: false);
        // page=2 size=2 → следующие 2
        var page2 = await sut.GetByChildAsync(child.ChildId, page: 2, pageSize: 2, onlyImportant: false);
        // page=3 size=2 → 1 последняя
        var page3 = await sut.GetByChildAsync(child.ChildId, page: 3, pageSize: 2, onlyImportant: false);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page3.TotalCount);
        Assert.Single(page3.Items);

        // Все ID уникальны между страницами
        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items)
            .Select(i => i.NoteId).ToList();
        Assert.Equal(5, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetByChildAsync_PageSizeOver100_ClampsTo100()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var n = CreateNote(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(n);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, page: 1, pageSize: 9999, onlyImportant: false);

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetByChildAsync_PageLessThan1_ClampsTo1()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var n = CreateNote(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(n);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, page: -5, pageSize: 10, onlyImportant: false);

        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetByChildAsync_MapsDoctorName()
    {
        var child = CreateChild();
        var doctor = CreateDoctor(firstName: "Анна", lastName: "Сидорова");
        var n = CreateNote(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(n);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(child.ChildId, 1, 10, false);

        // DoctorName = "EncryptedFirstName EncryptedLastName" (в тестах — plain text)
        Assert.Equal("Анна Сидорова", result.Items[0].DoctorName);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetByMeasurementAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByMeasurementAsync_NoNotes_ReturnsEmpty()
    {
        var measurementId = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.GetByMeasurementAsync(measurementId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByMeasurementAsync_ReturnsNotesForThatMeasurement()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var m1 = new Measurement { MeasurementId = Guid.NewGuid(), ChildId = child.ChildId, GlucoseValue = 5.5m, MeasurementTime = DateTime.UtcNow };
        var m2 = new Measurement { MeasurementId = Guid.NewGuid(), ChildId = child.ChildId, GlucoseValue = 6.0m, MeasurementTime = DateTime.UtcNow };
        var n1 = CreateNote(doctor.UserId, child.ChildId, measurementId: m1.MeasurementId,
            createdAt: DateTime.UtcNow.AddMinutes(-2));
        var n2 = CreateNote(doctor.UserId, child.ChildId, measurementId: m1.MeasurementId,
            createdAt: DateTime.UtcNow.AddMinutes(-1));
        var n3 = CreateNote(doctor.UserId, child.ChildId, measurementId: m2.MeasurementId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.Measurements.AddRange(m1, m2);
            db.DoctorNotes.AddRange(n1, n2, n3);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByMeasurementAsync(m1.MeasurementId);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(m1.MeasurementId, r.MeasurementId));
        // newest first
        Assert.Equal(n2.NoteId, result[0].NoteId);
        Assert.Equal(n1.NoteId, result[1].NoteId);
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
    public async Task GetByIdAsync_Found_ReturnsDto()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var n = CreateNote(doctor.UserId, child.ChildId, text: "нашёл");
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(n);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(n.NoteId);

        Assert.NotNull(result);
        Assert.Equal(n.NoteId, result!.NoteId);
        Assert.Equal("нашёл", result.NoteText);
        Assert.Equal(doctor.UserId, result.DoctorUserId);
    }

    // ───────────────────────────────────────────────────────────────────
    // CreateAsync — access guard (CWE-639 symmetry)
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NoDoctorChildLink_ThrowsUnauthorized()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child.ChildId,
            NoteText = "Тест"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.CreateAsync(doctor.UserId, request));
    }

    [Fact]
    public async Task CreateAsync_InactiveLink_ThrowsUnauthorized()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var inactiveLink = CreateLink(doctor.UserId, child.ChildId, isActive: false);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(inactiveLink);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child.ChildId,
            NoteText = "Тест"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.CreateAsync(doctor.UserId, request));
    }

    [Fact]
    public async Task CreateAsync_LinkToDifferentChild_ThrowsUnauthorized()
    {
        // Врач связан с child1, пытается создать заметку для child2
        var child1 = CreateChild();
        var child2 = CreateChild();
        var doctor = CreateDoctor();
        var linkToChild1 = CreateLink(doctor.UserId, child1.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.AddRange(child1, child2);
            db.DoctorChildLinks.Add(linkToChild1);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child2.ChildId,
            NoteText = "Попытка"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.CreateAsync(doctor.UserId, request));
    }

    [Fact]
    public async Task CreateAsync_MeasurementNotFound_ThrowsKeyNotFound()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var link = CreateLink(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(link);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child.ChildId,
            NoteText = "К измерению",
            MeasurementId = Guid.NewGuid()  // не существует
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateAsync(doctor.UserId, request));
    }

    [Fact]
    public async Task CreateAsync_MeasurementBelongsToOtherChild_ThrowsKeyNotFound()
    {
        var child1 = CreateChild();
        var child2 = CreateChild();
        var doctor = CreateDoctor();
        var link = CreateLink(doctor.UserId, child1.ChildId);
        var otherMeasurement = new Measurement
        {
            MeasurementId = Guid.NewGuid(),
            ChildId = child2.ChildId,  // не наш ребёнок
            GlucoseValue = 5m,
            MeasurementTime = DateTime.UtcNow
        };
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.AddRange(child1, child2);
            db.Measurements.Add(otherMeasurement);
            db.DoctorChildLinks.Add(link);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child1.ChildId,
            NoteText = "Чужое измерение",
            MeasurementId = otherMeasurement.MeasurementId
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateAsync(doctor.UserId, request));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_TrimsAndPersists()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var link = CreateLink(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(link);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child.ChildId,
            NoteText = "  Заметка с пробелами  ",
            IsImportant = true
        };
        var before = DateTime.UtcNow;

        var result = await sut.CreateAsync(doctor.UserId, request);

        Assert.NotEqual(Guid.Empty, result.NoteId);
        Assert.Equal(child.ChildId, result.ChildId);
        Assert.Equal(doctor.UserId, result.DoctorUserId);
        Assert.Equal("Заметка с пробелами", result.NoteText);  // trimmed
        Assert.True(result.IsImportant);
        Assert.Null(result.UpdatedAt);  // null при Create
        Assert.True(result.CreatedAt >= before.AddSeconds(-2) && result.CreatedAt <= DateTime.UtcNow.AddSeconds(2));

        // Запись реально в БД
        using var verifyDb = NewDb();
        var saved = await verifyDb.DoctorNotes.FindAsync(result.NoteId);
        Assert.NotNull(saved);
        Assert.Equal("Заметка с пробелами", saved!.NoteText);
    }

    [Fact]
    public async Task CreateAsync_WithMeasurement_AttachesNote()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var link = CreateLink(doctor.UserId, child.ChildId);
        var measurement = new Measurement
        {
            MeasurementId = Guid.NewGuid(),
            ChildId = child.ChildId,
            GlucoseValue = 5.5m,
            MeasurementTime = DateTime.UtcNow
        };
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.Measurements.Add(measurement);
            db.DoctorChildLinks.Add(link);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateDoctorNoteRequest
        {
            ChildId = child.ChildId,
            MeasurementId = measurement.MeasurementId,
            NoteText = "Привязано к измерению"
        };

        var result = await sut.CreateAsync(doctor.UserId, request);

        Assert.Equal(measurement.MeasurementId, result.MeasurementId);
    }

    [Fact]
    public async Task CreateAsync_WithLinkedParents_CreatesUnreadNotificationForEachParent()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var firstParent = CreateParent();
        var secondParent = CreateParent();
        var link = CreateLink(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.AddRange(doctor, firstParent, secondParent);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(link);
            db.ParentChildLinks.AddRange(
                new ParentChildLink
                {
                    LinkId = Guid.NewGuid(),
                    ParentUserId = firstParent.UserId,
                    ChildId = child.ChildId,
                    CreatedAt = DateTime.UtcNow
                },
                new ParentChildLink
                {
                    LinkId = Guid.NewGuid(),
                    ParentUserId = secondParent.UserId,
                    ChildId = child.ChildId,
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var result = await CreateSut().CreateAsync(
            doctor.UserId,
            new CreateDoctorNoteRequest
            {
                ChildId = child.ChildId,
                NoteText = "Новая рекомендация",
                IsImportant = true
            });

        using var verifyDb = NewDb();
        var notifications = await verifyDb.UserNotifications
            .OrderBy(notification => notification.RecipientUserId)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(child.ChildId, notification.ChildId);
            Assert.Equal("DoctorNote", notification.SourceType);
            Assert.Equal(result.NoteId, notification.SourceId);
            Assert.Equal("warn", notification.Type);
            Assert.False(notification.IsRead);
        });
        Assert.Equal(
            new[] { firstParent.UserId, secondParent.UserId }.OrderBy(id => id),
            notifications.Select(notification => notification.RecipientUserId));
    }

    // ───────────────────────────────────────────────────────────────────
    // UpdateAsync — author guard
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var sut = CreateSut();
        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateDoctorNoteRequest { NoteText = "test" });
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_DifferentDoctor_ThrowsUnauthorized()
    {
        var child = CreateChild();
        var author = CreateDoctor(firstName: "Автор");
        var intruder = CreateDoctor(firstName: "Чужак");
        var note = CreateNote(author.UserId, child.ChildId, text: "оригинал");
        using (var db = NewDb())
        {
            db.Users.AddRange(author, intruder);
            db.Children.Add(child);
            db.DoctorNotes.Add(note);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.UpdateAsync(
                note.NoteId,
                intruder.UserId,
                new UpdateDoctorNoteRequest { NoteText = "перехвачено" }));
    }

    [Fact]
    public async Task UpdateAsync_ByAuthor_TrimsSetsUpdatedAt()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var note = CreateNote(doctor.UserId, child.ChildId,
            text: "старое", isImportant: false, updatedAt: null);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(note);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var result = await sut.UpdateAsync(
            note.NoteId,
            doctor.UserId,
            new UpdateDoctorNoteRequest { NoteText = "  новое  ", IsImportant = true });

        Assert.NotNull(result);
        Assert.Equal("новое", result!.NoteText);
        Assert.True(result.IsImportant);
        Assert.NotNull(result.UpdatedAt);
        Assert.True(result.UpdatedAt >= before.AddSeconds(-2) && result.UpdatedAt <= DateTime.UtcNow.AddSeconds(2));

        // Запись обновлена в БД
        using var verifyDb = NewDb();
        var saved = await verifyDb.DoctorNotes.FindAsync(note.NoteId);
        Assert.Equal("новое", saved!.NoteText);
        Assert.True(saved.IsImportant);
        Assert.NotNull(saved.UpdatedAt);
    }

    // ───────────────────────────────────────────────────────────────────
    // DeleteAsync — author-or-admin guard
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        var sut = CreateSut();
        var result = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_DifferentDoctor_NotAdmin_ThrowsUnauthorized()
    {
        var child = CreateChild();
        var author = CreateDoctor();
        var intruder = CreateDoctor();
        var note = CreateNote(author.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.AddRange(author, intruder);
            db.Children.Add(child);
            db.DoctorNotes.Add(note);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.DeleteAsync(note.NoteId, intruder.UserId, isAdmin: false));
    }

    [Fact]
    public async Task DeleteAsync_DifferentDoctor_AsAdmin_Deletes()
    {
        var child = CreateChild();
        var author = CreateDoctor();
        var admin = CreateDoctor();
        var note = CreateNote(author.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.AddRange(author, admin);
            db.Children.Add(child);
            db.DoctorNotes.Add(note);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeleteAsync(note.NoteId, admin.UserId, isAdmin: true);

        Assert.True(result);
        using var verifyDb = NewDb();
        var saved = await verifyDb.DoctorNotes.FindAsync(note.NoteId);
        Assert.Null(saved);
    }

    [Fact]
    public async Task DeleteAsync_ByAuthor_Deletes()
    {
        var child = CreateChild();
        var doctor = CreateDoctor();
        var note = CreateNote(doctor.UserId, child.ChildId);
        using (var db = NewDb())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorNotes.Add(note);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.DeleteAsync(note.NoteId, doctor.UserId, isAdmin: false);

        Assert.True(result);
        using var verifyDb = NewDb();
        var saved = await verifyDb.DoctorNotes.FindAsync(note.NoteId);
        Assert.Null(saved);
    }

    private sealed class PassthroughCryptoService : ICryptoService
    {
        public string Encrypt(string plainText) => plainText;

        public string Decrypt(string cipherText) => cipherText;
    }
}
