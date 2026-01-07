using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Core.Services;
using RssAgent.Integrations.Http;

namespace RssAgent.Integrations.Gemini;

public sealed class GeminiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ResilientHttpClient _http;
    private readonly AppLogger _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiClient(string apiKey, string model, AppLogger logger)
    {
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-3-flash-preview" : model;
        _logger = logger;
        _http = new ResilientHttpClient(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        });
    }

    public async Task<(GeminiRoutePlan? Plan, OperationResult Result)> GeneratePlanAsync(string userInput, string mode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return (null, OperationResult.Fail("Gemini API Key 为空。"));
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = string.Format(GeminiPrompts.UserPromptTemplate, userInput, mode) } }
                }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = GeminiPrompts.SystemPrompt } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                response_mime_type = "application/json"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Gemini 请求失败。", ex.Message);
            return (null, OperationResult.Fail("Gemini 请求失败。", ex.Message));
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await _logger.WarnAsync("Gemini 返回失败。", content);
            return (null, OperationResult.Fail($"Gemini 返回 {(int)response.StatusCode}", content));
        }

        var json = ExtractTextFromResponse(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, OperationResult.Fail("Gemini 返回内容为空或格式异常。"));
        }

        try
        {
            var plan = JsonSerializer.Deserialize<GeminiRoutePlan>(json, JsonOptions);
            if (plan == null)
            {
                return (null, OperationResult.Fail("Gemini JSON 解析失败。"));
            }

            var validation = GeminiRoutePlanValidator.Validate(plan);
            return validation.Success ? (plan, validation) : (null, validation);
        }
        catch (JsonException ex)
        {
            return (null, OperationResult.Fail("Gemini JSON 解析失败。", ex.Message));
        }
    }

    public async Task<string?> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var prompt = $"Detect language and return JSON only: {{\"lang\":\"xx\"}}. Text: {text}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.0,
                response_mime_type = "application/json"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("Gemini 语言检测失败。", ex.Message);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = ExtractTextFromResponse(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("lang", out var langElement))
            {
                return langElement.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? ExtractTextFromResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return text?.Trim();
        }
        catch
        {
            return null;
        }
    }
}
