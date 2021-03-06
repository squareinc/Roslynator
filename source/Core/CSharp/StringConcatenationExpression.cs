﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Utilities;

namespace Roslynator.CSharp
{
    internal struct StringConcatenationExpression
    {
        private StringConcatenationExpression(BinaryExpressionSyntax addExpression, IEnumerable<ExpressionSyntax> expressions, TextSpan? span = null)
        {
            ContainsNonSpecificExpression = false;
            ContainsRegularLiteralExpression = false;
            ContainsVerbatimLiteralExpression = false;
            ContainsRegularInterpolatedStringExpression = false;
            ContainsVerbatimInterpolatedStringExpression = false;

            OriginalExpression = addExpression;
            Expressions = ImmutableArray.CreateRange(expressions);
            Span = span;

            foreach (ExpressionSyntax expression in expressions)
            {
                SyntaxKind kind = expression.Kind();

                if (kind == SyntaxKind.StringLiteralExpression)
                {
                    if (((LiteralExpressionSyntax)expression).IsVerbatimStringLiteral())
                    {
                        ContainsVerbatimLiteralExpression = true;
                    }
                    else
                    {
                        ContainsRegularLiteralExpression = true;
                    }
                }
                else
                {
                    if (kind == SyntaxKind.InterpolatedStringExpression)
                    {
                        if (((InterpolatedStringExpressionSyntax)expression).IsVerbatim())
                        {
                            ContainsVerbatimInterpolatedStringExpression = true;
                        }
                        else
                        {
                            ContainsRegularInterpolatedStringExpression = true;
                        }
                    }
                    else
                    {
                        ContainsNonSpecificExpression = true;
                    }
                }
            }
        }

        public ImmutableArray<ExpressionSyntax> Expressions { get; }

        public BinaryExpressionSyntax OriginalExpression { get; }

        public TextSpan? Span { get; }

        public bool ContainsNonSpecificExpression { get; }

        public bool ContainsNonLiteralExpression
        {
            get { return ContainsInterpolatedStringExpression || ContainsNonSpecificExpression; }
        }

        public bool ContainsLiteralExpression
        {
            get { return ContainsRegularLiteralExpression || ContainsVerbatimLiteralExpression; }
        }

        public bool ContainsRegularLiteralExpression { get; }

        public bool ContainsVerbatimLiteralExpression { get; }

        public bool ContainsInterpolatedStringExpression
        {
            get { return ContainsRegularInterpolatedStringExpression || ContainsVerbatimInterpolatedStringExpression; }
        }

        public bool ContainsRegularInterpolatedStringExpression { get; }

        public bool ContainsVerbatimInterpolatedStringExpression { get; }

        public bool ContainsRegular
        {
            get { return ContainsRegularLiteralExpression || ContainsRegularInterpolatedStringExpression; }
        }

        public bool ContainsVerbatim
        {
            get { return ContainsVerbatimLiteralExpression || ContainsVerbatimInterpolatedStringExpression; }
        }

        public static StringConcatenationExpression Create(BinaryExpressionSyntax binaryExpression, SemanticModel semanticModel)
        {
            return Create(binaryExpression, semanticModel, default(CancellationToken));
        }

        public static StringConcatenationExpression Create(
            BinaryExpressionSyntax binaryExpression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (!binaryExpression.IsKind(SyntaxKind.AddExpression))
                throw new ArgumentException("", nameof(binaryExpression));

            List<ExpressionSyntax> expressions = GetExpressions(binaryExpression, semanticModel, cancellationToken);

            if (expressions == null)
                throw new ArgumentException("", nameof(binaryExpression));

            expressions.Reverse();

            return new StringConcatenationExpression(binaryExpression, expressions);
        }

        public static StringConcatenationExpression Create(BinaryExpressionSelection binaryExpressionSelection, SemanticModel semanticModel)
        {
            return Create(binaryExpressionSelection, semanticModel, default(CancellationToken));
        }

