namespace GarageTycoon.Core.Cars
{
    /// <summary>Where a car is in its journey through the garage.</summary>
    public enum CarState
    {
        /// <summary>Parked outside, waiting for a free bay. The patience clock is already ticking.</summary>
        Waiting = 0,

        /// <summary>In a bay with work underway.</summary>
        InBay = 1,

        /// <summary>All jobs done - the customer paid and drove off happy.</summary>
        Completed = 2,

        /// <summary>Patience ran out. The customer left and the payout is gone.</summary>
        LeftAngry = 3
    }
}
