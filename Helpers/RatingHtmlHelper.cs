using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Roma.Helpers;

public static class RatingHtmlHelper
{
    public static IHtmlContent RatingBadge(this IHtmlHelper html, double rating, bool showDecimal = true)
    {
        var clampedRating = Math.Clamp(rating, 1, 5);
        var ratingText = showDecimal
            ? clampedRating.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Clamp((int)Math.Round(clampedRating), 1, 5).ToString(CultureInfo.InvariantCulture);

        var badge = new TagBuilder("span");
        badge.AddCssClass($"badge {GetBadgeClass(clampedRating)}");
        badge.InnerHtml.Append($"★ {ratingText} / 5");

        return badge;
    }

    public static IHtmlContent RatingBadge(this IHtmlHelper html, int rating)
    {
        return html.RatingBadge((double)rating, showDecimal: false);
    }

    private static string GetBadgeClass(double rating) => rating switch
    {
        >= 4.5 and <= 5 => "bg-success",
        >= 3.5 and < 4.5 => "bg-primary",
        >= 2.5 and < 3.5 => "bg-warning text-dark",
        >= 1 and < 2.5 => "bg-danger",
        _ => "bg-secondary"
    };
}