// Polyfill required by the C# compiler for 'init' accessors and record types
// when targeting netstandard2.1 or .NET Framework, where the type is not in the BCL.
#if NETSTANDARD2_1 || NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
