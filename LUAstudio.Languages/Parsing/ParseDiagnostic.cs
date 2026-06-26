using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

public enum DiagnosticSeverity
{
    Hidden,
    Info,
    Warning,
    Error,
    Hint
}

public sealed record ParseDiagnostic(
    string Code,
    string Message,
    TextSpan Span,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error);
