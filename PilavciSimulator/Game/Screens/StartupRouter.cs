namespace PilavciSimulator.Screens;

/// <summary>
/// Komut satiri kisayollari (--new-game / --host / --join) icin menuyu
/// atlayip dogrudan ilgili ekrani acar. Otomatik dogrulama ve co-op testi
/// bunu kullanir.
/// </summary>
public static class StartupRouter
{
    public static Screen Create(PilavciGame game) => new MainMenuScreen();
}
