using System;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetMemberAccess(ParseContext context, IList<ParseNode> siblings,
            ExpressionBlock source, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var dotResult = GetMemberAccess(context, siblings, ".", source, index);
            if (dotResult.HasProgress(index))
                return dotResult;

            return GetMemberAccess(context, siblings, "?.", source, index);
        }

        static ParseBlockResult GetMemberAccess(ParseContext context, IList<ParseNode> siblings, string oper,
            ExpressionBlock source, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (oper == null)
                throw new ArgumentNullException(nameof(oper));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var afterOperator = GetToken(exp, index,siblings,ParseNodeType.Operator, oper);
            if (afterOperator == index)
                return ParseBlockResult.NoAdvance(index);

            var memberIndex = afterOperator;
            var afterIdentifier = GetIdentifier(exp, memberIndex, out var member, out _, out var memberNode);
            if (afterIdentifier == memberIndex)
            {
                errors.Add(new SyntaxErrorData(memberIndex, 0, "member identifier expected"));
                return ParseBlockResult.NoAdvance(index);
            }

            var currentIndex = afterIdentifier;

            var function = context.Provider.Get(oper);
            var expression = new FunctionCallExpression
            {
                Function = new LiteralBlock(function),
                Parameters = new ExpressionBlock[] { source, new LiteralBlock(member) },
                Pos = source.Pos,
                Length = currentIndex - source.Pos
            };

            var childNodes = memberNode != null ? new[] { memberNode } : Array.Empty<ParseNode>();
            var parseNode = new ParseNode(ParseNodeType.MemberAccess, index, currentIndex - index, childNodes);

            siblings?.Add(parseNode);

            return new ParseBlockResult(currentIndex, expression, parseNode);
        }
    }
}
