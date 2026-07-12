using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;
using SugarGuard.API.Security;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Реализация сервиса кодов приглашения
    /// </summary>
    public class InviteCodeService : IInviteCodeService
    {
        private const int CodeTtlHours = InviteCodeLimits.CodeTtlHours;

        private const int CodeLength = InviteCodeLimits.CodeLength; // Длина кода

        private const string CodeAlphabet = InviteCodeLimits.Alphabet; //Алфавит

        private static readonly HashSet<UserRole> _allowedTargetRoles = new()
        {
            UserRole.Parent,
            UserRole.Doctor
        };

        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ILogger<InviteCodeService> _logger;
        private readonly ICryptoService _crypto;

        public InviteCodeService(
            AppDbContext context,
            IAuditService auditService,
            ICryptoService crypto,
            ILogger<InviteCodeService> logger)
        {
            _context = context;
            _auditService = auditService;
            _crypto = crypto;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<InviteCodeResponse> GenerateAsync(
            Guid childId,
            UserRole targetRole,
            CancellationToken cancellationToken = default)
        {
            if (!_allowedTargetRoles.Contains(targetRole))
                throw new ArgumentException(
                    $"TargetRole должен быть Parent или Doctor, получено: {targetRole}.",
                    nameof(targetRole));

            // Аннулируем все активные коды, чтобы одновременно существовал только один активный код
            var existingActive = await _context.InviteCodes
                .Where(c => c.ChildId == childId
                            && c.TargetRole == targetRole
                            && c.Status == "Pending"
                            && c.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            if (existingActive.Count > 0)
            {
                foreach (var old in existingActive)
                    old.Status = "Rejected";

                _logger.LogInformation(
                    "Аннулировано {Count} старых кодов для ChildId={ChildId}, Role={Role}",
                    existingActive.Count, childId, targetRole);
            }

            var code = new InviteCode
            {
                ChildId = childId,
                TargetRole = targetRole,
                Code = await GenerateUniqueCodeAsync(cancellationToken),
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(CodeTtlHours),
                CreatedAt = DateTime.UtcNow
            };

            _context.InviteCodes.Add(code);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.WriteAsync(
                "invitecode.generated",
                nameof(InviteCode),
                code.InviteCodeId.ToString(),
                $"Child={childId} TargetRole={targetRole}",
                cancellationToken);

            _logger.LogInformation(
                "Код приглашения создан. InviteCodeId={Id} ChildId={ChildId} Role={Role} ExpiresAt={ExpiresAt}",
                code.InviteCodeId, childId, targetRole, code.ExpiresAt);

            return MapToResponse(code);
        }

        /// <inheritdoc/>
        public async Task<ClaimInviteCodeResult> ClaimAsync(
            string code,
            Guid claimedByUserId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return ClaimInviteCodeResult.Fail("code_not_found", "Код не может быть пустым.");

            var normalizedCode = InviteCodeLimits.Normalize(code);

            // Ищем код в БД
            var invite = await _context.InviteCodes
                .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

            if (invite is null)
            {
                _logger.LogWarning(
                    "Попытка использовать несуществующий код приглашения. UserId={UserId}",
                    claimedByUserId);
                return ClaimInviteCodeResult.Fail("code_not_found", "Код не найден.");
            }

            // Проверяем статус
            if (invite.Status != "Pending")
            {
                _logger.LogWarning(
                    "Код приглашения уже использован или отозван. InviteCodeId={InviteCodeId} Status={Status} UserId={UserId}",
                    invite.InviteCodeId, invite.Status, claimedByUserId);
                return ClaimInviteCodeResult.Fail(
                    "code_already_used",
                    $"Код недействителен (статус: {invite.Status}).");
            }

            // Проверяем TTL
            if (invite.ExpiresAt <= DateTime.UtcNow)
            {
                invite.Status = "Expired";
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Код приглашения истёк. InviteCodeId={InviteCodeId} ExpiresAt={ExpiresAt} UserId={UserId}",
                    invite.InviteCodeId, invite.ExpiresAt, claimedByUserId);
                return ClaimInviteCodeResult.Fail("code_expired", "Срок действия кода истёк.");
            }

            // Проверяем роль пользователя
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == claimedByUserId, cancellationToken);

            if (user is null)
                return ClaimInviteCodeResult.Fail("access_denied", "Пользователь не найден.");

            if (user.Role != invite.TargetRole)
            {
                _logger.LogWarning(
                    "Несоответствие роли: код для {TargetRole}, пользователь имеет {UserRole}. UserId={UserId}",
                    invite.TargetRole, user.Role, claimedByUserId);
                return ClaimInviteCodeResult.Fail(
                    "role_mismatch",
                    $"Этот код предназначен для роли {invite.TargetRole}, " +
                    $"а ваша роль — {user.Role}.");
            }

            // Создаём связку — в зависимости от роли
            Guid linkId;
            string linkType;

            if (invite.TargetRole == UserRole.Parent)
            {
                // Проверяем, нет ли уже такой связки
                var linkExists = await _context.ParentChildLinks
                    .AnyAsync(l => l.ParentUserId == claimedByUserId
                                   && l.ChildId == invite.ChildId,
                              cancellationToken);

                if (linkExists)
                {
                    // Считаем это успехом, код всё равно закрываем
                    invite.Status = "Claimed";
                    invite.ClaimedByUserId = claimedByUserId;
                    await _context.SaveChangesAsync(cancellationToken);

                    var existing = await _context.ParentChildLinks
                        .AsNoTracking()
                        .FirstAsync(l => l.ParentUserId == claimedByUserId
                                         && l.ChildId == invite.ChildId,
                                    cancellationToken);

                    _logger.LogInformation(
                        "Связка ParentChildLink уже существует. LinkId={LinkId}", existing.LinkId);

                    return ClaimInviteCodeResult.Ok(invite.ChildId, existing.LinkId, "ParentChildLink");
                }

                var parentLink = new ParentChildLink
                {
                    LinkId = Guid.NewGuid(),
                    ParentUserId = claimedByUserId,
                    ChildId = invite.ChildId,
                    CreatedAt = DateTime.UtcNow,
                    LinkedByUserId = claimedByUserId
                };

                _context.ParentChildLinks.Add(parentLink);
                linkId = parentLink.LinkId;
                linkType = "ParentChildLink";
            }
            else // Doctor
            {
                // Проверяем, нет ли уже такой связки
                var linkExists = await _context.DoctorChildLinks
                    .AnyAsync(l => l.DoctorUserId == claimedByUserId
                                   && l.ChildId == invite.ChildId,
                              cancellationToken);

                if (linkExists)
                {
                    invite.Status = "Claimed";
                    invite.ClaimedByUserId = claimedByUserId;
                    await _context.SaveChangesAsync(cancellationToken);

                    var existing = await _context.DoctorChildLinks
                        .AsNoTracking()
                        .FirstAsync(l => l.DoctorUserId == claimedByUserId
                                         && l.ChildId == invite.ChildId,
                                    cancellationToken);

                    _logger.LogInformation(
                        "Связка DoctorChildLink уже существует. LinkId={LinkId}", existing.LinkId);

                    return ClaimInviteCodeResult.Ok(invite.ChildId, existing.LinkId, "DoctorChildLink");
                }

                var doctorLink = new DoctorChildLink
                {
                    DoctorUserId = claimedByUserId,
                    ChildId = invite.ChildId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DoctorChildLinks.Add(doctorLink);
                linkId = doctorLink.LinkId;
                linkType = "DoctorChildLink";
            }

            // Закрываем код
            invite.Status = "Claimed";
            invite.ClaimedByUserId = claimedByUserId;

            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.WriteAsync(
                "invitecode.claimed",
                nameof(InviteCode),
                invite.InviteCodeId.ToString(),
                $"Child={invite.ChildId} ClaimedBy={claimedByUserId} LinkType={linkType} LinkId={linkId}",
                cancellationToken);

            _logger.LogInformation(
                "Код принят. InviteCodeId={Id} ChildId={ChildId} UserId={UserId} LinkType={LinkType} LinkId={LinkId}",
                invite.InviteCodeId, invite.ChildId, claimedByUserId, linkType, linkId);

            return ClaimInviteCodeResult.Ok(invite.ChildId, linkId, linkType);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<InviteCodeResponse>> GetActiveAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var codes = await _context.InviteCodes
                .AsNoTracking()
                .Where(c => c.ChildId == childId
                            && c.Status == "Pending"
                            && c.ExpiresAt > now)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            return codes.Select(MapToResponse).ToList();
        }

        /// <inheritdoc/>
        public async Task<bool> RevokeAsync(
            Guid inviteCodeId,
            CancellationToken cancellationToken = default)
        {
            var invite = await _context.InviteCodes
                .FirstOrDefaultAsync(c => c.InviteCodeId == inviteCodeId, cancellationToken);

            if (invite is null)
            {
                _logger.LogWarning("RevokeAsync: код не найден. InviteCodeId={Id}", inviteCodeId);
                return false;
            }

            if (invite.Status != "Pending")
            {
                _logger.LogWarning(
                    "RevokeAsync: код уже не активен. InviteCodeId={Id} Status={Status}",
                    inviteCodeId, invite.Status);
                return false;
            }

            invite.Status = "Rejected";
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.WriteAsync(
                "invitecode.revoked",
                nameof(InviteCode),
                inviteCodeId.ToString(),
                $"Child={invite.ChildId} TargetRole={invite.TargetRole}",
                cancellationToken);

            _logger.LogInformation(
                "Код приглашения отозван. InviteCodeId={Id} ChildId={ChildId}",
                inviteCodeId, invite.ChildId);

            return true;
        }

        /// <inheritdoc/>
        public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var updated = await _context.InviteCodes
                .Where(c => c.Status == "Pending" && c.ExpiresAt <= now)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(c => c.Status, "Expired"),
                    cancellationToken);

            if (updated > 0)
                _logger.LogInformation(
                    "PurgeExpiredAsync: помечено истёкшими {Count} кодов приглашений.", updated);

            return updated;
        }

        /// <inheritdoc/>
        public async Task<ChildAccessLinksResponse> GetChildLinksAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var parentEntities = await _context.ParentChildLinks
                .AsNoTracking()
                .Include(l => l.ParentUser)
                .Where(l => l.ChildId == childId)
                .Where(l => l.ParentUser.Role == UserRole.Parent)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync(cancellationToken);

            var parentLinks = parentEntities.Select(l => MapLinkedUser(l.LinkId, l.ParentUser, l.CreatedAt)).ToList();

            var doctorEntities = await _context.DoctorChildLinks
                .AsNoTracking()
                .Include(l => l.DoctorUser)
                .Where(l => l.ChildId == childId)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync(cancellationToken);

            var doctorLinks = doctorEntities.Select(l => MapLinkedUser(l.LinkId, l.DoctorUser, l.CreatedAt)).ToList();

            return new ChildAccessLinksResponse
            {
                ChildId = childId,
                ParentLinks = parentLinks,
                DoctorLinks = doctorLinks
            };
        }

        /// <inheritdoc/>
        public async Task<UnlinkResult> UnlinkAsync(
            Guid childId,
            string linkType,
            Guid linkId,
            CancellationToken cancellationToken = default)
        {
            var normalizedType = (linkType ?? string.Empty).Trim().ToLowerInvariant();

            if (normalizedType == "parent")
            {
                var link = await _context.ParentChildLinks
                    .Include(l => l.ParentUser)
                    .FirstOrDefaultAsync(l => l.LinkId == linkId && l.ChildId == childId,
                        cancellationToken);

                if (link is null || link.ParentUser.Role != UserRole.Parent)
                {
                    _logger.LogWarning(
                        "UnlinkAsync: parent-связка не найдена или не является реальным родителем. ChildId={ChildId} LinkId={LinkId}.",
                        childId, linkId);
                    return UnlinkResult.NotFound;
                }

                _context.ParentChildLinks.Remove(link);
                await _context.SaveChangesAsync(cancellationToken);

                await _auditService.WriteAsync(
                    action: "invitecode.parent_link_removed",
                    targetType: "ParentChildLink",
                    targetId: linkId.ToString(),
                    details: $"Child={childId}",
                    cancellationToken: CancellationToken.None);

                _logger.LogInformation(
                    "UnlinkAsync: parent-связка удалена. ChildId={ChildId} LinkId={LinkId}.",
                    childId, linkId);
                return UnlinkResult.Success;
            }

            if (normalizedType == "doctor")
            {
                var link = await _context.DoctorChildLinks
                    .FirstOrDefaultAsync(l => l.LinkId == linkId && l.ChildId == childId,
                        cancellationToken);

                if (link is null)
                {
                    _logger.LogWarning(
                        "UnlinkAsync: doctor-связка не найдена. ChildId={ChildId} LinkId={LinkId}.",
                        childId, linkId);
                    return UnlinkResult.NotFound;
                }

                _context.DoctorChildLinks.Remove(link);
                await _context.SaveChangesAsync(cancellationToken);

                await _auditService.WriteAsync(
                    action: "invitecode.doctor_link_removed",
                    targetType: "DoctorChildLink",
                    targetId: linkId.ToString(),
                    details: $"Child={childId}",
                    cancellationToken: CancellationToken.None);

                _logger.LogInformation(
                    "UnlinkAsync: doctor-связка удалена. ChildId={ChildId} LinkId={LinkId}.",
                    childId, linkId);
                return UnlinkResult.Success;
            }

            _logger.LogWarning(
                "UnlinkAsync: недопустимый linkType='{LinkType}'. ChildId={ChildId} LinkId={LinkId}.",
                linkType, childId, linkId);
            return UnlinkResult.InvalidLinkType;
        }

        // Приватные вспомогательные методы
        /// <summary>
        /// Генерирует криптографически случайный 8-символьный код
        /// </summary>
        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            const int maxAttempts = 8;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var code = GenerateCode();
                var exists = await _context.InviteCodes
                    .AsNoTracking()
                    .AnyAsync(x => x.Code == code, cancellationToken);

                if (!exists)
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Не удалось сгенерировать уникальный код приглашения.");
        }

        private LinkedAccessUserResponse MapLinkedUser(Guid linkId, User user, DateTime linkedAt)
        {
            var firstName = Decrypt(user.EncryptedFirstName);
            var lastName = Decrypt(user.EncryptedLastName);
            var displayName = string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new LinkedAccessUserResponse
            {
                LinkId = linkId,
                UserId = user.UserId,
                EmailForLogin = user.EmailForLogin,
                TelegramId = user.TelegramId,
                UserRole = user.Role.ToString(),
                LinkedAt = linkedAt,
                DisplayName = displayName,
                PhotoUrl = user.ProfilePhotoUrl,
                Specialty = user.DoctorSpecialty
            };
        }

        private string Decrypt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            try { return _crypto.Decrypt(value); }
            catch (Exception exception) when (exception is FormatException or System.Security.Cryptography.CryptographicException) { return value; }
        }

        private static string GenerateCode()
        {
            var result = new char[CodeLength];
            var alphabetLength = CodeAlphabet.Length;

            var buffer = new byte[CodeLength * 2];

            var filled = 0;
            while (filled < CodeLength)
            {
                System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);

                foreach (var b in buffer)
                {
                    var limit = 256 - (256 % alphabetLength);
                    if (b >= limit) continue;

                    result[filled++] = CodeAlphabet[b % alphabetLength];
                    if (filled == CodeLength) break;
                }
            }

            return new string(result);
        }

        /// <summary>
        /// Маппинг модели в DTO ответа
        /// </summary>
        private static InviteCodeResponse MapToResponse(InviteCode invite) =>
            new()
            {
                InviteCodeId = invite.InviteCodeId,
                ChildId = invite.ChildId,
                Code = InviteCodeLimits.Format(invite.Code),
                TargetRole = invite.TargetRole,
                Status = invite.Status,
                ExpiresAt = invite.ExpiresAt,
                CreatedAt = invite.CreatedAt,
                IsActive = invite.IsActive
            };
    }
}
