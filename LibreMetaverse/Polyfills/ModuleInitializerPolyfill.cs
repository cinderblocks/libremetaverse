// Polyfill for the compiler's support of module initializers, which the runtimes have always honoured
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif
