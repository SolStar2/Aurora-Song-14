using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Nyanotrasen.Kitchen
{
    [Serializable, NetSerializable]
    public sealed partial class ClearSlagDoAfterEvent : DoAfterEvent
    {
        [DataField("solution", required: true)]
        public Solution Solution = default!;

        [DataField(required: true)] // Aurora's Song - Pass name too
        public string SolutionName = default!;

        [DataField("amount", required: true)]
        public FixedPoint2 Amount;

        private ClearSlagDoAfterEvent()
        {
        }

        public ClearSlagDoAfterEvent(Solution solution, string solutionName, FixedPoint2 amount) // Aurora's Song - Pass name too
        {
            Solution = solution;
            SolutionName = solutionName; // Aurora's Song - Pass name too
            Amount = amount;
        }

        public override DoAfterEvent Clone() => this;
    }
}
