// Polyfills for System.Diagnostics.CodeAnalysis AOT/trimmer attributes that are
// only available natively on net5.0+/net6.0+. For older TFMs these are no-op
// stubs so annotation code compiles without runtime effect.
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

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }
        public string Category { get; }
        public string CheckId { get; }
        public string? Scope { get; set; }
        public string? Target { get; set; }
        public string? MessageId { get; set; }
        public string? Justification { get; set; }
    }
}
#endif

#if !NET6_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, Inherited = false)]
    internal sealed class RequiresAssemblyFilesAttribute : Attribute
    {
        public RequiresAssemblyFilesAttribute() { }
        public RequiresAssemblyFilesAttribute(string message) { Message = message; }
        public string? Message { get; }
        public string? Url { get; set; }
    }
}
#endif
