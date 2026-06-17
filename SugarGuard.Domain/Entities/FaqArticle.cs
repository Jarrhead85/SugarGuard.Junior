using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities;

[Table("faq_articles")]
public class FaqArticle
{
    [Key]
    [Column("faq_article_id")]
    public Guid FaqArticleId { get; set; } = Guid.NewGuid();

    [Column("title")]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    [Column("is_published")]
    public bool IsPublished { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
