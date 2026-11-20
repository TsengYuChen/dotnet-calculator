using System.Text;

namespace VehicleGo.Domain.Vehicles;

public static class PlateNormalizer
{
    /// <summary>NFKC (full-width → half-width), upper-case, remove whitespace and hyphens.</summary>
    public static string Normalize(string plate)
    {
        ArgumentNullException.ThrowIfNull(plate);

        var nfkc = plate.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(nfkc.Length);
        foreach (var c in nfkc)
        {
            if (char.IsWhiteSpace(c) || c == '-')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
