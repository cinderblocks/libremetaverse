// Polyfills for System.Diagnostics.CodeAnalysis AOT/trimmer attributes that are
// only available natively on net5.0+. For older TFMs these are no-op stubs so
// annotation code compiles without runtime effect.
#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
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
