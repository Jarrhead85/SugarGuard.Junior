using Microsoft.AspNetCore.Http;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

public interface IAccountProfileService
{
    Task<AccountProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<AccountProfileResponse?> UpdateAsync(Guid userId, UpdateAccountProfileRequest request, CancellationToken cancellationToken);
    Task<string> UploadPhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken);
    Task<bool> DeletePhotoAsync(Guid userId, CancellationToken cancellationToken);
}
