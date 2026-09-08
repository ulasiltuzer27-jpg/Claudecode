using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Inventory;

namespace PixelSurvival.Systems.Crafting;

public sealed class RecipeIngredient
{
    [JsonPropertyName("item")] public string Item { get; init; } = "";
    [JsonPropertyName("amount")] public int Amount { get; init; } = 1;
}

public sealed class Recipe
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("output")] public RecipeIngredient Output { get; init; } = new();
    [JsonPropertyName("inputs")] public List<RecipeIngredient> Inputs { get; init; } = [];
}

/// <summary>
/// <c>Content/Items/recipes.json</c> dosyasının kod karşılığı.
/// Tarifler koda gömülü değil; denge ayarı yeniden derleme gerektirmez.
/// </summary>
public sealed class RecipeBook
{
    [JsonPropertyName("recipes")] public List<Recipe> Recipes { get; init; } = [];

    public static RecipeBook Load(ContentManager content, string assetName, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var book = JsonSerializer.Deserialize<RecipeBook>(stream, JsonOptions)
                   ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        var seen = new HashSet<string>();

        foreach (var recipe in book.Recipes)
        {
            if (!seen.Add(recipe.Id))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{recipe.Id}' tarifi iki kez tanımlanmış.");
            }

            Validate(items, relativePath, recipe.Id, recipe.Output);

            if (recipe.Inputs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{recipe.Id}' girdisiz — bedava item üretirdi.");
            }

            foreach (var input in recipe.Inputs)
            {
                Validate(items, relativePath, recipe.Id, input);

                // Çıktısı girdisiyle aynı olan tarif net kazanç üretir:
                // 1 odun -> 2 odun sonsuz kaynak demektir.
                if (input.Item == recipe.Output.Item)
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{recipe.Id}' hem girdi hem çıktı olarak " +
                        $"'{input.Item}' kullanıyor — sonsuz kaynak istismarına açık.");
                }
            }
        }

        return book;
    }

    private static void Validate(ItemDatabase items, string path, string recipeId,
                                 RecipeIngredient ingredient)
    {
        if (!items.Contains(ingredient.Item))
        {
            throw new InvalidOperationException(
                $"'{path}': '{recipeId}' tarifi tanımsız '{ingredient.Item}' item'ını kullanıyor.");
        }

        if (ingredient.Amount < 1)
        {
            throw new InvalidOperationException(
                $"'{path}': '{recipeId}' tarifinde '{ingredient.Item}' miktarı en az 1 olmalı.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Bir crafting denemesinin sonucu.</summary>
public enum CraftOutcome
{
    Success,
    MissingIngredients,
    NoRoomForOutput
}

/// <summary>
/// AŞAMA 1 / MADDE 8 — basit crafting.
///
/// Üretim ANINDA gerçekleşir; süre/ilerleme çubuğu yok. Tuşa bir basış =
/// bir üretim.
///
/// KAPSAM DIŞI: crafting istasyonu gereksinimi, üretim süresi, alet etkileri
/// (Taş Balta şu an sadece bir item, toplamayı hızlandırmıyor), yerleştirme
/// (Kamp Ateşi madde 9'da yerleştirilebilir olacak).
/// </summary>
public sealed class CraftingSystem(RecipeBook book, ItemDatabase items)
{
    public IReadOnlyList<Recipe> Recipes => book.Recipes;

    /// <summary>Tarifin girdileri envanterde var mı (çıktıya yer olup olmadığına bakmaz).</summary>
    public bool HasIngredients(Recipe recipe, WorldInventory inventory) =>
        recipe.Inputs.All(input => inventory.Has(input.Item, input.Amount));

    /// <summary>
    /// Tarifi uygular.
    ///
    /// İşlem BÜTÜNSELDİR: girdiler çıkarıldıktan sonra çıktı envantere
    /// sığmazsa her şey geri alınır. Aksi halde dolu envanterde üretim
    /// yapan oyuncu girdilerini kaybeder, karşılığında hiçbir şey almazdı.
    ///
    /// Girdileri önce çıkarmak gerekiyor çünkü çıkarma slot boşaltabilir ve
    /// çıktı ancak o boşluğa sığabilir. "Önce yer var mı" kontrolü bu
    /// durumda yanlış negatif verirdi.
    /// </summary>
    public CraftOutcome TryCraft(Recipe recipe, WorldInventory inventory)
    {
        if (!HasIngredients(recipe, inventory))
        {
            return CraftOutcome.MissingIngredients;
        }

        var snapshot = inventory.Snapshot();

        foreach (var input in recipe.Inputs)
        {
            if (inventory.TryRemove(input.Item, input.Amount))
            {
                continue;
            }

            // HasIngredients geçtiği için buraya düşülmemeli; yine de
            // sessizce item yutmaktansa geri al.
            inventory.Restore(snapshot);
            return CraftOutcome.MissingIngredients;
        }

        var leftover = inventory.TryAdd(recipe.Output.Item, recipe.Output.Amount);
        if (leftover > 0)
        {
            inventory.Restore(snapshot);
            return CraftOutcome.NoRoomForOutput;
        }

        return CraftOutcome.Success;
    }

    /// <summary>Tarifi insan okunur şekilde özetler (HUD için).</summary>
    public string Describe(Recipe recipe)
    {
        var inputs = string.Join(" + ", recipe.Inputs.Select(
            i => $"{i.Amount} {items.Get(i.Item).Name}"));

        return $"{inputs} > {recipe.Output.Amount} {items.Get(recipe.Output.Item).Name}";
    }
}