        public static StringConcatenationExpression Create(
            BinaryExpressionSelection binaryExpressionSelection,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            BinaryExpressionSyntax binaryExpression = binaryExpressionSelection.BinaryExpression;
            ImmutableArray<ExpressionSyntax> expressions = binaryExpressionSelection.Expressions;

            if (!binaryExpression.IsKind(SyntaxKind.AddExpression))
                throw new ArgumentException("", nameof(binaryExpressionSelection));

            if (expressions.Any(expression => !IsStringExpression(expression, semanticModel, cancellationToken)))
                throw new ArgumentException("", nameof(BinaryExpressionSelection));

            return new StringConcatenationExpression(binaryExpression, expressions, binaryExpressionSelection.Span);
        }

        public static bool TryCreate(
            BinaryExpressionSyntax binaryExpression,
            SemanticModel semanticModel,
            out StringConcatenationExpression concatenation,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (binaryExpression.IsKind(SyntaxKind.AddExpression))
            {
                List<ExpressionSyntax> expressions = GetExpressions(binaryExpression, semanticModel, cancellationToken);

                if (expressions != null)
                {
                    expressions.Reverse();
                    concatenation = new StringConcatenationExpression(binaryExpression, expressions);
                    return true;
                }
            }

            concatenation = default(StringConcatenationExpression);
            return false;
        }

        public static bool TryCreate(
            BinaryExpressionSelection binaryExpressionSelection,
            SemanticModel semanticModel,
            out StringConcatenationExpression concatenation,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            BinaryExpressionSyntax binaryExpression = binaryExpressionSelection.BinaryExpression;
            ImmutableArray<ExpressionSyntax> expressions = binaryExpressionSelection.Expressions;

            if (binaryExpression.IsKind(SyntaxKind.AddExpression)
                && expressions.All(expression => IsStringExpression(expression, semanticModel, cancellationToken)))
            {
                concatenation = new StringConcatenationExpression(binaryExpression, expressions, binaryExpressionSelection.Span);
                return true;
            }

            concatenation = default(StringConcatenationExpression);
            return false;
        }

