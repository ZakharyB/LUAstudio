namespace LUAstudio.Languages.Syntax;

public enum SyntaxKind
{
    None,
    CompilationUnit,
    Block,
    LocalStatement,
    LocalFunctionStatement,
    FunctionDeclaration,
    AssignmentStatement,
    CallStatement,
    ReturnStatement,
    IfStatement,
    WhileStatement,
    ForStatement,
    IdentifierName,
    LiteralExpression,
    TableExpression,
    TableField,
    CallExpression,
    MemberAccessExpression,
    IndexExpression,
    BinaryExpression,
    UnaryExpression,
    FunctionBody,
    ParameterList,
    Parameter,
    TypeAnnotation,
    RequireCall,
    Comment,
    Broken
}
