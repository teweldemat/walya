using System;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseResult GetCommentBlock(ParseContext context, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var exp = context.Expression;
            var nextIndex = GetCommentBlock(exp, index, out var parseNode);
            if (nextIndex == index)
                return ParseBlockResult.NoAdvance(index);

            var text = exp.Substring(index, nextIndex - index);
            return new CommentParseResult(nextIndex, text, parseNode);
        }

        static int GetCommentBlock(String exp, int index, out ParseNode parseNode)
        {
            parseNode = null;
            var i = GetLiteralMatch(exp, index, "//");
            if (i == index)
                return index;
            var i2 = exp.IndexOf("\n", i);
            if (i2 == -1)
                i = exp.Length;
            else
                i = i2 + 1;
            parseNode = new ParseNode(ParseNodeType.Comment, index, i - index);
            return i;
        }
    }
}