        private static bool IsStringExpression(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (expression != null)
            {
                if (expression.IsKind(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxKind.InterpolatedStringExpression))
                {
                    return true;
                }

                if (semanticModel.GetTypeInfo(expression, cancellationToken)
                    .ConvertedType?
                    .IsString() == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<ExpressionSyntax> GetExpressions(
            BinaryExpressionSyntax binaryExpression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            List<ExpressionSyntax> expressions = null;

            while (true)
            {
                MethodInfo methodInfo;
                if (semanticModel.TryGetMethodInfo(binaryExpression, out methodInfo, cancellationToken)
                    && methodInfo.MethodKind == MethodKind.BuiltinOperator
                    && methodInfo.Name == WellKnownMemberNames.AdditionOperatorName
                    && methodInfo.IsContainingType(SpecialType.System_String))
                {
                    (expressions ?? (expressions = new List<ExpressionSyntax>())).Add(binaryExpression.Right);

                    ExpressionSyntax left = binaryExpression.Left;

                    if (left.IsKind(SyntaxKind.AddExpression))
                    {
                        binaryExpression = (BinaryExpressionSyntax)left;
                    }
                    else
                    {
                        expressions.Add(left);
                        return expressions;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public InterpolatedStringExpressionSyntax ToInterpolatedString()
        {
            var sb = new StringBuilder();

            sb.Append('$');

            if (!ContainsRegular)
                sb.Append('@');

            sb.Append('"');

            for (int i = 0; i < Expressions.Length; i++)
            {
                SyntaxKind kind = Expressions[i].Kind();

                if (kind == SyntaxKind.StringLiteralExpression)
                {
                    var literal = (LiteralExpressionSyntax)Expressions[i];

                    if (ContainsRegular
                        && literal.IsVerbatimStringLiteral())
                    {
                        string s = literal.Token.ValueText;
                        s = StringUtility.DoubleBackslash(s);
                        s = StringUtility.EscapeQuote(s);
                        s = StringUtility.DoubleBraces(s);
                        s = s.Replace("\n", @"\n");
                        s = s.Replace("\r", @"\r");
                        sb.Append(s);
                    }
                    else
                    {
                        string s = GetInnerText(literal.Token.Text);
                        s = StringUtility.DoubleBraces(s);
                        sb.Append(s);
                    }
                }
                else if (kind == SyntaxKind.InterpolatedStringExpression)
                {
                    var interpolatedString = (InterpolatedStringExpressionSyntax)Expressions[i];

                    bool isVerbatimInterpolatedString = interpolatedString.IsVerbatim();

                    foreach (InterpolatedStringContentSyntax content in interpolatedString.Contents)
                    {
                        Debug.Assert(content.IsKind(SyntaxKind.Interpolation, SyntaxKind.InterpolatedStringText), content.Kind().ToString());

                        switch (content.Kind())
                        {
                            case SyntaxKind.InterpolatedStringText:
                                {
                                    var text = (InterpolatedStringTextSyntax)content;

                                    if (ContainsRegular
                                        && isVerbatimInterpolatedString)
                                    {
                                        string s = text.TextToken.ValueText;
                                        s = StringUtility.DoubleBackslash(s);
                                        s = StringUtility.EscapeQuote(s);
                                        s = s.Replace("\n", @"\n");
                                        s = s.Replace("\r", @"\r");
                                        sb.Append(s);
                                    }
                                    else
                                    {
                                        sb.Append(content.ToString());
                                    }

                                    break;
                                }
                            case SyntaxKind.Interpolation:
                                {
                                    sb.Append(content.ToString());
                                    break;
                                }
                        }
                    }
                }
                else
                {
                    sb.Append('{');
                    sb.Append(Expressions[i].ToString());
                    sb.Append('}');
                }
            }

            sb.Append("\"");

            return (InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression(sb.ToString());
        }

        public LiteralExpressionSyntax ToStringLiteral()
        {
            if (ContainsNonLiteralExpression)
                throw new InvalidOperationException();

            var sb = new StringBuilder();

            if (!ContainsRegular)
                sb.Append('@');

            sb.Append('"');

            foreach (ExpressionSyntax expression in Expressions)
            {
                if (expression.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var literal = (LiteralExpressionSyntax)expression;

                    if (ContainsRegular
                        && literal.IsVerbatimStringLiteral())
                    {
                        string s = literal.Token.ValueText;
                        s = StringUtility.DoubleBackslash(s);
                        s = StringUtility.EscapeQuote(s);
                        s = s.Replace("\n", @"\n");
                        s = s.Replace("\r", @"\r");
                        sb.Append(s);
                    }
                    else
                    {
                        sb.Append(GetInnerText(literal.Token.Text));
                    }
                }
            }

            sb.Append('"');

            return (LiteralExpressionSyntax)SyntaxFactory.ParseExpression(sb.ToString());
        }

        public LiteralExpressionSyntax ToMultilineStringLiteral()
        {
            if (ContainsNonLiteralExpression)
                throw new InvalidOperationException();

            var sb = new StringBuilder();

            sb.Append('@');
            sb.Append('"');

            for (int i = 0; i < Expressions.Length; i++)
            {
                if (Expressions[i].IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var literal = (LiteralExpressionSyntax)Expressions[i];

                    string s = StringUtility.DoubleQuote(literal.Token.ValueText);

                    int charCount = 0;

                    if (s.Length > 0
                        && s[s.Length - 1] == '\n')
                    {
                        charCount = 1;

                        if (s.Length > 1
                            && s[s.Length - 2] == '\r')
                        {
                            charCount = 2;
                        }
                    }

                    sb.Append(s, 0, s.Length - charCount);

                    if (charCount > 0)
                    {
                        sb.AppendLine();
                    }
                    else if (i < Expressions.Length - 1)
                    {
                        TextSpan span = TextSpan.FromBounds(Expressions[i].Span.End, Expressions[i + 1].SpanStart);

                        if (OriginalExpression.SyntaxTree.IsMultiLineSpan(span))
                            sb.AppendLine();
                    }
                }
            }

            sb.Append('"');

            return (LiteralExpressionSyntax)SyntaxFactory.ParseExpression(sb.ToString());
        }

        public override string ToString()
        {
            if (Span != null)
            {
                TextSpan span = Span.Value;

                return OriginalExpression
                    .ToString()
                    .Substring(span.Start - OriginalExpression.SpanStart, span.Length);
            }
            else
            {
                return OriginalExpression.ToString();
            }
        }

        private static string GetInnerText(string s)
        {
            return (s[0] == '@')
                ? s.Substring(2, s.Length - 3)
                : s.Substring(1, s.Length - 2);
        }
    }
}
