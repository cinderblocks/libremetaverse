/*
 * Copyright (c) 2026, Sjofn LLC.
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
using Microsoft.Extensions.DependencyInjection;

namespace LibreMetaverse
{
    /// <summary>
    /// Extension methods for registering <see cref="GridClient"/> with an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class GridClientServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a <see cref="GridClient"/> singleton and exposes it as both
        /// <see cref="IGridClient"/> and <see cref="GridClient"/> in the service container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional callback to configure <see cref="Settings"/> after the client is constructed.</param>
        /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
        /// <example>
        /// <code>
        /// builder.Services.AddGridClient(settings =>
        /// {
        ///     settings.UserAgent = "MyBot/1.0";
        /// });
        ///
        /// // Then inject IGridClient or GridClient in your services:
        /// public class MyBot(IGridClient client) { ... }
        /// </code>
        /// </example>
        public static IServiceCollection AddGridClient(
            this IServiceCollection services,
            Action<Settings>? configure = null)
        {
            services.AddSingleton<GridClient>(sp =>
            {
                var client = new GridClient();
                configure?.Invoke(client.Settings);
                return client;
            });
            services.AddSingleton<IGridClient>(sp => sp.GetRequiredService<GridClient>());
            return services;
        }
    }
}
