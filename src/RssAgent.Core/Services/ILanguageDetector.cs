using System.Threading.Tasks;

namespace RssAgent.Core.Services;

public interface ILanguageDetector
{
    Task<string?> DetectAsync(string text);
}
