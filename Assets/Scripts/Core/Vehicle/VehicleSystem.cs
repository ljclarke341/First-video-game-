namespace GarageTycoon.Core.Vehicle
{
    /// <summary>
    /// The major systems of a car, as a workshop would list them on an inspection sheet.
    ///
    /// These are deliberately coarser than <see cref="Cars.JobType"/>: a customer thinks "my brakes
    /// are bad", not "I need a brake service AND a caliper bolt". Several job types therefore roll
    /// up into one system - a paint touch-up and a panel beat are both Body.
    /// </summary>
    public enum VehicleSystem
    {
        Engine = 0,
        Brakes = 1,
        Suspension = 2,
        Electrical = 3,
        Body = 4,
        Cooling = 5,
        Transmission = 6
    }

    public static class VehicleSystemExtensions
    {
        /// <summary>How many systems exist. Used to size the condition array and its save data.</summary>
        public const int Count = 7;

        public static string DisplayName(this VehicleSystem system)
        {
            switch (system)
            {
                case VehicleSystem.Engine: return "Engine";
                case VehicleSystem.Brakes: return "Brakes";
                case VehicleSystem.Suspension: return "Suspension";
                case VehicleSystem.Electrical: return "Electrical";
                case VehicleSystem.Body: return "Body";
                case VehicleSystem.Cooling: return "Cooling";
                case VehicleSystem.Transmission: return "Transmission";
                default: return system.ToString();
            }
        }

        /// <summary>
        /// What a customer would say is wrong when this system is in a bad way. Used to build the
        /// complaint they arrive with, which is the first thing the player reads about a car.
        /// </summary>
        public static string Complaint(this VehicleSystem system)
        {
            switch (system)
            {
                case VehicleSystem.Engine: return "the engine's making a knocking noise";
                case VehicleSystem.Brakes: return "the brakes feel soft";
                case VehicleSystem.Suspension: return "it crashes over every bump";
                case VehicleSystem.Electrical: return "the dash lights keep flickering";
                case VehicleSystem.Body: return "it's looking rough down one side";
                case VehicleSystem.Cooling: return "the temperature gauge keeps climbing";
                case VehicleSystem.Transmission: return "it's slipping between gears";
                default: return "something doesn't sound right";
            }
        }
    }
}
