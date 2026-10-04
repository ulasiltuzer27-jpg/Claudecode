namespace PilavciSimulator.Sim.Interaction;

/// <summary>
/// Oyuncunun yapabilecegi her eylem. Istemci bunu ve hedefi host'a yollar;
/// host ayni kurallarla (<see cref="InteractionRules"/>) yeniden dogrular.
/// </summary>
public enum ActionId : byte
{
    None = 0,

    // ── Esya ────────────────────────────────────────────────────────
    PickUp,
    PlaceSocket,
    PlaceSurface,
    Drop,
    Throw,

    // ── Kap / malzeme ──────────────────────────────────────────────
    TakeRice,
    TakeBulgur,
    TakeChickpea,
    TakeCanned,
    TakeBeans,
    TakeButter,
    TakeChicken,
    TakeMeat,
    PourGrain,
    AddButter,
    AddWater,
    AddSalt,
    AddChicken,
    AddMeat,
    ToggleLid,
    EmptyIntoTrash,
    StockBox,

    // ── Ocak ───────────────────────────────────────────────────────
    KnobUp,
    KnobDown,

    // ── Basili tutulan ─────────────────────────────────────────────
    HoldWash,
    HoldFill,
    HoldStir,
    HoldShred,
    HoldWashDishes,

    // ── Tavuk ──────────────────────────────────────────────────────
    MoveChickenToBoard,

    // ── Servis ─────────────────────────────────────────────────────
    TakePlate,
    TakePackage,
    Scoop,
    AddTopping,
    AddPepper,
    AddPickle,
    AddAyran,
    Serve,
    ReturnPlate,
    OpenCashBox,
    RefuseOrder,

    // ── Araba ──────────────────────────────────────────────────────
    PushCart,
    ReleaseCart,
    ToggleCartOpen,
    ToggleHeater,
    OpenPriceBoard,
    LoadCooler,
    LoadPickles,
    LoadPackages,
    LoadPepper,
    LoadGas,

    // ── Diger ──────────────────────────────────────────────────────
    OpenLaptop,
    Sleep,
    ShooCat,
    PetCat,

    // ── Arayuzden (hedefsiz) ───────────────────────────────────────
    GiveChange,
    SetPrice,
    BuySupplies,
    BuyUpgrade,
    SelectCosmetic,
    SleepVote,
    Chat,
}

/// <summary>Bir secenegin hangi tusla tetiklendigi.</summary>
public enum InputSlot : byte
{
    Interact,
    Secondary,
    Use,
    AltUse,
    HoldUse,
    HoldInteract,
}
