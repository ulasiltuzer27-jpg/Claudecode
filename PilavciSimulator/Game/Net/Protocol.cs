namespace PilavciSimulator.Net;

/// <summary>Ag mesaj turleri. Her mesajin ilk bayti.</summary>
public enum Msg : byte
{
    /// <summary>istemci -> host: surum, isim.</summary>
    Hello = 1,
    /// <summary>host -> istemci: oyuncu kimligi + tam dunya.</summary>
    Welcome,
    /// <summary>host -> istemci: reddedildi (surum, dolu).</summary>
    Reject,
    /// <summary>istemci -> host (guvenilmez): konum, bakis, basili eylem, itilen araba.</summary>
    PlayerState,
    /// <summary>istemci -> host (guvenilir): eylem istegi.</summary>
    Action,
    /// <summary>host -> istemci (guvenilir): yeni/degisen varliklar (tam durum).</summary>
    Entities,
    /// <summary>host -> istemci (guvenilmez): konum guncellemeleri.</summary>
    Motions,
    /// <summary>host -> istemci (guvenilir): silinen varliklar.</summary>
    Removed,
    /// <summary>host -> istemci (guvenilir): saat, ekonomi, ilerleme...</summary>
    Globals,
    /// <summary>host -> istemci (guvenilir): geri bildirim olayi.</summary>
    Event,
}

public static class Protocol
{
    /// <summary>Ag uyumlulugu. Mesaj bicimi degisince artirin; farkli surumler baglanamaz.</summary>
    public const int Version = 3;

    /// <summary>Guvenilmez mesajlarin ust siniri (MTU altinda kalsin).</summary>
    public const int MaxUnreliable = 1100;
}
