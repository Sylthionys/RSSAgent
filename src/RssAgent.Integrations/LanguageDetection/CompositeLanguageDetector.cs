using System.Threading.Tasks;
using RssAgent.Core.Services;
using RssAgent.Integrations.Gemini;

namespace RssAgent.Integrations.LanguageDetection;

public sealed class CompositeLanguageDetector : ILanguageDetector
{
    private readonly HeuristicLanguageDetector _heuristic;
    private readonly GeminiClient? _geminiClient;

    public CompositeLanguageDetector(HeuristicLanguageDetector heuristic, GeminiClient? geminiClient)
    {
        _heuristic = heuristic;
        _geminiClient = geminiClient;
    }

    public async Task<string?> DetectAsync(string text)
    {
        var heuristic = await _heuristic.DetectAsync(text);
        if (!string.IsNullOrWhiteSpace(heuristic))
        {
            return heuristic;
        }

        if (_geminiClient == null)
        {
            return null;
        }

        return await _geminiClient.DetectLanguageAsync(text);
    }
}
