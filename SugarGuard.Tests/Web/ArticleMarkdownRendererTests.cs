using SugarGuard.Web.Services;

namespace SugarGuard.Tests.Web;

/// <summary>
/// Регрессионные тесты Markdown базы знаний.
/// </summary>
public sealed class ArticleMarkdownRendererTests
{
    private readonly ArticleMarkdownRenderer _renderer = new();

    [Fact]
    public void Parse_NormalizesLegacyEscapedNewlinesAndRendersArticleImage()
    {
        const string imageUrl = "/uploads/articles/3faae1b9f6dd451eb97c4b653586ad5a.jpg";
        var blocks = _renderer.Parse($"Вступление\\n## Заголовок\\nТекст перед рисунком![]({imageUrl})\\n");

        Assert.Collection(
            blocks,
            block => Assert.Equal(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Paragraph, "Вступление"), block),
            block => Assert.Equal(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Heading, "Заголовок"), block),
            block => Assert.Equal(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Paragraph, "Текст перед рисунком"), block),
            block => Assert.Equal(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Image, string.Empty, imageUrl), block));
    }

    [Fact]
    public void Parse_DoesNotTreatExternalUrlAsArticleImage()
    {
        var blocks = _renderer.Parse("![Стороннее изображение](https://example.test/image.png)");

        var block = Assert.Single(blocks);
        Assert.Equal(ArticleMarkdownBlockKind.Paragraph, block.Kind);
    }

    [Fact]
    public void RenderInline_EscapesHtmlBeforeApplyingSupportedFormatting()
    {
        var html = _renderer.RenderInline("**<script>alert(1)</script>** и *курсив*");

        Assert.Contains("<strong>&lt;script&gt;alert(1)&lt;/script&gt;</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<em>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }
}
