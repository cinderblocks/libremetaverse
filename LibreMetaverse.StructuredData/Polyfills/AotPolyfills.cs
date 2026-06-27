// Polyfills for System.Diagnostics.CodeAnalysis AOT/trimmer attributes that are
// only available natively on net5.0+. For older TFMs these are no-op stubs so
// annotation code compiles without runtime effect.
#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message) { Message = message; }
        public string Message { get; }
        public string? Url { get; set; }
    }
}
#endif
