using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

public class FaqArticleRequest
{
    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}

public class FaqArticleResponse
{
    public Guid FaqArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
