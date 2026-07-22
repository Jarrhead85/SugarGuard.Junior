using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace SugarGuard.Web.Services;

/// <summary>
/// Безопасно разбирает ограниченный набор Markdown, поддерживаемый базой знаний.
/// HTML из текста статьи не интерпретируется, а адреса иллюстраций ограничены
/// публичным каталогом загруженных материалов.
/// </summary>
public sealed class ArticleMarkdownRenderer
{
    private static readonly Regex Image = new(
        "!\\[(?<alt>[^\\]]{0,200})\\]\\((?<url>/uploads/articles/[a-z0-9_-]+\\.(?:png|jpe?g|webp))\\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Bold = new(
        "\\*\\*(.+?)\\*\\*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Italic = new(
        "(?<!\\*)\\*(?!\\*)(.+?)(?<!\\*)\\*(?!\\*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Преобразует текст статьи в безопасные структурные блоки.
    /// Старые статьи с буквальными последовательностями <c>\n</c> нормализуются
    /// при чтении, поэтому не требуют ручного редактирования.
    /// </summary>
    public IReadOnlyList<ArticleMarkdownBlock> Parse(string? content)
    {
        var blocks = new List<ArticleMarkdownBlock>();

        foreach (var rawLine in NormalizeLineEndings(content).Split('\n'))
        {
            ParseLine(rawLine, blocks);
        }

        return blocks;
    }

    /// <summary>
    /// Экранирует пользовательский текст и применяет разрешённые inline-форматы.
    /// </summary>
    public string RenderInline(string? text)
    {
        var escaped = HtmlEncoder.Default.Encode(text ?? string.Empty);
        escaped = Bold.Replace(escaped, "<strong>$1</strong>");
        return Italic.Replace(escaped, "<em>$1</em>");
    }

    private static void ParseLine(string rawLine, ICollection<ArticleMarkdownBlock> blocks)
    {
        var line = rawLine.Trim();
        if (line.Length == 0)
        {
            return;
        }

        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            blocks.Add(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Heading, line[3..]));
            return;
        }

        if (line.StartsWith("- ", StringComparison.Ordinal))
        {
            blocks.Add(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.ListItem, line[2..]));
            return;
        }

        var cursor = 0;
        foreach (Match match in Image.Matches(line))
        {
            AddParagraph(line[cursor..match.Index], blocks);
            blocks.Add(new ArticleMarkdownBlock(
                ArticleMarkdownBlockKind.Image,
                match.Groups["alt"].Value,
                match.Groups["url"].Value));
            cursor = match.Index + match.Length;
        }

        AddParagraph(line[cursor..], blocks);
    }

    private static void AddParagraph(string text, ICollection<ArticleMarkdownBlock> blocks)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0)
        {
            blocks.Add(new ArticleMarkdownBlock(ArticleMarkdownBlockKind.Paragraph, trimmed));
        }
    }

    private static string NormalizeLineEndings(string? content) =>
        (content ?? string.Empty)
        // Ранние версии редактора передавали в текстовое поле два символа: "\\" и "n".
        .Replace("\\r\\n", "\n", StringComparison.Ordinal)
        .Replace("\\n", "\n", StringComparison.Ordinal)
        .Replace("\\r", "\n", StringComparison.Ordinal)
        .ReplaceLineEndings("\n");
}

public sealed record ArticleMarkdownBlock(
    ArticleMarkdownBlockKind Kind,
    string Text,
    string? Url = null);

public enum ArticleMarkdownBlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Image
}
