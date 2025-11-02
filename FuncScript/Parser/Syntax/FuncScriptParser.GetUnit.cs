using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetUnit(ParseContext context, IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            // String template
            var stringTemplateResult = GetStringTemplate(context, siblings, index);
            if (stringTemplateResult.HasProgress(index) && stringTemplateResult.ExpressionBlock != null)
            {
                var block = stringTemplateResult.ExpressionBlock;
                block.Pos = index;
                block.Length = stringTemplateResult.NextIndex - index;
                return new ParseBlockResult(stringTemplateResult.NextIndex, block, stringTemplateResult.ParseNode);
            }

            // Simple string literal
            var stringIndex = GetSimpleString(exp, index, out var text, out var stringNode, errors);
            if (stringIndex > index)
            {
                var block = new LiteralBlock(text)
                {
                    Pos = index,
                    Length = stringIndex - index
                };
                if (stringNode != null)
                    siblings?.Add(stringNode);
                return new ParseBlockResult(stringIndex, block, stringNode);
            }

            // Numeric literal
            var numberIndex = GetNumber(exp, index, out var numberValue, out var numberNode, errors);
            if (numberIndex > index)
            {
                var block = new LiteralBlock(numberValue)
                {
                    Pos = index,
                    Length = numberIndex - index
                };
                if (numberNode != null)
                    siblings?.Add(numberNode);
                return new ParseBlockResult(numberIndex, block, numberNode);
            }

            // List expression
            var listResult = GetListExpression(context, siblings, index);
            if (listResult.HasProgress(index) && listResult.Value != null)
            {
                var listExpression = listResult.Value;
                listExpression.Pos = index;
                listExpression.Length = listResult.NextIndex - index;
                return new ParseBlockResult(listResult.NextIndex, listExpression, listResult.ParseNode);
            }

            // Key-value collection or selector definition
            var kvcResult = GetKvcExpression(context, siblings, false, index);
            if (kvcResult.HasProgress(index) && kvcResult.Value != null)
            {
                var kvcExpression = kvcResult.Value;
                kvcExpression.Pos = index;
                kvcExpression.Length = kvcResult.NextIndex - index;
                return new ParseBlockResult(kvcResult.NextIndex, kvcExpression, kvcResult.ParseNode);
            }

            // If-then-else
            var ifResult = GetIfThenElseExpression(context, siblings, index);
            if (ifResult.HasProgress(index) && ifResult.ExpressionBlock != null)
            {
                var block = ifResult.ExpressionBlock;
                block.Pos = index;
                block.Length = ifResult.NextIndex - index;
                return new ParseBlockResult(ifResult.NextIndex, block, ifResult.ParseNode);
            }

            // Case expression
            var caseResult = GetCaseExpression(context, siblings, index);
            if (caseResult.HasProgress(index) && caseResult.ExpressionBlock != null)
            {
                var block = caseResult.ExpressionBlock;
                block.Pos = index;
                block.Length = caseResult.NextIndex - index;
                return new ParseBlockResult(caseResult.NextIndex, block, caseResult.ParseNode);
            }

            // Switch expression
            var switchResult = GetSwitchExpression(context, siblings, index);
            if (switchResult.HasProgress(index) && switchResult.ExpressionBlock != null)
            {
                var block = switchResult.ExpressionBlock;
                block.Pos = index;
                block.Length = switchResult.NextIndex - index;
                return new ParseBlockResult(switchResult.NextIndex, block, switchResult.ParseNode);
            }

            // Lambda expression
            var lambdaResult = GetLambdaExpression(context, siblings, index);
            if (lambdaResult.HasProgress(index) && lambdaResult.Value != null)
            {
                var block = new LiteralBlock(lambdaResult.Value)
                {
                    Pos = index,
                    Length = lambdaResult.NextIndex - index
                };
                return new ParseBlockResult(lambdaResult.NextIndex, block, lambdaResult.ParseNode);
            }

            // Keyword literal (null/true/false)
            var keywordIndex = GetKeyWordLiteral(exp, index, out var keywordValue, out var keywordNode);
            if (keywordIndex > index)
            {
                var block = new LiteralBlock(keywordValue)
                {
                    Pos = index,
                    Length = keywordIndex - index
                };
                if (keywordNode != null)
                    siblings?.Add(keywordNode);
                return new ParseBlockResult(keywordIndex, block, keywordNode);
            }

            // Identifier reference
            var identifierIndex = GetIdentifier(exp, index, out var identifier, out _, out var identifierNode);
            if (identifierIndex > index)
            {
                var reference = new ReferenceBlock(identifier)
                {
                    Pos = index,
                    Length = identifierIndex - index
                };
                if (identifierNode != null)
                    siblings?.Add(identifierNode);
                return new ParseBlockResult(identifierIndex, reference, identifierNode);
            }

            // Expression in parenthesis
            var parenthesisResult = GetExpInParenthesis(context, siblings, index);
            if (parenthesisResult.HasProgress(index) && parenthesisResult.ExpressionBlock != null)
            {
                var block = parenthesisResult.ExpressionBlock;
                block.Pos = index;
                block.Length = parenthesisResult.NextIndex - index;
                return parenthesisResult;
            }

            // Prefix operator
            var prefixResult = GetPrefixOperator(context, siblings, index);
            if (prefixResult.HasProgress(index) && prefixResult.ExpressionBlock != null)
            {
                var block = prefixResult.ExpressionBlock;
                block.Pos = index;
                block.Length = prefixResult.NextIndex - index;
                return prefixResult;
            }

            return ParseBlockResult.NoAdvance(index);
        }
    }
}
