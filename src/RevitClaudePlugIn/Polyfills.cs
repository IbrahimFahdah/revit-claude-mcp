#if NET48
// Required to compile C# 9 'init'-only properties and 'record' types when targeting net48.
// The attribute exists natively in .NET 5+ but must be declared manually for .NET Framework.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
