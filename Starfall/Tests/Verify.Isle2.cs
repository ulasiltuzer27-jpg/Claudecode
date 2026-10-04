namespace Starfall.Tests;

/// <summary>Asama B denetimleri (Kar Adasi, tekne, tirmanma, kazi, ev).</summary>
public sealed partial class Verify
{
    private partial IEnumerable<string> ExtraKeys() => Array.Empty<string>();
    private partial float ExtraSpotDy(string id) => 0;
    private partial int ExtraQuestCount() => 0;
}
