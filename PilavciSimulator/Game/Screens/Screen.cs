namespace PilavciSimulator.Screens;

/// <summary>
/// Ekran yiginindaki bir katman: menu, oyun, duraklatma, laptop...
///
/// Ust ust binen ekranlarda yalnizca en ustteki girdi alir
/// (<c>focused</c>). Alttaki oyun ekrani, ustteki ekran
/// <see cref="PausesBelow"/> false ise (laptop, kasa) calismaya devam eder;
/// co-op'ta oyun hic durmaz.
/// </summary>
public abstract class Screen
{
    public PilavciGame Game { get; internal set; } = null!;

    /// <summary>Alttaki ekranin ustune cizilir (alttaki 3B sahne gorunur).</summary>
    public virtual bool IsOverlay => false;

    /// <summary>Alttaki ekranlarin Update'i durur.</summary>
    public virtual bool PausesBelow => true;

    /// <summary>Fare imleci gorunur ve serbest; false ise FPS bakisi icin kilitli.</summary>
    public virtual bool ShowCursor => true;

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void Update(float dt, bool focused)
    {
    }

    /// <summary>3B sahne (yalnizca gorunen en alttaki opak ekran icin cagrilir).</summary>
    public virtual void Draw3D()
    {
    }

    public virtual void DrawUi()
    {
    }

    /// <summary>Otomatik dogrulama notu (<c>annotate</c> komutu).</summary>
    public virtual string Annotate() => GetType().Name;

    /// <summary>Gelistirici komutu (<c>cmd</c>); taninmadiysa false.</summary>
    public virtual bool Command(string[] args) => false;
}

public sealed class ScreenStack
{
    private readonly List<Screen> _stack = new();
    private readonly PilavciGame _game;
    private readonly List<Action> _pending = new();

    public ScreenStack(PilavciGame game) => _game = game;

    public Screen? Top => _stack.Count > 0 ? _stack[^1] : null;
    public IReadOnlyList<Screen> All => _stack;

    public T? Find<T>() where T : Screen => _stack.OfType<T>().LastOrDefault();

    // Degisiklikler kare sonunda uygulanir: Update sirasinda yigini degistirmek
    // dongudeki indeksleri bozardi.
    public void Push(Screen s) => _pending.Add(() =>
    {
        s.Game = _game;
        _stack.Add(s);
        s.Enter();
    });

    public void Pop() => _pending.Add(() =>
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var s = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        s.Exit();
    });

    public void Remove(Screen s) => _pending.Add(() =>
    {
        if (_stack.Remove(s))
        {
            s.Exit();
        }
    });

    /// <summary>Yigini temizleyip tek ekranla baslar.</summary>
    public void ReplaceAll(Screen s) => _pending.Add(() =>
    {
        for (var i = _stack.Count - 1; i >= 0; i--)
        {
            _stack[i].Exit();
        }

        _stack.Clear();
        s.Game = _game;
        _stack.Add(s);
        s.Enter();
    });

    public void ApplyPending()
    {
        // Bir ekranin Enter'i yeni bir Push isteyebilir; sabit noktaya kadar uygula.
        var guard = 0;
        while (_pending.Count > 0 && guard++ < 16)
        {
            var batch = _pending.ToArray();
            _pending.Clear();
            foreach (var a in batch)
            {
                a();
            }
        }
    }

    public void Update(float dt)
    {
        for (var i = _stack.Count - 1; i >= 0; i--)
        {
            var s = _stack[i];
            s.Update(dt, i == _stack.Count - 1);
            if (s.PausesBelow)
            {
                break;
            }
        }
    }

    public void Draw()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var start = _stack.Count - 1;
        while (start > 0 && _stack[start].IsOverlay)
        {
            start--;
        }

        _stack[start].Draw3D();
        for (var i = start; i < _stack.Count; i++)
        {
            _game.Ui.SetInteractive(i == _stack.Count - 1);
            _stack[i].DrawUi();
        }

        _game.Ui.SetInteractive(true);
    }
}
