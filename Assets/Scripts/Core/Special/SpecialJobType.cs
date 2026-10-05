namespace GarageTycoon.Core.Special
{
    /// <summary>
    /// The out-of-the-ordinary cars.
    ///
    /// A special job is NOT a separate kind of repair. It is the same car, through the same
    /// diagnosis, quote, parts, mini-games and quality, with some of those dials turned - which is
    /// the whole design rule: if a special job were a payout multiplier bolted to a normal car, it
    /// would be a bigger number rather than a different decision.
    /// </summary>
    public enum SpecialJobType
    {
        /// <summary>An ordinary customer. Most cars.</summary>
        None = 0,

        /// <summary>Needs it back today. Short fuse, pays well, and the clock is the whole problem.</summary>
        Urgent = 1,

        /// <summary>Wants it done properly, and knows the difference.</summary>
        Performance = 2,

        /// <summary>An old car with a lot wrong, and a lot to find.</summary>
        Restoration = 3,

        /// <summary>A company car. One of several, thin margins, steady work.</summary>
        Fleet = 4,

        /// <summary>Something valuable, and someone watching.</summary>
        Vip = 5
    }
}
