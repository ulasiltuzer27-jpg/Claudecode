using Starfall.Gameplay;
using Starfall.World;

namespace Starfall.Core;

/// <summary>Asama B kancalari: Kar Adasi, tekne, tirmanma, kazi, ev. Simdilik bos.</summary>
public sealed partial class Game
{
    private void BuildIsle2Systems() { }
    private void ApplyIsle2Save(SaveData s) { }
    private void UpdateIsle2(float dt) { }
    private void LeaveSpecialStates() => Player.ExitSpecialStates();
    private Interaction? IsleInteraction() => null;
    private void OnRegionDiscovered(RegionDef r) { }
    private void WireIsle2Events() { }
    private void OnCollectIsle2(Item item) { }
    private void GrantRewardIsle2(string id, string kind, System.Numerics.Vector3 pos) { }
}
