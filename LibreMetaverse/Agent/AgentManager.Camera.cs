/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright(c) 2025, Sjofn, LLC
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

namespace LibreMetaverse
{
    public partial class AgentManager
    {
        public partial class AgentMovement
        {
            /// <summary>
            /// Camera controls for the agent, mostly a thin wrapper around
            /// CoordinateFrame. This class is only responsible for state
            /// tracking and math, it does not send any packets
            /// </summary>
            public class AgentCamera
            {
                /// <summary>Maximum camera draw distance in meters</summary>
                public float Far;

                /// <summary>The camera is a local frame of reference inside
                /// the larger grid space. This is where the math happens</summary>
                private readonly CoordinateFrame Frame;

                /// <summary>Camera position in region coordinates</summary>
                public Vector3 Position
                {
                    get => Frame.Origin;
                    set => Frame.Origin = value;
                }
                /// <summary>Camera forward direction (Y axis of the camera frame)</summary>
                public Vector3 AtAxis
                {
                    get => Frame.YAxis;
                    set => Frame.YAxis = value;
                }
                /// <summary>Camera left direction (X axis of the camera frame)</summary>
                public Vector3 LeftAxis
                {
                    get => Frame.XAxis;
                    set => Frame.XAxis = value;
                }
                /// <summary>Camera up direction (Z axis of the camera frame)</summary>
                public Vector3 UpAxis
                {
                    get => Frame.ZAxis;
                    set => Frame.ZAxis = value;
                }

                /// <summary>
                /// Default constructor
                /// </summary>
                public AgentCamera()
                {
                    Frame = new CoordinateFrame(new Vector3(128f, 128f, 20f));
                    Far = 128f;
                }

                /// <summary>Roll the camera around its forward axis</summary>
                /// <param name="angle">Angle in radians</param>
                public void Roll(float angle)
                {
                    Frame.Roll(angle);
                }

                /// <summary>Pitch the camera around its left axis</summary>
                /// <param name="angle">Angle in radians</param>
                public void Pitch(float angle)
                {
                    Frame.Pitch(angle);
                }

                /// <summary>Yaw the camera around its up axis</summary>
                /// <param name="angle">Angle in radians</param>
                public void Yaw(float angle)
                {
                    Frame.Yaw(angle);
                }

                /// <summary>Orient the camera to point in a given direction</summary>
                /// <param name="target">Direction vector to look toward (does not need to be normalized)</param>
                public void LookDirection(Vector3 target)
                {
                    Frame.LookDirection(target);
                }

                /// <summary>Orient the camera to point in a given direction with an explicit up vector</summary>
                /// <param name="target">Direction vector to look toward</param>
                /// <param name="upDirection">World up direction used to compute the camera roll</param>
                public void LookDirection(Vector3 target, Vector3 upDirection)
                {
                    Frame.LookDirection(target, upDirection);
                }

                /// <summary>Orient the camera to face a compass heading</summary>
                /// <param name="heading">Heading in radians (0 = north, increasing clockwise)</param>
                public void LookDirection(double heading)
                {
                    Frame.LookDirection(heading);
                }

                /// <summary>Move the camera to a position and orient it toward a target point</summary>
                /// <param name="position">New camera position in region coordinates</param>
                /// <param name="target">Point in region coordinates to look at</param>
                public void LookAt(Vector3 position, Vector3 target)
                {
                    Frame.LookAt(position, target);
                }

                /// <summary>Move the camera to a position, orient it toward a target point, and specify the up direction</summary>
                /// <param name="position">New camera position in region coordinates</param>
                /// <param name="target">Point in region coordinates to look at</param>
                /// <param name="upDirection">World up direction used to compute the camera roll</param>
                public void LookAt(Vector3 position, Vector3 target, Vector3 upDirection)
                {
                    Frame.LookAt(position, target, upDirection);
                }

                /// <summary>Set the camera position and orientation from Euler angles</summary>
                /// <param name="position">New camera position in region coordinates</param>
                /// <param name="roll">Roll angle in radians</param>
                /// <param name="pitch">Pitch angle in radians</param>
                /// <param name="yaw">Yaw angle in radians</param>
                public void SetPositionOrientation(Vector3 position, float roll, float pitch, float yaw)
                {
                    Frame.Origin = position;

                    Frame.ResetAxes();

                    Frame.Roll(roll);
                    Frame.Pitch(pitch);
                    Frame.Yaw(yaw);
                }
            }
        }
    }
}
