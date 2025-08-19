/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2022, Sjofn LLC.
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

namespace LibreMetaverse.Voice.Vivox
{
    public partial class VoiceGateway
    {
        /// <summary>
        /// This is used to get a list of audio devices that can be used for capture (input) of voice.
        /// </summary>
        /// <returns></returns>
        public int AuxGetCaptureDevices()
        {
            return Request("Aux.GetCaptureDevices.1");
        }

        /// <summary>
        /// This is used to get a list of audio devices that can be used for render (playback) of voice.
        /// </summary>
        public int AuxGetRenderDevices()
        {
            return Request("Aux.GetRenderDevices.1");
        }

        /// <summary>
        /// This command is used to select the render device.
        /// </summary>
        /// <param name="renderDeviceSpecifier">The name of the device as returned by the Aux.GetRenderDevices command.</param>
        public int AuxSetRenderDevice(string renderDeviceSpecifier)
        {
            var requestXml = VoiceGateway.MakeXML("RenderDeviceSpecifier", renderDeviceSpecifier);
            return Request("Aux.SetRenderDevice.1", requestXml);
        }

        /// <summary>
        /// This command is used to select the capture device.
        /// </summary>
        /// <param name="captureDeviceSpecifier">The name of the device as returned by the Aux.GetCaptureDevices command.</param>
        public int AuxSetCaptureDevice(string captureDeviceSpecifier)
        {
            var requestXml = VoiceGateway.MakeXML("CaptureDeviceSpecifier", captureDeviceSpecifier);
            return Request("Aux.SetCaptureDevice.1", requestXml);
        }

        /// <summary>
        /// This command is used to start the audio capture process which will cause
        /// AuxAudioProperty Events to be raised. These events can be used to display a
        /// microphone VU meter for the currently selected capture device. This command
        /// should not be issued if the user is on a call.
        /// </summary>
        /// <param name="duration">(unused but required)</param>
        /// <returns></returns>
        public int AuxCaptureAudioStart(int duration)
        {
            var requestXml = VoiceGateway.MakeXML("Duration", duration.ToString());
            return Request("Aux.CaptureAudioStart.1", requestXml);
        }

        /// <summary>
        /// This command is used to stop the audio capture process.
        /// </summary>
        /// <returns></returns>
        public int AuxCaptureAudioStop()
        {
            return Request("Aux.CaptureAudioStop.1");
        }

        /// <summary>
        /// This command is used to set the mic volume while in the audio tuning process.
        /// Once an acceptable mic level is attained, the application must issue a
        /// connector set mic volume command to have that level be used while on voice
        /// calls.
        /// </summary>
        /// <param name="level">the microphone volume (-100 to 100 inclusive)</param>
        /// <returns></returns>
        public int AuxSetMicLevel(int level)
        {
            var requestXml = VoiceGateway.MakeXML("Level", level.ToString());
            return Request("Aux.SetMicLevel.1", requestXml);
        }

        /// <summary>
        /// This command is used to set the speaker volume while in the audio tuning
        /// process. Once an acceptable speaker level is attained, the application must
        /// issue a connector set speaker volume command to have that level be used while
        /// on voice calls.
        /// </summary>
        /// <param name="level">the speaker volume (-100 to 100 inclusive)</param>
        /// <returns></returns>
        public int AuxSetSpeakerLevel(int level)
        {
            var requestXml = VoiceGateway.MakeXML("Level", level.ToString());
            return Request("Aux.SetSpeakerLevel.1", requestXml);
        }
    }
}
