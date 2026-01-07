namespace RssAgent.Integrations.Gemini;

public static class GeminiPrompts
{
    public const string SystemPrompt = """
You are an assistant that converts user intent into RSS feed subscription plans.
Output ONLY valid JSON, no markdown or extra text.
The JSON must follow this schema:
{
  "mode": "single" | "recommend",
  "items": [
    {
      "title": "string",
      "rsshub": {
        "route": "/xxx/yyy",
        "query": { "k": "v" }
      },
      "directRssUrl": "string | null",
      "tags": ["string"],
      "notes": "string",
      "expectedSourceLang": "string | null"
    }
  ]
}
Rules:
- If you provide rsshub.route it MUST start with "/" and must not include the base URL.
- If you provide directRssUrl, it must be a full URL.
- Each item must have either rsshub.route or directRssUrl.
- "items" length must be 1 for mode "single", and 5-12 for mode "recommend".
- "query" keys and values must be strings.
- "tags" should be 1-5 concise tags.
""";

    public const string UserPromptTemplate = """
User request: {0}
Mode: {1}
Return ONLY JSON.
""";
}
