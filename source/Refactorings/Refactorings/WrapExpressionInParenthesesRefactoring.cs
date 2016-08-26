﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pihrtsoft.CodeAnalysis.CSharp.Refactorings
{
    internal static class WrapExpressionInParenthesesRefactoring
    {
        public static bool CanRefactor(RefactoringContext context, ExpressionSyntax expression)
        {
            if (SyntaxHelper.AreParenthesesUnnecessary(expression))
                return false;

            try
            {
                Refactor(expression, context.Root);
            }
            catch (InvalidCastException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Debug.Assert(false, nameof(WrapExpressionInParenthesesRefactoring));
                return false;
            }

            return true;
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            ExpressionSyntax expression,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            SyntaxNode newRoot = Refactor(expression, root);

            return document.WithSyntaxRoot(newRoot);
        }

        private static SyntaxNode Refactor(ExpressionSyntax expression, SyntaxNode root)
        {
            return root.ReplaceNode(
                expression,
                expression.Parenthesize(cutCopyTrivia: true));
        }
    }
}