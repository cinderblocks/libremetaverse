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
using System.IO;
using System.Xml;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.ImportExport
{
    /// <summary>A model is bigger than a limit of the loader allows</summary>
    internal sealed class LimitExceededException : Exception
    {
        public LimitExceededException(string message) : base(message)
        {
        }
    }

    internal static class ModelFailures
    {
        /// <summary>
        /// Reports why a file was refused. The exceptions a loader throws on purpose say the file is damaged or too
        /// big; the rest say the loader met something it did not expect, which is still a refusal but a different thing
        /// to tell the user.
        /// </summary>
        /// <param name="format">What to call the format in the message and the log</param>
        internal static void ReportFailure(this ModelDiagnosticCollector diagnostics, Exception ex, string format)
        {
            diagnostics.Error(Classify(ex), $"Failed parsing {format} file: {ex.Message}", exception: ex);
        }

        internal static ModelDiagnosticCode Classify(Exception ex)
        {
            if (ex is LimitExceededException) return ModelDiagnosticCode.LimitExceeded;
            if (ex is NotSupportedException) return ModelDiagnosticCode.FileUnsupported;
            if (ex is IOException || ex is UnauthorizedAccessException)
                return ModelDiagnosticCode.FileUnreadable;
            if (ex is InvalidDataException || ex is XmlException || ex is OSDException || ex is FormatException || IsJsonException(ex) ||
                ex is InvalidOperationException && ex.InnerException is XmlException)
                return ModelDiagnosticCode.FileMalformed;
            return ModelDiagnosticCode.LoaderFailed;
        }

        // System.Text.Json is not a dependency of every target, so the exception is recognised by name
        private static bool IsJsonException(Exception ex)
        {
            for (var type = ex.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == "System.Text.Json.JsonException") return true;
            }
            return false;
        }
    }
}
