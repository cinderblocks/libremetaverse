/*
 * Copyright (c) 2025, Sjofn LLC
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

using System;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Appearance
{
    /// <summary>
    /// Handles the application of an initial outfit for a user upon their first login to the grid.
    /// </summary>
    /// <remarks>This handler listens for the client connection event, and if the user is logging in for the
    /// first time and an initial outfit is specified, applies the designated outfit automatically. Dispose of this
    /// handler when it is no longer needed to unsubscribe from events and release resources.</remarks>
    public class InitialOutfitHandler : IDisposable
    {
        private readonly GridClient client;
        private readonly CurrentOutfitFolder cof;
        private readonly CancellationTokenSource applyCts = new CancellationTokenSource();
        private readonly IProgress<InitialOutfit.InitialOutfitProgress> progress;

        public InitialOutfitHandler(GridClient client, CurrentOutfitFolder cof, IProgress<InitialOutfit.InitialOutfitProgress> progress = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.cof = cof ?? throw new ArgumentNullException(nameof(cof));
            this.progress = progress;

            try
            {
                client.Network.LoginProgress += Network_LoginProgress;
            }
            catch { }
        }

        private void Network_LoginProgress(object sender, LoginProgressEventArgs e)
        {
            if (e.Status != LoginStatus.Success) { return; }

            var token = applyCts.Token;
            Task.Run(async () =>
            {
                try
                {
                    var loginData = client?.Network?.LoginResponseData;

                    if (loginData?.FirstLogin == true && !string.IsNullOrEmpty(loginData.InitialOutfit))
                    {
                        try { client.Network.LoginProgress -= Network_LoginProgress; } catch { }

                        try
                        {
                            try { await client.Self.SetAgentAccessAsync("A", null, token).ConfigureAwait(false); } catch { }

                            var initial = new InitialOutfit(client, cof);
                            await initial.SetInitialOutfitAsync(loginData.InitialOutfit, token, progress).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (ex is OperationCanceledException)
                            {
                                Logger.Debug("InitialOutfitHandler: initial outfit apply cancelled", client);
                            }
                            else
                            {
                                Logger.Error("InitialOutfitHandler: failed to apply initial outfit: " + ex.Message, ex, client);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("InitialOutfitHandler: unexpected error: " + ex.Message, ex, client);
                }
            });
        }

        public void Dispose()
        {
            try { client.Network.LoginProgress -= Network_LoginProgress; } catch { }
            try
            {
                applyCts.Cancel();
                applyCts.Dispose();
            }
            catch { }
        }
    }
}
