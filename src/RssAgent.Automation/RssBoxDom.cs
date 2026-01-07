namespace RssAgent.Automation;

internal static class RssBoxDom
{
    public const string FeedListTableSelector = "#result_list";
    public const string FeedListRowSelector = "#result_list tbody tr";
    public const string FeedTitleSelector = "th.field-name a";
    public const string FeedFetchUrlSelector = "td.field-fetch_feed a[href^='http']";
    public const string FeedTagsSelector = "td.field-show_tags";
    public const string FeedStatusSelector = "td.field-fetch_info span";

    public const string TagListRowSelector = "#result_list tbody tr";
    public const string TagNameSelector = "th.field-name a";

    public const string DigestListRowSelector = "#result_list tbody tr";
    public const string DigestNameSelector = "th.field-name a";

    public const string FilterListRowSelector = "#result_list tbody tr";
    public const string FilterNameSelector = "th.field-name a";
}
