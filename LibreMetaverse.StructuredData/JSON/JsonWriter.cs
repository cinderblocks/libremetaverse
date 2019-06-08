#region Header
/*
 * JsonWriter.cs
 *   Stream-like facility to output JSON text.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 */
#endregion


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace LitJson
{
    internal enum Condition
    {
        InArray,
        InObject,
        NotAProperty,
        Property,
        Value
    }

    internal class WriterContext
    {
        public int  Count;
        public bool InArray;
        public bool InObject;
        public bool ExpectingValue;
        public int  Padding;
    }

    public class JsonWriter
    {
        #region Fields
        private static NumberFormatInfo number_format;

        private WriterContext        context;
        private Stack<WriterContext> ctx_stack;
        private bool                 has_reached_end;
        private char[]               hex_seq;
        private int                  indentation;
        private int                  indent_value;
        private StringBuilder        inst_string_builder;

        #endregion


        #region Properties
        public int IndentValue {
            get { return indent_value; }
            set {
                indentation = (indentation / indent_value) * value;
                indent_value = value;
            }
        }

        public bool PrettyPrint { get; set; }
        public TextWriter TextWriter { get; }
        public bool Validate { get; set; }
        #endregion

        #region Constructors
        static JsonWriter ()
        {
            number_format = NumberFormatInfo.InvariantInfo;
        }

        public JsonWriter ()
        {
            inst_string_builder = new StringBuilder ();
            TextWriter = new StringWriter (inst_string_builder);

            Init ();
        }

        public JsonWriter (StringBuilder sb) :
            this (new StringWriter (sb))
        {
        }

        public JsonWriter (TextWriter writer)
        {
            this.TextWriter = writer ?? throw new ArgumentNullException (nameof(writer));

            Init ();
        }
        #endregion


        #region Private Methods
        private void DoValidation (Condition cond)
        {
            if (! context.ExpectingValue)
                context.Count++;

            if (! Validate)
                return;

            if (has_reached_end)
                throw new JsonException (
                    "A complete JSON symbol has already been written");

            switch (cond) {
            case Condition.InArray:
                if (! context.InArray)
                    throw new JsonException (
                        "Can't close an array here");
                break;

            case Condition.InObject:
                if (! context.InObject || context.ExpectingValue)
                    throw new JsonException (
                        "Can't close an object here");
                break;

            case Condition.NotAProperty:
                if (context.InObject && ! context.ExpectingValue)
                    throw new JsonException (
                        "Expected a property");
                break;

            case Condition.Property:
                if (! context.InObject || context.ExpectingValue)
                    throw new JsonException (
                        "Can't add a property here");
                break;

            case Condition.Value:
                if (! context.InArray &&
                    (! context.InObject || ! context.ExpectingValue))
                    throw new JsonException (
                        "Can't add a value here");

                break;
            }
        }

        private void Init ()
        {
            has_reached_end = false;
            hex_seq = new char[4];
            indentation = 0;
            indent_value = 4;
            PrettyPrint = false;
            Validate = true;

            ctx_stack = new Stack<WriterContext> ();
            context = new WriterContext ();
            ctx_stack.Push (context);
        }

        private static void IntToHex (int n, char[] hex)
        {
            for (int i = 0; i < 4; i++) {
                var num = n % 16;

                if (num < 10)
                    hex[3 - i] = (char) ('0' + num);
                else
                    hex[3 - i] = (char) ('A' + (num - 10));

                n >>= 4;
            }
        }

        private void Indent ()
        {
            if (PrettyPrint)
                indentation += indent_value;
        }


        private void Put (string str)
        {
            if (PrettyPrint && ! context.ExpectingValue)
                for (int i = 0; i < indentation; i++)
                    TextWriter.Write (' ');

            TextWriter.Write (str);
        }

        private void PutNewline (bool add_comma = true)
        {
            if (add_comma && ! context.ExpectingValue &&
                context.Count > 1)
                TextWriter.Write (',');

            if (PrettyPrint && ! context.ExpectingValue)
                TextWriter.Write ('\n');
        }

        private void PutString (string str)
        {
            Put (string.Empty);

            TextWriter.Write ('"');

            int n = str.Length;
            for (int i = 0; i < n; i++) {
                switch (str[i]) {
                case '\n':
                    TextWriter.Write ("\\n");
                    continue;

                case '\r':
                    TextWriter.Write ("\\r");
                    continue;

                case '\t':
                    TextWriter.Write ("\\t");
                    continue;

                case '"':
                case '\\':
                    TextWriter.Write ('\\');
                    TextWriter.Write (str[i]);
                    continue;

                case '\f':
                    TextWriter.Write ("\\f");
                    continue;

                case '\b':
                    TextWriter.Write ("\\b");
                    continue;
                }

                if (str[i] >= 32 && str[i] <= 126) {
                    TextWriter.Write (str[i]);
                    continue;
                }

                // Default, turn into a \uXXXX sequence
                IntToHex ((int) str[i], hex_seq);
                TextWriter.Write ("\\u");
                TextWriter.Write (hex_seq);
            }

            TextWriter.Write ('"');
        }

        private void Unindent ()
        {
            if (PrettyPrint)
                indentation -= indent_value;
        }
        #endregion


        public override string ToString ()
        {
            return inst_string_builder?.ToString () ?? string.Empty;
        }

        public void Reset ()
        {
            has_reached_end = false;

            ctx_stack.Clear ();
            context = new WriterContext ();
            ctx_stack.Push (context);

            inst_string_builder?.Remove(0, inst_string_builder.Length);
        }

        public void Write (bool boolean)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            Put (boolean ? "true" : "false");

            context.ExpectingValue = false;
        }

        public void Write (decimal number)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (double number)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            if (double.IsNaN(number) || double.IsInfinity(number))
                Put("null");
            else
            {
                string str = Convert.ToString(number, number_format);
                Put(str);

                if (str.IndexOf('.') == -1 && str.IndexOf('E') == -1)
                    TextWriter.Write(".0");
            }

            context.ExpectingValue = false;
        }

        public void Write (int number)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (long number)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (string str)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            if (str == null)
                Put ("null");
            else
                PutString (str);

            context.ExpectingValue = false;
        }

        public void Write (ulong number)
        {
            DoValidation (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void WriteArrayEnd ()
        {
            DoValidation (Condition.InArray);
            PutNewline (false);

            ctx_stack.Pop ();
            if (ctx_stack.Count == 1)
                has_reached_end = true;
            else {
                context = ctx_stack.Peek ();
                context.ExpectingValue = false;
            }

            Unindent ();
            Put ("]");
        }

        public void WriteArrayStart ()
        {
            DoValidation (Condition.NotAProperty);
            PutNewline ();

            Put ("[");

            context = new WriterContext {InArray = true};
            ctx_stack.Push (context);

            Indent ();
        }

        public void WriteObjectEnd ()
        {
            DoValidation (Condition.InObject);
            PutNewline (false);

            ctx_stack.Pop ();
            if (ctx_stack.Count == 1)
                has_reached_end = true;
            else {
                context = ctx_stack.Peek ();
                context.ExpectingValue = false;
            }

            Unindent ();
            Put ("}");
        }

        public void WriteObjectStart ()
        {
            DoValidation (Condition.NotAProperty);
            PutNewline ();

            Put ("{");

            context = new WriterContext {InObject = true};
            ctx_stack.Push (context);

            Indent ();
        }

        public void WritePropertyName (string property_name)
        {
            DoValidation (Condition.Property);
            PutNewline ();

            PutString (property_name);

            if (PrettyPrint) {
                if (property_name.Length > context.Padding)
                    context.Padding = property_name.Length;

                for (int i = context.Padding - property_name.Length;
                     i >= 0; i--)
                    TextWriter.Write (' ');

                TextWriter.Write (": ");
            } else
                TextWriter.Write (':');

            context.ExpectingValue = true;
        }
    }
}
