using System.Numerics;
using Starfall.Core;

namespace Starfall.Gameplay;

public sealed record Choice(string Label, Action? Action = null);

/// <summary>
/// Diyalog mantigi: daktilo efekti + konusmaci "mirildanmasi" + secenekler. Cizimi
/// UI/DialogueView yapar; burada yalnizca durum var (testler pencere olmadan konusabilsin).
/// </summary>
public sealed class Dialogue
{
    private readonly Game _g;
    public bool Active;
    public Npc? Npc;
    public string Name = "";
    public string[] Lines = Array.Empty<string>();
    public List<Choice>? Choices;
    private Action? _onEnd;
    public int LineIdx;
    public string Full = "";
    public float Shown;
    public bool Typing;
    public int ChoiceIdx;
    public bool ShowingChoices;

    public Dialogue(Game g) => _g = g;

    public void Start(Npc? npc = null, string? name = null, string[]? lines = null, string? line = null, List<Choice>? choices = null, Action? onEnd = null)
    {
        if (Active) Close(false);
        Npc = npc;
        Lines = lines ?? new[] { line ?? "" };
        Choices = choices;
        _onEnd = onEnd;
        LineIdx = 0;
        Active = true;
        ShowingChoices = false;
        if (npc != null) npc.Talking = true;
        _g.Player.Model.LookTarget = npc != null ? npc.Pos + new Vector3(0, 0.8f, 0) : null;
        Name = name ?? (npc != null ? Loc.T($"npc.{npc.Id}") : "");
        _g.SetState(GameState.Dialogue);
        ShowLine();
    }

    private void ShowLine()
    {
        Full = LineIdx < Lines.Length ? Lines[LineIdx] : "";
        Shown = 0;
        Typing = true;
    }

    public string Visible => Typing ? Full[..Math.Min(Full.Length, (int)Shown)] : Full;

    public bool CanAdvance => !Typing && !ShowingChoices;

    public void Update(float dt)
    {
        if (!Active || !Typing) return;
        int before = (int)Shown;
        Shown += dt * 48;
        int now = Math.Min(Full.Length, (int)Shown);
        if (now > before)
        {
            char ch = Full[now - 1];
            if (char.IsLetterOrDigit(ch)) _g.Audio.VoiceBlip(Npc?.Voice ?? 1, ch);
        }
        if (now >= Full.Length) FinishTyping();
    }

    private void FinishTyping()
    {
        Typing = false;
        bool last = LineIdx >= Lines.Length - 1;
        if (last && Choices is { Count: > 0 })
        {
            ShowingChoices = true;
            ChoiceIdx = 0;
        }
    }

    public void FocusChoice(int i)
    {
        if (Choices == null || Choices.Count == 0) return;
        ChoiceIdx = ((i % Choices.Count) + Choices.Count) % Choices.Count;
    }

    public void Pick(int i)
    {
        if (Choices == null || i < 0 || i >= Choices.Count) return;
        var c = Choices[i];
        _g.Audio.Sfx("uiConfirm");
        Close(false);
        c.Action?.Invoke();
        if (!Active && _g.State == GameState.Dialogue) _g.SetState(GameState.Playing);
    }

    public void Advance()
    {
        if (!Active) return;
        if (Typing) { FinishTyping(); return; }
        if (ShowingChoices) return;
        if (LineIdx < Lines.Length - 1)
        {
            LineIdx++;
            ShowLine();
            return;
        }
        Close(true);
    }

    public void Handle(Input input)
    {
        if (!Active) return;
        if (ShowingChoices && !Typing)
        {
            if (input.Pressed("up")) { FocusChoice(ChoiceIdx - 1); _g.Audio.Sfx("uiMove"); }
            if (input.Pressed("down")) { FocusChoice(ChoiceIdx + 1); _g.Audio.Sfx("uiMove"); }
            if (input.Pressed("confirm") || input.Pressed("interact")) Pick(ChoiceIdx);
            else if (input.Pressed("back")) Pick(Choices!.Count - 1);
            return;
        }
        if (input.Pressed("confirm") || input.Pressed("interact") || input.Pressed("jump")) Advance();
    }

    public void Close(bool runEnd)
    {
        Active = false;
        ShowingChoices = false;
        if (Npc != null) Npc.Talking = false;
        _g.Player.Model.LookTarget = null;
        var cb = _onEnd;
        _onEnd = null;
        if (_g.State == GameState.Dialogue) _g.SetState(GameState.Playing);
        if (runEnd) cb?.Invoke();
    }
}
