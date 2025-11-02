
namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetKeyWordLiteral(ParseContext context,IList<ParseNode> siblings, int index, out object literal, out ParseNode parseNode)
        {
            parseNode = null;
            literal = null;

            if (index >= context.Expression.Length)
                return index;

            var currentIndex = SkipSpace(context,siblings,  index);
            if (currentIndex >= context.Expression.Length)
                return index;

            var i = GetLiteralMatch(context.Expression, currentIndex, "null");
            if (i > currentIndex)
            {
                literal = null;
            }
            else if ((i = GetLiteralMatch(context.Expression, currentIndex, "true")) > currentIndex)
            {
                literal = true;
            }
            else if ((i = GetLiteralMatch(context.Expression, currentIndex, "false")) > currentIndex)
            {
                literal = false;
            }
            else
            {
                literal = null;
                return index;
            }

            parseNode = new ParseNode(ParseNodeType.KeyWord, currentIndex, i - currentIndex);
            siblings.Add(parseNode);
            return i;
        }
    }
}
