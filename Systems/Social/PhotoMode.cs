using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.Systems.Social;

/// <summary>
/// AŞAMA 2 / MADDE 24 — photo mode.
///
/// ── Ne yapar ────────────────────────────────────────────────────────────
/// Arayüzü gizler, kamerayı karakterden AYIRIR (serbest gezdirme),
/// yakınlaştırmayı serbest bırakır ve kareyi PNG olarak diske yazar.
///
/// ── Oyun DURMAZ, ama oyuncu da hareket etmez ────────────────────────────
/// Photo mode'da yön tuşları KAMERAYI sürer, karakteri değil. Bunlar aynı
/// tuşlar; ikisini birden sürselerdi oyuncu fotoğrafını çekmeye çalışırken
/// karakteri kadrajdan çıkardı.
///
/// Dünya akmaya devam eder (hava, gündüz-gece, yaratıklar): fotoğrafın
/// amacı hareketli bir anı yakalamak. Donmuş bir dünyada photo mode'un
/// tek faydası kadraj olurdu.
///
/// ── Ekran görüntüsü nereye ──────────────────────────────────────────────
/// Çalışma dizininde <c>photos/</c>. Steam'in kendi F12'si Steam
/// derlemesinde ayrıca çalışır; bu, ondan bağımsız ve HUD'suz temiz kare
/// veren yol.
/// </summary>
public sealed class PhotoMode
{
    /// <summary>Serbest kameranın saniyedeki hızı (dünya pixel'i).</summary>
    private const float PanSpeed = 220f;

    /// <summary>Saniyedeki yakınlaştırma değişimi.</summary>
    private const float ZoomSpeed = 2.2f;

    /// <summary>Kaydedilen karelerin klasörü.</summary>
    public const string OutputDirectory = "photos";

    /// <summary>Kameranın oyuncudan sapması. Çıkışta sıfırlanır.</summary>
    public Vector2 Offset { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Son kaydedilen dosyanın yolu — arayüzde gösterilir.</summary>
    public string LastSavedPath { get; private set; } = "";

    /// <summary>
    /// Bu karede ekran görüntüsü isteniyor mu.
    ///
    /// Çizim tarafı bunu okuyup bilgi şeridini ATLAR: şerit çizilseydi
    /// kaydedilen PNG'de görünürdü ve photo mode'un "temiz kare" amacı
    /// kalmazdı.
    /// </summary>
    public bool ShotPending { get; private set; }

    /// <summary>Photo mode'a girer; kamera o anki konumdan devralır.</summary>
    public void Enter()
    {
        IsActive = true;
        Offset = Vector2.Zero;
    }

    /// <summary>Çıkar ve kamerayı varsayılana döndürür.</summary>
    public void Exit(Camera2D camera)
    {
        IsActive = false;
        Offset = Vector2.Zero;
        camera.Zoom = camera.DefaultZoom;
    }

    public void Toggle(Camera2D camera)
    {
        if (IsActive) Exit(camera);
        else Enter();
    }

    /// <summary>
    /// Serbest kamerayı sürer.
    /// </summary>
    /// <param name="pan">-1..1 aralığında yön (aynı hareket tuşları).</param>
    /// <param name="zoomDelta">-1 uzaklaş, +1 yakınlaş.</param>
    public void Update(float delta, Vector2 pan, float zoomDelta, Camera2D camera)
    {
        if (!IsActive) return;

        // Yakinlastikca panning YAVASLAR: ekranda gorunen alan kuculuyor,
        // ayni dunya hizi yakinken cok hizli hissettiriyor.
        Offset += pan * (PanSpeed / camera.Zoom) * delta;

        if (zoomDelta != 0f)
        {
            camera.Zoom += zoomDelta * ZoomSpeed * delta * camera.Zoom;
        }
    }

    /// <summary>Bir sonraki çizimin sonunda kare kaydedilsin.</summary>
    public void RequestShot() => ShotPending = true;

    /// <summary>
    /// İstenmişse back buffer'ı PNG'ye yazar. <c>Draw</c>'un EN SONUNDA
    /// çağrılmalı — arayüz gizlendikten sonra.
    /// </summary>
    public void CaptureIfRequested(GraphicsDevice device)
    {
        if (!ShotPending) return;
        ShotPending = false;

        Directory.CreateDirectory(OutputDirectory);

        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;

        var pixels = new Color[width * height];
        device.GetBackBufferData(pixels);

        using var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        // Dosya adi zaman damgali: ayni saniyede iki kare cekilse bile
        // ustune yazilmasin diye milisaniye de var.
        var name = $"pixelsurvival_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var path = Path.Combine(OutputDirectory, name);

        using (var stream = File.Create(path))
        {
            texture.SaveAsPng(stream, width, height);
        }

        LastSavedPath = path;
        Console.WriteLine($"[photo] kaydedildi: {path}");
    }
}
