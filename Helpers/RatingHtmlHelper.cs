using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Roma.Helpers;

public static class RatingHtmlHelper
{
    public static IHtmlContent RatingBadge(this IHtmlHelper html, double rating, bool showDecimal = true)
    {
        var clampedRating = Math.Clamp(rating, 1, 5);
        var filledStars = Math.Clamp((int)Math.Round(clampedRating, MidpointRounding.AwayFromZero), 1, 5);
        var stars = string.Concat(new string('★', filledStars), new string('☆', 5 - filledStars));
        var ratingText = showDecimal
            ? clampedRating.ToString("0.0", CultureInfo.InvariantCulture)
            : filledStars.ToString(CultureInfo.InvariantCulture);

        var badge = new TagBuilder("span");
        badge.Attributes["style"] = "color:#f4c430;";
        badge.Attributes["title"] = $"{ratingText} / 5";
        badge.Attributes["aria-label"] = $"Puan: {ratingText} / 5";
        badge.InnerHtml.Append(stars);

        return badge;
    }

    public static IHtmlContent RatingBadge(this IHtmlHelper html, int rating)
    {
        return html.RatingBadge((double)rating, showDecimal: false);
    }
}