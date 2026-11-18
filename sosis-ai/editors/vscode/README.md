# Sosis AI — VS Code eklentisi

Kendi bilgisayarınızda çalışan Sosy modelleriyle:
- **Satır içi öneriler** (gri metin, `Tab` ile kabul) — imlecin önü ve arkası (FIM) kullanılır
- **Seçili kodu açıkla / düzelt** (sağ tık menüsü)
- Durum çubuğunda model durumu; tıklayınca öneriler açılır/kapanır

Harici API yok: eklenti yalnızca yerel `sosis serve` sunucusuyla konuşur.

## Kurulum

```bash
# 1) Sunucu (ayrı bir terminalde açık kalsın)
sosis serve -r model.sosy --draft flash.sosy      # --draft isteğe bağlı (daha hızlı)

# 2) Eklentiyi derleyip paketleyin
cd editors/vscode
npm install
npx @vscode/vsce package --no-dependencies        # sosis-ai-1.2.0.vsix
code --install-extension sosis-ai-1.2.0.vsix
```

Ayarlar (`sosis.*`): sunucu adresi, en çok token, sıcaklık, bekleme süresi, bağlam satırı.
Hızlı öneriler için küçük model (Sosy-Flash) ya da spekülatif kod çözme önerilir.
