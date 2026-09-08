using Microsoft.Xna.Framework;

namespace PixelSurvival.Systems.Animation;

/// <summary>
/// Bir <see cref="SpriteSheet"/> üzerinde tek bir animasyon state'ini oynatır.
///
/// Zamanlama delta-time tabanlı: 30 FPS'te de 144 FPS'te de animasyon aynı
/// hızda akar. Frame sayacı DEĞİL, geçen süre kullanılır.
/// </summary>
public sealed class SpriteAnimator
{
    private readonly SpriteSheet _sheet;
    private AnimationClip _clip;
    private float _elapsed;
    private int _frameIndex;

    public SpriteAnimator(SpriteSheet sheet, string initialState)
    {
        _sheet = sheet;
        _clip = sheet.GetClip(initialState);
    }

    /// <summary>Şu an oynayan state'in adı.</summary>
    public string CurrentState => _clip.State;

    /// <summary>Döngüsüz bir animasyon son frame'ine ulaştı mı.</summary>
    public bool IsFinished => !_clip.Loop && _frameIndex >= _clip.Frames - 1;

    /// <summary>
    /// State'i değiştirir. Zaten aynı state oynuyorsa HİÇBİR ŞEY yapmaz —
    /// aksi halde her Update'te sıfırlanır ve animasyon ilk frame'de donar.
    /// </summary>
    public void Play(string state)
    {
        if (_clip.State == state)
        {
            return;
        }

        _clip = _sheet.GetClip(state);
        _elapsed = 0f;
        _frameIndex = 0;
    }

    public void Update(GameTime gameTime)
    {
        if (_clip.Frames <= 1 || _clip.Fps <= 0f)
        {
            return; // tek frame'lik veya durdurulmuş animasyon
        }

        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

        var frameDuration = 1f / _clip.Fps;

        // while: bir kare çok uzun sürdüyse (debugger'da durdurma, ağır yükleme)
        // animasyon geride kalmasın, atlanan frame'ler telafi edilsin.
        while (_elapsed >= frameDuration)
        {
            _elapsed -= frameDuration;

            if (_frameIndex + 1 < _clip.Frames)
            {
                _frameIndex++;
            }
            else if (_clip.Loop)
            {
                _frameIndex = 0;
            }
            else
            {
                _elapsed = 0f; // son frame'de kilitlen
                break;
            }
        }
    }

    /// <summary>Şu anki frame'in sheet üzerindeki kaynak dikdörtgeni.</summary>
    public Rectangle CurrentSourceRectangle => _sheet.GetSourceRectangle(_clip, _frameIndex);
}
