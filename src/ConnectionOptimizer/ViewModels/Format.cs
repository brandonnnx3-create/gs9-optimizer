using System.Globalization;

namespace ConnectionOptimizer.ViewModels;

internal static class Format
{
    public static string Duration(TimeSpan duration) =>
        duration.TotalSeconds < 1 ? "<1s"
        : duration.TotalSeconds < 60 ? $"{(int)Math.Round(duration.TotalSeconds)}s"
        : $"{(int)duration.TotalMinutes}m {duration.Seconds:00}s";

    public static string Elapsed(TimeSpan elapsed) => $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

    public static string Clock(DateTime time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static (string Value, string Unit) Speed(long bitsPerSecond) => bitsPerSecond switch
    {
        <= 0 => ("—", string.Empty),
        >= 1_000_000_000 => ((bitsPerSecond / 1e9).ToString("0.#", CultureInfo.InvariantCulture), "GBPS"),
        >= 1_000_000 => ((bitsPerSecond / 1e6).ToString("0.#", CultureInfo.InvariantCulture), "MBPS"),
        _ => ((bitsPerSecond / 1e3).ToString("0.#", CultureInfo.InvariantCulture), "KBPS"),
    };

    public static string SpeedText(long bitsPerSecond)
    {
        (string value, string unit) = Speed(bitsPerSecond);
        return unit.Length == 0 ? "speed unknown" : $"{value} {unit}";
    }
}
