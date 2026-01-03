using System;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    /// <summary>
    /// Command to cross region borders by walking or flying in a given direction
    /// </summary>
    public class CrossRegionCommand : Command
    {
        private const float BORDER_DISTANCE = 256.0f; // Standard region size
        private const float CROSSING_BUFFER = 10.0f; // Distance past border to ensure crossing
        private const float MOVEMENT_SPEED = 2.0f; // Approximate walk speed in m/s
        private const int MAX_CROSSING_TIME_MS = 60000; // 60 second timeout
        private const int UPDATE_INTERVAL_MS = 100; // Update movement every 100ms

        public CrossRegionCommand(TestClient testClient)
        {
            Name = "crossregion";
            Description = "Cross region borders by walking or flying in a direction. " +
                         "Usage: crossregion [direction] [walk/fly]\n" +
                         "Directions: north, south, east, west, northeast, northwest, southeast, southwest\n" +
                         "Mode: walk (default) or fly\n" +
                         "Examples:\n" +
                         "  crossregion north\n" +
                         "  crossregion northeast fly\n" +
                         "  crossregion east walk";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: crossregion [direction] [walk/fly]\n" +
                       "Directions: north, south, east, west, northeast, northwest, southeast, southwest";

            // Parse direction
            string direction = args[0].ToLower();
            Vector3 directionVector;
            string directionName;

            switch (direction)
            {
                case "n":
                case "north":
                    directionVector = new Vector3(0, 1, 0);
                    directionName = "North";
                    break;
                case "s":
                case "south":
                    directionVector = new Vector3(0, -1, 0);
                    directionName = "South";
                    break;
                case "e":
                case "east":
                    directionVector = new Vector3(1, 0, 0);
                    directionName = "East";
                    break;
                case "w":
                case "west":
                    directionVector = new Vector3(-1, 0, 0);
                    directionName = "West";
                    break;
                case "ne":
                case "northeast":
                    directionVector = new Vector3(1, 1, 0);
                    directionVector.Normalize();
                    directionName = "Northeast";
                    break;
                case "nw":
                case "northwest":
                    directionVector = new Vector3(-1, 1, 0);
                    directionVector.Normalize();
                    directionName = "Northwest";
                    break;
                case "se":
                case "southeast":
                    directionVector = new Vector3(1, -1, 0);
                    directionVector.Normalize();
                    directionName = "Southeast";
                    break;
                case "sw":
                case "southwest":
                    directionVector = new Vector3(-1, -1, 0);
                    directionVector.Normalize();
                    directionName = "Southwest";
                    break;
                default:
                    return $"Unknown direction: {direction}\n" +
                           "Valid directions: north, south, east, west, northeast, northwest, southeast, southwest";
            }

            // Parse movement mode (walk or fly)
            bool shouldFly = false;
            if (args.Length > 1)
            {
                string mode = args[1].ToLower();
                if (mode == "fly")
                {
                    shouldFly = true;
                }
                else if (mode != "walk")
                {
                    return $"Unknown mode: {mode}. Use 'walk' or 'fly'";
                }
            }

            // Store initial state
            var startSim = Client.Network.CurrentSim;
            var startHandle = startSim?.Handle ?? 0;
            var startPosition = Client.Self.SimPosition;

            Logger.Info($"Starting region crossing {directionName} by {(shouldFly ? "flying" : "walking")} from {startSim?.Name ?? "unknown"} at {startPosition}", Client);

            try
            {
                // Start flying if requested
                if (shouldFly && !Client.Self.Movement.Fly)
                {
                    Client.Self.Fly(true);
                    await Task.Delay(500).ConfigureAwait(false); // Give it a moment to start flying
                }
                else if (!shouldFly && Client.Self.Movement.Fly)
                {
                    Client.Self.Fly(false);
                    await Task.Delay(500).ConfigureAwait(false);
                }

                // Calculate target position (just past the border in the specified direction)
                Vector3 targetPosition = CalculateTargetPosition(startPosition, directionVector);

                Logger.Info($"Target position: {targetPosition}", Client);

                // Set up region crossing detection
                bool crossingDetected = false;
                ulong newSimHandle = 0;
                string newSimName = null;

                EventHandler<RegionCrossedEventArgs> crossingHandler = (sender, e) =>
                {
                    if (e.NewSimulator != null)
                    {
                        crossingDetected = true;
                        newSimHandle = e.NewSimulator.Handle;
                        newSimName = e.NewSimulator.Name;
                        Logger.Info($"Region crossing detected to {newSimName}", Client);
                    }
                };

                Client.Self.RegionCrossed += crossingHandler;

                try
                {
                    // Move toward target using autopilot or manual movement
                    bool success = await MoveToTarget(targetPosition, directionVector, shouldFly).ConfigureAwait(false);

                    // Wait a bit for crossing to complete if we detected it
                    if (crossingDetected)
                    {
                        await Task.Delay(2000).ConfigureAwait(false); // Give crossing state machine time to stabilize
                    }

                    var endSim = Client.Network.CurrentSim;
                    var endHandle = endSim?.Handle ?? 0;
                    var endPosition = Client.Self.SimPosition;

                    // Check if we successfully crossed
                    if (endHandle != startHandle && endHandle != 0)
                    {
                        var crossingState = Client.Self.GetCrossingState();
                        return $"Successfully crossed region border {directionName}\n" +
                               $"From: {startSim?.Name ?? "unknown"} at {startPosition}\n" +
                               $"To: {endSim?.Name ?? "unknown"} at {endPosition}\n" +
                               $"Crossing state: {crossingState}\n" +
                               $"Distance traveled: {Vector3.Distance(startPosition, endPosition):F2}m\n" +
                               $"Mode: {(shouldFly ? "Flying" : "Walking")}";
                    }
                    else if (crossingDetected && !string.IsNullOrEmpty(newSimName))
                    {
                        // Crossing was detected but we might still be processing
                        return $"Region crossing initiated {directionName} to {newSimName}\n" +
                               "Crossing may still be in progress. Use 'stat' to check current location.";
                    }
                    else
                    {
                        var crossingState = Client.Self.GetCrossingState();
                        var failureReason = Client.Self.GetCrossingFailureReason();

                        return $"Failed to cross region border {directionName}\n" +
                               $"Current location: {endSim?.Name ?? "unknown"} at {endPosition}\n" +
                               $"Crossing state: {crossingState}\n" +
                               $"Failure reason: {failureReason}\n" +
                               $"Distance moved: {Vector3.Distance(startPosition, endPosition):F2}m";
                    }
                }
                finally
                {
                    Client.Self.RegionCrossed -= crossingHandler;
                    
                    // Stop movement
                    Client.Self.Movement.AtPos = false;
                    Client.Self.Movement.AtNeg = false;
                    Client.Self.Movement.LeftPos = false;
                    Client.Self.Movement.LeftNeg = false;
                    Client.Self.Movement.SendUpdate(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception during region crossing: {ex.Message}", ex, Client);
                return $"Error during region crossing: {ex.Message}";
            }
        }

        /// <summary>
        /// Calculate target position just past the border in the given direction
        /// </summary>
        private Vector3 CalculateTargetPosition(Vector3 currentPosition, Vector3 direction)
        {
            Vector3 target = currentPosition;

            // Move to just past the border based on direction
            if (direction.X > 0) // East
            {
                target.X = BORDER_DISTANCE + CROSSING_BUFFER;
            }
            else if (direction.X < 0) // West
            {
                target.X = -CROSSING_BUFFER;
            }

            if (direction.Y > 0) // North
            {
                target.Y = BORDER_DISTANCE + CROSSING_BUFFER;
            }
            else if (direction.Y < 0) // South
            {
                target.Y = -CROSSING_BUFFER;
            }

            // For diagonal movement, we need to be careful
            if (Math.Abs(direction.X) > 0 && Math.Abs(direction.Y) > 0)
            {
                // For diagonal, aim for the corner
                if (direction.X > 0) target.X = BORDER_DISTANCE + CROSSING_BUFFER;
                else target.X = -CROSSING_BUFFER;

                if (direction.Y > 0) target.Y = BORDER_DISTANCE + CROSSING_BUFFER;
                else target.Y = -CROSSING_BUFFER;
            }

            // Keep current Z height (with slight buffer for flying)
            target.Z = currentPosition.Z;

            return target;
        }

        /// <summary>
        /// Move toward target position using manual movement controls
        /// </summary>
        private async Task<bool> MoveToTarget(Vector3 targetPosition, Vector3 direction, bool flying)
        {
            var startTime = DateTime.UtcNow;
            var startPosition = Client.Self.SimPosition;
            var startSimHandle = Client.Network.CurrentSim?.Handle ?? 0;

            // Calculate approximate time needed
            float distance = Vector3.Distance(startPosition, targetPosition);
            int estimatedTimeMs = (int)((distance / MOVEMENT_SPEED) * 1000);
            estimatedTimeMs = Math.Min(estimatedTimeMs, MAX_CROSSING_TIME_MS);

            Logger.Info($"Moving {distance:F2}m toward target, estimated time: {estimatedTimeMs / 1000}s", Client);

            // Set movement flags based on direction
            SetMovementFlags(direction, flying);

            // Move until we reach target or timeout
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < MAX_CROSSING_TIME_MS)
            {
                var currentPos = Client.Self.SimPosition;
                var currentSimHandle = Client.Network.CurrentSim?.Handle ?? 0;

                // Check if we crossed to a new region
                if (currentSimHandle != 0 && currentSimHandle != startSimHandle)
                {
                    Logger.Info($"Detected region change from handle {startSimHandle} to {currentSimHandle}", Client);
                    return true;
                }

                // Check if we're close enough to target (or past it in the crossing direction)
                if (HasReachedTarget(startPosition, currentPos, targetPosition, direction))
                {
                    Logger.Info($"Reached target position at {currentPos}", Client);
                    return true;
                }

                // Keep moving
                Client.Self.Movement.SendUpdate(false);

                await Task.Delay(UPDATE_INTERVAL_MS).ConfigureAwait(false);
            }

            Logger.Warn("Crossing timed out", Client);
            return false;
        }

        /// <summary>
        /// Set movement control flags based on direction
        /// </summary>
        private void SetMovementFlags(Vector3 direction, bool flying)
        {
            // Reset all movement flags
            Client.Self.Movement.AtPos = false;
            Client.Self.Movement.AtNeg = false;
            Client.Self.Movement.LeftPos = false;
            Client.Self.Movement.LeftNeg = false;
            Client.Self.Movement.UpPos = false;
            Client.Self.Movement.UpNeg = false;

            // AtPos/AtNeg control movement along the X axis (East/West)
            if (direction.X > 0.1f)
            {
                Client.Self.Movement.AtPos = true; // East (positive X)
            }
            else if (direction.X < -0.1f)
            {
                Client.Self.Movement.AtNeg = true; // West (negative X)
            }

            // LeftPos/LeftNeg control movement along the Y axis (North/South)
            if (direction.Y > 0.1f)
            {
                Client.Self.Movement.LeftPos = true; // North (positive Y)
            }
            else if (direction.Y < -0.1f)
            {
                Client.Self.Movement.LeftNeg = true; // South (negative Y)
            }

            // Ensure flying state matches
            if (flying && !Client.Self.Movement.Fly)
            {
                Client.Self.Movement.Fly = true;
            }

            Client.Self.Movement.SendUpdate(true);
        }

        /// <summary>
        /// Check if we've reached or passed the target position in the crossing direction
        /// </summary>
        private bool HasReachedTarget(Vector3 startPos, Vector3 currentPos, Vector3 targetPos, Vector3 direction)
        {
            // Check if we've moved far enough in the crossing direction
            if (Math.Abs(direction.X) > 0.1f)
            {
                if (direction.X > 0) // Moving East
                {
                    if (currentPos.X >= targetPos.X - 5.0f) return true;
                }
                else // Moving West
                {
                    if (currentPos.X <= targetPos.X + 5.0f) return true;
                }
            }

            if (Math.Abs(direction.Y) > 0.1f)
            {
                if (direction.Y > 0) // Moving North
                {
                    if (currentPos.Y >= targetPos.Y - 5.0f) return true;
                }
                else // Moving South
                {
                    if (currentPos.Y <= targetPos.Y + 5.0f) return true;
                }
            }

            return false;
        }
    }
}
