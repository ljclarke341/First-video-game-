using System.Collections.Generic;
using GarageTycoon.Core.Cars;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>One "job prompt + correct tool" pairing used by the tool matching mini-game.</summary>
    public sealed class ToolTask
    {
        public string Prompt { get; private set; }
        public string CorrectTool { get; private set; }

        public ToolTask(string prompt, string correctTool)
        {
            Prompt = prompt;
            CorrectTool = correctTool;
        }
    }

    /// <summary>
    /// The workshop's tool wall, plus the little jobs that call for each tool.
    /// Kept as data so new prompts can be added without touching the mini-game logic.
    /// </summary>
    public static class ToolLibrary
    {
        /// <summary>Every tool that can appear as an option button.</summary>
        public static readonly string[] AllTools =
        {
            "Spanner", "Torque Wrench", "Screwdriver", "Multimeter", "Trolley Jack",
            "Impact Gun", "Panel Hammer", "MIG Welder", "Spray Gun", "Pliers",
            "OBD Scanner", "Grease Gun", "Tire Iron", "Socket Set", "Spring Compressor"
        };

        // Which small tasks belong to which repair job. The mini-game picks one at random.
        private static readonly Dictionary<JobType, ToolTask[]> _tasksByJob = new Dictionary<JobType, ToolTask[]>
        {
            { JobType.Engine, new[] {
                new ToolTask("Torque the head bolts", "Torque Wrench"),
                new ToolTask("Undo the sump plug", "Socket Set"),
                new ToolTask("Re-grease the pulley", "Grease Gun") } },

            { JobType.Tires, new[] {
                new ToolTask("Rattle off the wheel nuts", "Impact Gun"),
                new ToolTask("Lift the front end", "Trolley Jack"),
                new ToolTask("Lever the tire off the rim", "Tire Iron") } },

            { JobType.Brakes, new[] {
                new ToolTask("Crack the bleed nipple", "Spanner"),
                new ToolTask("Torque the caliper bolts", "Torque Wrench"),
                new ToolTask("Free the seized clip", "Pliers") } },

            { JobType.Panels, new[] {
                new ToolTask("Knock out the dent", "Panel Hammer"),
                new ToolTask("Tack the new panel on", "MIG Welder"),
                new ToolTask("Pull the trim clips", "Pliers") } },

            { JobType.Electrics, new[] {
                new ToolTask("Check the earth voltage", "Multimeter"),
                new ToolTask("Undo the dash screws", "Screwdriver"),
                new ToolTask("Crimp the broken wire", "Pliers") } },

            { JobType.Suspension, new[] {
                new ToolTask("Cage the coil spring", "Spring Compressor"),
                new ToolTask("Raise the subframe", "Trolley Jack"),
                new ToolTask("Torque the strut top", "Torque Wrench") } },

            { JobType.Exhaust, new[] {
                new ToolTask("Weld the cracked joint", "MIG Welder"),
                new ToolTask("Undo the manifold nuts", "Socket Set"),
                new ToolTask("Free the rusted clamp", "Impact Gun") } },

            { JobType.Paint, new[] {
                new ToolTask("Lay the clear coat", "Spray Gun"),
                new ToolTask("Pop the badge off", "Screwdriver"),
                new ToolTask("Mask up the door edge", "Pliers") } },

            { JobType.Diagnostics, new[] {
                new ToolTask("Pull the fault codes", "OBD Scanner"),
                new ToolTask("Test the battery drain", "Multimeter"),
                new ToolTask("Open the fuse box", "Screwdriver") } }
        };

        /// <summary>Returns a random task for the given job type (falls back to a generic one).</summary>
        public static ToolTask RandomTaskFor(JobType jobType, Util.IRandomSource random)
        {
            ToolTask[] tasks;
            if (!_tasksByJob.TryGetValue(jobType, out tasks) || tasks.Length == 0)
            {
                return new ToolTask("Tighten the bolt", "Spanner");
            }
            return tasks[random.NextInt(0, tasks.Length)];
        }
    }
}
