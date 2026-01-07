using System.Linq;
using System.Threading.Tasks;
using RssAgent.Core.Services;

namespace RssAgent.Integrations.LanguageDetection;

public sealed class HeuristicLanguageDetector : ILanguageDetector
{
    public Task<string?> DetectAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult<string?>(null);
        }

        if (text.Any(ch => ch is >= '\u4E00' and <= '\u9FFF'))
        {
            return Task.FromResult<string?>("zh");
        }

        if (text.Any(ch => ch is >= '\u3040' and <= '\u30FF'))
        {
            return Task.FromResult<string?>("ja");
        }

        if (text.Any(ch => ch is >= '\uAC00' and <= '\uD7AF'))
        {
            return Task.FromResult<string?>("ko");
        }

        if (text.Any(ch => ch is >= '\u0400' and <= '\u04FF'))
        {
            return Task.FromResult<string?>("ru");
        }

        var latinLetters = text.Count(ch => ch is >= 'A' and <= 'Z' || ch is >= 'a' and <= 'z');
        if (latinLetters > 0)
        {
            return Task.FromResult<string?>("en");
        }

        return Task.FromResult<string?>(null);
    }
}
