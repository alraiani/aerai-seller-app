using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.ExchangeRates;

/// <summary>Settings for the daily exchange rates feed (<c>ExchangeRates</c> section).</summary>
public sealed class ExchangeRateOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ExchangeRates";

    /// <summary>Where rates come from.</summary>
    public ExchangeRateMode Mode { get; set; } = ExchangeRateMode.Live;

    /// <summary>Base address of the Frankfurter API (with a trailing slash).</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://api.frankfurter.dev/v1/");
}
