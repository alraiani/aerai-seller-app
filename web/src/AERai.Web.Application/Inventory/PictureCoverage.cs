namespace AERai.Web.Application.Inventory;

/// <summary>How many SKUs have a picture, and what an Amazon pull could still fill in.</summary>
/// <param name="Total">All SKUs in the catalog.</param>
/// <param name="WithPicture">SKUs that have a picture.</param>
/// <param name="MissingWithAsin">SKUs without a picture that have an ASIN (an Amazon pull can fill them).</param>
/// <param name="MissingWithoutAsin">SKUs without a picture or an ASIN (only an upload can fill them).</param>
public sealed record PictureCoverage(int Total, int WithPicture, int MissingWithAsin, int MissingWithoutAsin);
