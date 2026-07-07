using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Displays a sprite on the item that points towards the target component.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(SharedPinpointerSystem))]
public sealed partial class PinpointerComponent : Component
{
    // TODO: Type serializer oh god
    [DataField]
    public string? Component;

    [DataField]
    public float MediumDistance = 16f;

    [DataField]
    public float CloseDistance = 8f;

    [DataField]
    public float ReachedDistance = 1f;

    /// <summary>
    ///     Pinpointer arrow precision in radians.
    /// </summary>
    [DataField]
    public double Precision = 0.09;

    /// <summary>
    ///     Name to display of the target being tracked.
    /// </summary>
    [DataField]
    public string? TargetName;

    /// <summary>
    ///     Whether or not the target name should be updated when the target is updated.
    /// </summary>
    [DataField]
    public bool UpdateTargetName;

    /// <summary>
    ///     Whether or not the target can be reassigned.
    /// </summary>
    [DataField]
    public bool CanRetarget;

    [ViewVariables]
    public EntityUid? Target = null;

    [ViewVariables, AutoNetworkedField]
    public bool IsActive = false;

    [ViewVariables, AutoNetworkedField]
    public Angle ArrowAngle;

    [ViewVariables, AutoNetworkedField]
    public Distance DistanceToTarget = Distance.Unknown;

    [ViewVariables]
    public bool HasTarget => DistanceToTarget != Distance.Unknown;

    // Frontier: Frontier-specific fields
    // If greater than 0, the pinpointer stops pointing to its target when it's further away than this many meters.
    [DataField]
    public float MaxRange = -1;

    // Time in seconds to retarget.
    [DataField]
    public float RetargetDoAfter = 15f;

    // Whether this pinpointer can target mobs.
    [DataField]
    public bool CanTargetMobs = false;

    // Whether this pinpointer's target knows about the pinpointer using the PinpointerTargetComponent.
    [DataField]
    public bool SetsTarget = false;
    // End Frontier: extra pinpointer fields

    // Aurora's Song
    /// <summary>
    /// Whether this pinpointer has been set to its long range configuration and has extended range brackets.
    /// </summary>
    [DataField]
    public bool LongRange = false;
}

[Serializable, NetSerializable]
public enum Distance : byte
{
    Unknown,
    Reached,
    Close,
    Medium,
    Far
}
