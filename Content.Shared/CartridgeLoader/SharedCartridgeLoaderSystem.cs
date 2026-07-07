using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Shared.CartridgeLoader;

public abstract partial class SharedCartridgeLoaderSystem : EntitySystem
{
    public const string InstalledContainerId = "program-container";

    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartridgeLoaderComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CartridgeLoaderComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<CartridgeLoaderComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<CartridgeLoaderComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnComponentInit(EntityUid uid, CartridgeLoaderComponent loader, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, CartridgeLoaderComponent.CartridgeSlotId, loader.CartridgeSlot);
    }

    /// <summary>
    /// Marks installed program entities for deletion when the component gets removed
    /// </summary>
    private void OnComponentRemove(EntityUid uid, CartridgeLoaderComponent loader, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, loader.CartridgeSlot);
        if (_container.TryGetContainer(uid, InstalledContainerId, out var cont))
            _container.ShutdownContainer(cont);
    }

    protected virtual void OnItemInserted(EntityUid uid, CartridgeLoaderComponent loader, EntInsertedIntoContainerMessage args)
    {
        UpdateAppearanceData(uid, loader);
    }

    protected virtual void OnItemRemoved(EntityUid uid, CartridgeLoaderComponent loader, EntRemovedFromContainerMessage args)
    {
        UpdateAppearanceData(uid, loader);
    }

    private void UpdateAppearanceData(EntityUid uid, CartridgeLoaderComponent loader)
    {
        _appearanceSystem.SetData(uid, CartridgeLoaderVisuals.CartridgeInserted, loader.CartridgeSlot.HasItem);
    }
}

/// <summary>
/// Gets sent to program / cartridge entities when they get inserted or installed
/// </summary>
public sealed class CartridgeAddedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeAddedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

/// <summary>
/// Gets sent to cartridge entities when they get ejected
/// </summary>
public sealed class CartridgeRemovedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeRemovedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

//Aurora's Song
/// <summary>
/// Gets raised on the active program when an item get moved from the cartridge loader.
/// </summary>
/// <param name="loader">The loader an item was inserted or removed from.</param>
/// <param name="item">The item that was moved.</param>
/// <param name="container">The container that the item was inserted/removed from.</param>
/// <param name="inserted">Denotes whether the item is being inserted or removed from the container.</param>

public sealed class LoaderContentsChangedEvent(Entity<CartridgeLoaderComponent> loader, EntityUid item, BaseContainer container, bool inserted) : EntityEventArgs
{
    public readonly EntityUid Loader = loader;
    public readonly EntityUid Item = item;
    public readonly BaseContainer Container = container;
    public readonly bool Inserted = inserted;
}

/// <summary>
/// Gets sent to program / cartridge entities when they get activated
/// </summary>
/// <remarks>
/// Don't update the programs ui state in this events listener
/// </remarks>
public sealed class CartridgeActivatedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeActivatedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

/// <summary>
/// Gets sent to program / cartridge entities when they get deactivated
/// </summary>
public sealed class CartridgeDeactivatedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeDeactivatedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

/// <summary>
/// Gets sent to program / cartridge entities when the ui is ready to be updated by the cartridge.
/// </summary>
/// <remarks>
/// This is used for the initial ui state update because updating the ui in the activate event doesn't work
/// </remarks>
public sealed class CartridgeUiReadyEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeUiReadyEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

/// <summary>
/// Gets sent by the cartridge loader system to the cartridge loader entity so another system
/// can handle displaying the notification
/// </summary>
/// <param name="Message">The message to be displayed</param>
[ByRefEvent]
public record struct CartridgeLoaderNotificationSentEvent(string Header, string Message);
