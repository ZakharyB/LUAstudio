using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Syntax.Nodes;

public sealed class CompilationUnitSyntax : SyntaxNode
{
    public CompilationUnitSyntax(TextSpan span, IReadOnlyList<SyntaxNode> statements)
        : base(SyntaxKind.CompilationUnit, span, null)
    {
        Statements = statements;
    }

    public IReadOnlyList<SyntaxNode> Statements { get; }

    public override IReadOnlyList<SyntaxNode> Children => Statements;
}

public sealed class BlockSyntax : SyntaxNode
{
    public BlockSyntax(TextSpan span, SyntaxNode? parent, IReadOnlyList<SyntaxNode> statements)
        : base(SyntaxKind.Block, span, parent)
    {
        Statements = statements;
    }

    public IReadOnlyList<SyntaxNode> Statements { get; }

    public override IReadOnlyList<SyntaxNode> Children => Statements;
}

public sealed class LocalStatementSyntax : SyntaxNode
{
    public LocalStatementSyntax(
        TextSpan span,
        SyntaxNode? parent,
        SyntaxToken name,
        SyntaxNode? initializer,
        TypeAnnotationSyntax? typeAnnotation)
        : base(SyntaxKind.LocalStatement, span, parent)
    {
        Name = name;
        Initializer = initializer;
        TypeAnnotation = typeAnnotation;
    }

    public SyntaxToken Name { get; }
    public SyntaxNode? Initializer { get; }
    public TypeAnnotationSyntax? TypeAnnotation { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Name };
            if (TypeAnnotation is not null) list.Add(TypeAnnotation);
            if (Initializer is not null) list.Add(Initializer);
            return list;
        }
    }
}

public sealed class FunctionDeclarationSyntax : SyntaxNode
{
    public FunctionDeclarationSyntax(
        TextSpan span,
        SyntaxNode? parent,
        bool isLocal,
        SyntaxToken name,
        ParameterListSyntax parameters,
        FunctionBodySyntax body,
        TypeAnnotationSyntax? returnType)
        : base(SyntaxKind.FunctionDeclaration, span, parent)
    {
        IsLocal = isLocal;
        Name = name;
        Parameters = parameters;
        Body = body;
        ReturnType = returnType;
    }

    public bool IsLocal { get; }
    public SyntaxToken Name { get; }
    public ParameterListSyntax Parameters { get; }
    public FunctionBodySyntax Body { get; }
    public TypeAnnotationSyntax? ReturnType { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Name, Parameters, Body };
            if (ReturnType is not null) list.Add(ReturnType);
            return list;
        }
    }
}

public sealed class ParameterListSyntax : SyntaxNode
{
    public ParameterListSyntax(TextSpan span, SyntaxNode? parent, IReadOnlyList<ParameterSyntax> parameters)
        : base(SyntaxKind.ParameterList, span, parent)
    {
        Parameters = parameters;
    }

    public IReadOnlyList<ParameterSyntax> Parameters { get; }

    public override IReadOnlyList<SyntaxNode> Children => Parameters.Cast<SyntaxNode>().ToArray();
}

public sealed class ParameterSyntax : SyntaxNode
{
    public ParameterSyntax(TextSpan span, SyntaxNode? parent, SyntaxToken name, TypeAnnotationSyntax? typeAnnotation)
        : base(SyntaxKind.Parameter, span, parent)
    {
        Name = name;
        TypeAnnotation = typeAnnotation;
    }

    public SyntaxToken Name { get; }
    public TypeAnnotationSyntax? TypeAnnotation { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Name };
            if (TypeAnnotation is not null) list.Add(TypeAnnotation);
            return list;
        }
    }
}

public sealed class FunctionBodySyntax : SyntaxNode
{
    public FunctionBodySyntax(TextSpan span, SyntaxNode? parent, BlockSyntax block)
        : base(SyntaxKind.FunctionBody, span, parent)
    {
        Block = block;
    }

    public BlockSyntax Block { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Block];
}

public sealed class TableExpressionSyntax : SyntaxNode
{
    public TableExpressionSyntax(TextSpan span, SyntaxNode? parent, IReadOnlyList<TableFieldSyntax> fields)
        : base(SyntaxKind.TableExpression, span, parent)
    {
        Fields = fields;
    }

    public IReadOnlyList<TableFieldSyntax> Fields { get; }

    public override IReadOnlyList<SyntaxNode> Children => Fields.Cast<SyntaxNode>().ToArray();
}

public sealed class TableFieldSyntax : SyntaxNode
{
    public TableFieldSyntax(TextSpan span, SyntaxNode? parent, SyntaxNode? key, SyntaxNode value)
        : base(SyntaxKind.TableField, span, parent)
    {
        Key = key;
        Value = value;
    }

