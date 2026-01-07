using System;
using System.Linq;
using RssAgent.Core.Models;

namespace RssAgent.Core.Services;

public static class GeminiRoutePlanValidator
{
    public static OperationResult Validate(GeminiRoutePlan plan)
    {
        if (plan.Items == null || plan.Items.Count == 0)
        {
            return OperationResult.Fail("Gemini JSON 缺少 items。");
        }

        if (!string.Equals(plan.Mode, "single", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(plan.Mode, "recommend", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("Gemini JSON 的 mode 必须是 single 或 recommend。");
        }

        if (string.Equals(plan.Mode, "single", StringComparison.OrdinalIgnoreCase) && plan.Items.Count != 1)
        {
            return OperationResult.Fail("single 模式必须只有 1 个 item。");
        }

        if (string.Equals(plan.Mode, "recommend", StringComparison.OrdinalIgnoreCase) &&
            (plan.Items.Count < 5 || plan.Items.Count > 12))
        {
            return OperationResult.Fail("recommend 模式需要 5-12 个 items。");
        }

        foreach (var item in plan.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                return OperationResult.Fail("Gemini JSON 的 title 不能为空。");
            }

            var hasRssHub = item.RssHub != null && !string.IsNullOrWhiteSpace(item.RssHub.Route);
            var hasDirect = !string.IsNullOrWhiteSpace(item.DirectRssUrl);

            if (!hasRssHub && !hasDirect)
            {
                return OperationResult.Fail("每个 item 需要 rsshub.route 或 directRssUrl。");
            }

            if (hasRssHub && item.RssHub != null)
            {
                if (!item.RssHub.Route.StartsWith("/", StringComparison.Ordinal))
                {
                    return OperationResult.Fail($"rsshub.route 必须以 / 开头: {item.RssHub.Route}");
                }

                if (item.RssHub.Query.Values.Any(value => value is null))
                {
                    return OperationResult.Fail("rsshub.query 的 value 必须是字符串。");
                }
            }
        }

        return OperationResult.Ok();
    }
}
