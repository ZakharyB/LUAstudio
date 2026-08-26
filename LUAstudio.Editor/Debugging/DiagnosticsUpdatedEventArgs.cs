using LUAstudio.Editor.Diagnostics;

namespace LUAstudio.Editor.Debugging
{
    public class DiagnosticsUpdatedEventArgs : EventArgs
    {
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public DiagnosticsUpdatedEventArgs(IReadOnlyList<Diagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }
    }
}