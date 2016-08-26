﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pihrtsoft.CodeAnalysis.CSharp.Refactorings
{
    internal static class RemoveAllMemberDeclarationsRefactoring
    {
        public static void ComputeRefactoring(RefactoringContext context, MemberDeclarationSyntax member)
        {
            switch (member.Kind())
            {
                case SyntaxKind.NamespaceDeclaration:
                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.StructDeclaration:
                case SyntaxKind.InterfaceDeclaration:
                    {
                        if (CanRefactor(member, context.Span))
                        {
                            context.RegisterRefactoring(
                                "Remove all members",
                                cancellationToken => RefactorAsync(context.Document, member, cancellationToken));
                        }

                        break;
                    }
            }
        }

        public static bool CanRefactor(MemberDeclarationSyntax member, TextSpan span)
        {
            switch (member.Kind())
            {
                case SyntaxKind.NamespaceDeclaration:
                    {
                        var declaration = (NamespaceDeclarationSyntax)member;

                        return declaration.Members.Any()
                            && (declaration.OpenBraceToken.Span.Contains(span)
                                || declaration.CloseBraceToken.Span.Contains(span));
                    }
                case SyntaxKind.ClassDeclaration:
                    {
                        var declaration = (ClassDeclarationSyntax)member;

                        return declaration.Members.Any()
                            && (declaration.OpenBraceToken.Span.Contains(span)
                                || declaration.CloseBraceToken.Span.Contains(span));
                    }
                case SyntaxKind.StructDeclaration:
                    {
                        var declaration = (StructDeclarationSyntax)member;

                        return declaration.Members.Any()
                            && (declaration.OpenBraceToken.Span.Contains(span)
                                || declaration.CloseBraceToken.Span.Contains(span));
                    }
                case SyntaxKind.InterfaceDeclaration:
                    {
                        var declaration = (InterfaceDeclarationSyntax)member;

                        return declaration.Members.Any()
                            && (declaration.OpenBraceToken.Span.Contains(span)
                                || declaration.CloseBraceToken.Span.Contains(span));
                    }
            }

            return false;
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            MemberDeclarationSyntax member,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            MemberDeclarationSyntax newNode = member
                .SetMembers(List<MemberDeclarationSyntax>())
                .WithFormatterAnnotation();

            SyntaxNode newRoot = root.ReplaceNode(member, newNode);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}