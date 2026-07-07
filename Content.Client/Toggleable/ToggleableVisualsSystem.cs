using System.Linq;
using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Toggleable;

/// <summary>
/// Implements the behavior of <see cref="ToggleableVisualsComponent"/> by reacting to
/// <see cref="AppearanceChangeEvent"/>, for the sprite directly; <see cref="OnGetHeldVisuals"/> for the
/// in-hand visuals; and <see cref="OnGetEquipmentVisuals"/> for the clothing visuals.
/// </summary>
/// <see cref="ToggleableVisualsComponent"/>
public sealed partial class ToggleableVisualsSystem : VisualizerSystem<ToggleableVisualsComponent>
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleableVisualsComponent, GetInhandVisualsEvent>(OnGetHeldVisuals,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<ToggleableVisualsComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid,
        ToggleableVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, args.Component))
            return;

        var modulateColor =
            AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, args.Component);

        // Update the item's sprite
        if (args.Sprite != null && component.SpriteLayer != null &&
            SpriteSystem.LayerMapTryGet((uid, args.Sprite), component.SpriteLayer, out var layer, false))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, enabled);
            if (modulateColor)
                SpriteSystem.LayerSetColor((uid, args.Sprite), component.SpriteLayer, color);

            // TheDen - If replace mode is on and there are any layers on the sprite, set the base layer to invisible
            if (component.ReplaceMode && args.Sprite.AllLayers.Any())
            {
                SpriteSystem.LayerSetVisible((uid, args.Sprite), 0, !enabled);
            }
            // TheDen - end insert
        }

        // If there's a `ItemTogglePointLightComponent` that says to apply the color to attached lights, do so.
        if (TryComp<ItemTogglePointLightComponent>(uid, out var toggleLights) &&
            TryComp(uid, out PointLightComponent? light))
        {
            DebugTools.Assert(!light.NetSyncEnabled,
                $"{typeof(ItemTogglePointLightComponent)} requires point lights without net-sync");
            _pointLight.SetEnabled(uid, enabled, light);
            if (modulateColor && toggleLights.ToggleableVisualsColorModulatesLights)
            {
                _pointLight.SetColor(uid, color, light);
            }
        }

        // update clothing & in-hand visuals.
        _item.VisualsChanged(uid);
    }

    private void OnGetEquipmentVisuals(EntityUid uid,
        ToggleableVisualsComponent component,
        GetEquipmentVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance)
            || !AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, appearance)
            || !enabled)
            return;

        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;
        List<PrototypeLayerData>? layers = null;

        // attempt to get species specific data
        if (inventory.SpeciesId != null)
            component.ClothingVisuals.TryGetValue($"{args.Slot}-{inventory.SpeciesId}", out layers);

        // No species specific data.  Try to default to generic data.
        if (layers == null && !component.ClothingVisuals.TryGetValue(args.Slot, out layers))
            return;

        // TheDen - iterates decrementally through the sprite layers on the entity, searchign for any that match a base key and don't have "-toggle" in the name, then removing that layer if so.
        if (component.ReplaceMode)
        {
            for (var layerIdx = args.Layers.Count - 1; layerIdx >= 0; layerIdx--)
            {
                var (layerKey, _) = args.Layers[layerIdx];
                if (layerKey.StartsWith($"{args.Slot}-") && !layerKey.Contains("-toggle"))
                {
                    args.Layers.RemoveAt(layerIdx);
                }
            }
        }
        // TheDen - end insert

        var modulateColor = AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, appearance);

        var i = 0;
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? $"{args.Slot}-toggle" : $"{args.Slot}-toggle-{i}";
                i++;
            }

            if (modulateColor)
                layer.Color = color;

            args.Layers.Add((key, layer));
        }
    }

    private void OnGetHeldVisuals(EntityUid uid, ToggleableVisualsComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance)
            || !AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, appearance)
            || !enabled)
            return;

        if (!component.InhandVisuals.TryGetValue(args.Location, out var layers))
            return;

        // TheDen - if replace mode is on, replace the basekey layer with the new toggle layer sprite instead of just rendering it in addition to the base sprite state.
        if (component.ReplaceMode)
        {
            var baseKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}";
            for (var j = args.Layers.Count - 1; j >= 0; j--)
            {
                var (layerKey, _) = args.Layers[j];
                if (layerKey.StartsWith(baseKey) && !layerKey.Contains("-toggle"))
                {
                    args.Layers.RemoveAt(j);
                }
            }
        }
        // TheDen - end insert

        var modulateColor = AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, appearance);

        var i = 0;
        var defaultKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}-toggle";
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? defaultKey : $"{defaultKey}-{i}";
                i++;
            }

            if (modulateColor)
                layer.Color = color;

            args.Layers.Add((key, layer));
        }
    }
}
