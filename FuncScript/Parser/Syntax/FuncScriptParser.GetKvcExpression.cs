using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<KvcExpression> GetKvcExpression(ParseContext context, IList<ParseNode> siblings,
            bool nakedMode, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var currentIndex = index;
            if (!nakedMode)
            {
                var afterOpen = GetToken(exp, currentIndex,siblings,ParseNodeType.OpenBrace, "{");
                if (afterOpen == currentIndex)
                    return new ValueParseResult<KvcExpression>(index, null, null);

                currentIndex = afterOpen;
            }

            var keyValues = new List<KvcExpression.KeyValueExpression>();
            ExpressionBlock returnExpression = null;
            var nodeItems = new List<ParseNode>();

            while (true)
            {
                var beforeItemWhitespace = SkipSpace(context, currentIndex);
                if (beforeItemWhitespace.HasProgress(currentIndex))
                {
                    currentIndex = beforeItemWhitespace.NextIndex;
                    if (beforeItemWhitespace.ParseNode != null)
                        nodeItems.Add(beforeItemWhitespace.ParseNode);
                }

                var itemResult = GetKvcItem(context, nodeItems, nakedMode, currentIndex);
                if (!itemResult.HasProgress(currentIndex))
                    break;

                if (itemResult.Value.Key == null)
                {
                    if (returnExpression != null)
                    {
                        var errorPos = itemResult.ParseNode?.Pos ?? currentIndex;
                        errors.Add(new SyntaxErrorData(errorPos, nodeItems.Count, "Duplicate return statement"));
                        return new ValueParseResult<KvcExpression>(index, null, null);
                    }

                    returnExpression = itemResult.Value.ValueExpression;
                }
                else
                {
                    keyValues.Add(itemResult.Value);
                }

                currentIndex = itemResult.NextIndex;

                var afterItemWhitespace = SkipSpace(context, currentIndex);
                if (afterItemWhitespace.HasProgress(currentIndex))
                {
                    currentIndex = afterItemWhitespace.NextIndex;
                    if (afterItemWhitespace.ParseNode != null)
                        nodeItems.Add(afterItemWhitespace.ParseNode);
                }

                var afterSeparator = GetToken(exp, currentIndex, nodeItems, ParseNodeType.ListSeparator, ",", ";");
                if (afterSeparator > currentIndex)
                    currentIndex = afterSeparator;
            }

            if (!nakedMode)
            {
                var afterClose = GetToken(exp, currentIndex,siblings,ParseNodeType.CloseBrance, "}");
                if (afterClose == currentIndex)
                {
                    errors.Add(new SyntaxErrorData(currentIndex, 0, "'}' expected"));
                    return new ValueParseResult<KvcExpression>(index, null, null);
                }

                currentIndex = afterClose;
            }
            else if (keyValues.Count == 0 && returnExpression == null)
            {
                return new ValueParseResult<KvcExpression>(index, null, null);
            }

            var kvcExpression = new KvcExpression();
            var validationError = kvcExpression.SetKeyValues(keyValues.ToArray(), returnExpression);
            if (validationError != null)
            {
                errors.Add(new SyntaxErrorData(index, currentIndex - index, validationError));
                return new ValueParseResult<KvcExpression>(index, null, null);
            }

            var parseNode = new ParseNode(ParseNodeType.KeyValueCollection, index, currentIndex - index, nodeItems);
            siblings?.Add(parseNode);
            return new ValueParseResult<KvcExpression>(currentIndex, kvcExpression, parseNode);
        }
    }
}
