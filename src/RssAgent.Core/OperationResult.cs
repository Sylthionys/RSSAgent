namespace RssAgent.Core;

public sealed record OperationResult(bool Success, string? Error, string? Detail)
{
    public static OperationResult Ok() => new(true, null, null);

    public static OperationResult Fail(string error, string? detail = null) =>
        new(false, error, detail);
}
