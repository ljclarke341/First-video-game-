using GarageTycoon.Core.Cars;

namespace GarageTycoon.Core.Parts
{
    /// <summary>
    /// The kinds of part the garage keeps on the shelf.
    ///
    /// Deliberately few. One kind per repair that actually fits something, and none at all for a
    /// diagnostic scan - a parts list long enough to need searching would turn the game into
    /// stock-keeping, which is exactly what it must not become.
    /// </summary>
    public enum PartKind
    {
        None = 0,
        BrakePads = 1,
        Tyres = 2,
        EngineParts = 3,
        Electrical = 4,
        SuspensionParts = 5,
        ExhaustParts = 6,
        BodyPanel = 7,
        Paint = 8
    }

    public static class PartKinds
    {
        /// <summary>How many real kinds there are, not counting None.</summary>
        public const int Count = 8;

        public static string DisplayName(this PartKind kind)
        {
            switch (kind)
            {
                case PartKind.BrakePads: return "Brake Pads";
                case PartKind.Tyres: return "Tyres";
                case PartKind.EngineParts: return "Engine Parts";
                case PartKind.Electrical: return "Electrical";
                case PartKind.SuspensionParts: return "Suspension";
                case PartKind.ExhaustParts: return "Exhaust";
                case PartKind.BodyPanel: return "Body Panel";
                case PartKind.Paint: return "Paint";
                default: return "None";
            }
        }

        /// <summary>
        /// Which part a repair fits. Diagnostics fits nothing - it is a scan, and charging the
        /// player for a part they did not use would be the first thing to feel unfair.
        /// </summary>
        public static PartKind For(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Brakes: return PartKind.BrakePads;
                case JobType.Tires: return PartKind.Tyres;
                case JobType.Engine: return PartKind.EngineParts;
                case JobType.Electrics: return PartKind.Electrical;
                case JobType.Suspension: return PartKind.SuspensionParts;
                case JobType.Exhaust: return PartKind.ExhaustParts;
                case JobType.Panels: return PartKind.BodyPanel;
                case JobType.Paint: return PartKind.Paint;
                default: return PartKind.None;
            }
        }

        /// <summary>True when this repair needs something off the shelf.</summary>
        public static bool NeedsPart(this JobType jobType)
        {
            return For(jobType) != PartKind.None;
        }
    }
}
