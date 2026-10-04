using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim;

/// <summary>Sokak kedisi: kovala ya da sev.</summary>
public static class AnimalLogic
{
    public const int PetsForMascot = 10;

    public static bool Shoo(GameWorld w, PlayerEntity p, AnimalEntity cat)
    {
        if (cat.Mascot)
        {
            w.Toast("toast.mascot_stays", "", 0, p.Id);
            return true;
        }

        cat.State = AnimalState.Flee;
        cat.Timer = 0;
        cat.MarkState();
        w.Sound("meow", cat.Position);
        return true;
    }

    public static bool Pet(GameWorld w, PlayerEntity p, AnimalEntity cat)
    {
        cat.State = AnimalState.Purr;
        cat.Timer = 0;
        cat.MarkState();
        w.Sound("purr", cat.Position);
        w.Progress.AddStat("cat_pets");
        w.Events.CatPetsToday++;
        if (!w.Progress.CatMascot && w.Progress.Stat("cat_pets") >= PetsForMascot)
        {
            w.Progress.CatMascot = true;
            cat.Mascot = true;
            w.Toast("toast.mascot", "", 3);
        }

        w.GlobalsDirty = true;
        Progression.CheckAchievements(w);
        return true;
    }
}