    public SyntaxNode? Key { get; }
    public SyntaxNode Value { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Value };
            if (Key is not null) list.Insert(0, Key);
            return list;
        }
    }
}

public sealed class CallExpressionSyntax : SyntaxNode
{
    public CallExpressionSyntax(
        TextSpan span,
        SyntaxNode? parent,
        SyntaxNode target,
        IReadOnlyList<SyntaxNode> arguments,
        bool isStatement)
        : base(isStatement ? SyntaxKind.CallStatement : SyntaxKind.CallExpression, span, parent)
    {
        Target = target;
        Arguments = arguments;
    }

    public SyntaxNode Target { get; }
    public IReadOnlyList<SyntaxNode> Arguments { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Target };
            list.AddRange(Arguments);
            return list;
        }
    }
}

public sealed class MemberAccessExpressionSyntax : SyntaxNode
{
    public MemberAccessExpressionSyntax(TextSpan span, SyntaxNode? parent, SyntaxNode expression, SyntaxToken member)
        : base(SyntaxKind.MemberAccessExpression, span, parent)
    {
        Expression = expression;
        Member = member;
    }

    public SyntaxNode Expression { get; }
    public SyntaxToken Member { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Expression, Member];
}

public sealed class IdentifierNameSyntax : SyntaxNode
{
    public IdentifierNameSyntax(TextSpan span, SyntaxNode? parent, SyntaxToken name)
        : base(SyntaxKind.IdentifierName, span, parent)
    {
        Name = name;
    }

    public SyntaxToken Name { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Name];
}

public sealed class LiteralExpressionSyntax : SyntaxNode
{
    public LiteralExpressionSyntax(TextSpan span, SyntaxNode? parent, SyntaxToken token)
        : base(SyntaxKind.LiteralExpression, span, parent)
    {
        Token = token;
    }

    public SyntaxToken Token { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Token];
}

public sealed class TypeAnnotationSyntax : SyntaxNode
{
    public TypeAnnotationSyntax(TextSpan span, SyntaxNode? parent, SyntaxToken typeName)
        : base(SyntaxKind.TypeAnnotation, span, parent)
    {
        TypeName = typeName;
    }

    public SyntaxToken TypeName { get; }

    public override IReadOnlyList<SyntaxNode> Children => [TypeName];
}

public sealed class RequireCallSyntax : SyntaxNode
{
    public RequireCallSyntax(TextSpan span, SyntaxNode? parent, SyntaxToken modulePath)
        : base(SyntaxKind.RequireCall, span, parent)
    {
        ModulePath = modulePath;
    }

    public SyntaxToken ModulePath { get; }

    public override IReadOnlyList<SyntaxNode> Children => [ModulePath];
}

public sealed class IfStatementSyntax : SyntaxNode
{
    public IfStatementSyntax(TextSpan span, SyntaxNode? parent, SyntaxNode condition, BlockSyntax thenBlock, BlockSyntax? elseBlock)
        : base(SyntaxKind.IfStatement, span, parent)
    {
        Condition = condition;
        ThenBlock = thenBlock;
        ElseBlock = elseBlock;
    }

    public SyntaxNode Condition { get; }
    public BlockSyntax ThenBlock { get; }
    public BlockSyntax? ElseBlock { get; }

    public override IReadOnlyList<SyntaxNode> Children
    {
        get
        {
            var list = new List<SyntaxNode> { Condition, ThenBlock };
            if (ElseBlock is not null) list.Add(ElseBlock);
            return list;
        }
    }
}

public sealed class WhileStatementSyntax : SyntaxNode
{
    public WhileStatementSyntax(TextSpan span, SyntaxNode? parent, SyntaxNode condition, BlockSyntax body)
        : base(SyntaxKind.WhileStatement, span, parent)
    {
        Condition = condition;
        Body = body;
    }

    public SyntaxNode Condition { get; }
    public BlockSyntax Body { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Condition, Body];
}

public sealed class ForStatementSyntax : SyntaxNode
{
    public ForStatementSyntax(TextSpan span, SyntaxNode? parent, BlockSyntax body)
        : base(SyntaxKind.ForStatement, span, parent)
    {
        Body = body;
    }

    public BlockSyntax Body { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Body];
}

public sealed class AssignmentStatementSyntax : SyntaxNode
{
    public AssignmentStatementSyntax(TextSpan span, SyntaxNode? parent, SyntaxNode target, SyntaxNode value)
        : base(SyntaxKind.AssignmentStatement, span, parent)
    {
        Target = target;
        Value = value;
    }

    public SyntaxNode Target { get; }
    public SyntaxNode Value { get; }

    public override IReadOnlyList<SyntaxNode> Children => [Target, Value];
}
