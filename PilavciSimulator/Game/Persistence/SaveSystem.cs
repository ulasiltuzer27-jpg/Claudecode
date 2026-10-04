using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.World;

namespace PilavciSimulator.Persistence;

/// <summary>Kayit yuvasinin ozeti (menude listelemek icin, kaydi acmadan).</summary>
public sealed class SaveMeta
{
    public int Slot { get; set; }
    public int Day { get; set; }
    public long Money { get; set; }
    public int Level { get; set; }
    public float Stars { get; set; }
    public string SavedAt { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public int SaveVersion { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Kayitlar: <c>saves/slotN.pilav</c> (ikili dunya goruntusu, bkz.
/// WorldSnapshot) + <c>slotN.json</c> (ozet). Yazim atomik: once .tmp,
/// sonra yerine; onceki kayit .bak olarak kalir. Kalip PixelSurvival'daki
/// Persistence/SaveGame.cs.
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 3;

    public static string DataPath(int slot) => Path.Combine(Paths.Saves, $"slot{slot}.pilav");
    public static string MetaPath(int slot) => Path.Combine(Paths.Saves, $"slot{slot}.json");

    public static SaveMeta? Meta(int slot) =>
        File.Exists(MetaPath(slot)) && File.Exists(DataPath(slot))
            ? JsonUtil.ReadOrDefault<SaveMeta?>(MetaPath(slot), () => null)
            : null;

    public static void Save(int slot, GameWorld w, string playerName)
    {
        Directory.CreateDirectory(Paths.Saves);
        var bytes = WorldSnapshot.Write(w, includePlayers: false, transient: false);
        var path = DataPath(slot);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        File.Move(tmp, path, overwrite: true);
        JsonUtil.WriteAtomic(MetaPath(slot), new SaveMeta
        {
            Slot = slot,
            Day = w.Clock.Day,
            Money = w.Economy.Money,
            Level = w.Level,
            Stars = w.Reputation.Stars,
            SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            GameVersion = typeof(SaveSystem).Assembly.GetName().Version?.ToString() ?? "",
            SaveVersion = WorldSnapshot.Version,
            Name = playerName,
        });
        Log.Info($"kaydedildi: yuva {slot}, gun {w.Clock.Day}, {bytes.Length} bayt");
    }

    /// <summary>Yuvayi yukler; bozuksa .bak denenir.</summary>
    public static GameWorld Load(int slot, GameData data, DistrictLayout layout)
    {
        var path = DataPath(slot);
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var w = new GameWorld(data, layout, isHost: true, Environment.TickCount);
                WorldSnapshot.Read(w, File.ReadAllBytes(candidate));
                Sanitize(w);
                if (candidate != path)
                {
                    Log.Warn($"kayit bozuktu, yedekten yuklendi: {candidate}");
                }

                return w;
            }
            catch (Exception ex)
            {
                Log.Error($"kayit okunamadi ({candidate})", ex);
            }
        }

        throw new InvalidDataException("kayit okunamadi");
    }

    public static void Delete(int slot)
    {
        foreach (var p in new[] { DataPath(slot), DataPath(slot) + ".bak", MetaPath(slot), MetaPath(slot) + ".bak" })
        {
            if (File.Exists(p))
            {
                File.Delete(p);
            }
        }
    }

    /// <summary>Kayitta oyuncu yok: ellerdeki esyalar depoya, itilen araba serbest.</summary>
    public static void Sanitize(GameWorld w)
    {
        var drop = new Vector3(-104.6f, 1.06f, -16.9f);
        var k = 0;
        foreach (var it in w.Items)
        {
            var orphan = it.Attach == Attach.Held || (it.Attach == Attach.Socket && w.Get<StationEntity>(it.ParentId) is null);
            if (orphan)
            {
                it.Attach = Attach.Free;
                it.ParentId = 0;
                it.Position = drop + new Vector3(0.3f * (k % 6), 0, 0.25f * (k / 6));
                it.Resting = true;
                k++;
            }
        }

        foreach (var c in w.Carts)
        {
            c.Cart!.PusherId = 0;
        }

        w.DayOver = false;
        w.RefreshDynamicColliders();
    }
}
