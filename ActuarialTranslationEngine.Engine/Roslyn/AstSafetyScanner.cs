namespace ActuarialTranslationEngine.Engine.Roslyn;

using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class AstSafetyScanner : CSharpSyntaxWalker
{
    private readonly List<string> _violations = new();
    public IReadOnlyList<string> Violations => _violations;

    private readonly HashSet<string> _allowedNamespaces;
    private readonly HashSet<string> _forbiddenNamespaces;

    public AstSafetyScanner(LlmBridgeConfiguration config)
    {
        _allowedNamespaces = new HashSet<string>(config.AllowedNamespaces);
        _forbiddenNamespaces = new HashSet<string>(config.ForbiddenNamespaces);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        string ns = node.Name.ToString();
        if (!_allowedNamespaces.Contains(ns))
        {
            _violations.Add($"Unauthorized using directive: {ns}. Only System and System.Collections.Generic are permitted.");
        }
        
        base.VisitUsingDirective(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        string access = node.ToString();
        foreach (var forbidden in _forbiddenNamespaces)
        {
            if (access.StartsWith(forbidden))
            {
                _violations.Add($"Unauthorized member access: {access} touches forbidden namespace {forbidden}.");
                break;
            }
        }
        
        // Also explicitly catch Process.Start
        if (access.Contains("Process.Start"))
        {
            _violations.Add("Process.Start is explicitly forbidden.");
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        string name = node.Identifier.Text;
        
        // Catch direct reference to File, Directory, Process if they somehow bypassed namespace checks
        if (name == "File" || name == "Directory" || name == "Process" || name == "HttpClient")
        {
            _violations.Add($"Suspicious identifier used: {name}");
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        if (node.Condition.ToString() == "true")
        {
            _violations.Add("while(true) infinite loops are explicitly forbidden.");
        }
        base.VisitWhileStatement(node);
    }
}
