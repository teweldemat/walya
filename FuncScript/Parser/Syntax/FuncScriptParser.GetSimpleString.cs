using System.Text;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetSimpleString(ParseContext context,IList<ParseNode> siblings, int index, out String str, out ParseNode pareNode,
            List<SyntaxErrorData> serrors)
        {
            pareNode = null;
            str = null;

            if (index >= context.Expression.Length)
                return index;

            var currentIndex = SkipSpace(context,siblings, index);
            if (currentIndex >= context.Expression.Length)
                return index;

            var i = GetSimpleString(context,siblings, "\"", currentIndex, out str, out pareNode, serrors);
            if (i == currentIndex)
                i = GetSimpleString(context,siblings, "'", currentIndex, out str, out pareNode, serrors);

            if (i == currentIndex)
            {
                str = null;
                pareNode = null;
                return index;
            }

            if (pareNode != null)
            {
                pareNode.Pos = currentIndex;
                pareNode.Length = i - currentIndex;
            }

            return i;
        }

        static int GetSimpleString(ParseContext context,IList<ParseNode> siblings, string delimator, int index, out String str, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            str = null;
            var i = GetLiteralMatch(context.Expression, index, delimator);
            if (i == index)
                return index;
            int i2;
            var sb = new StringBuilder();
            while (true)
            {
                i2 = GetLiteralMatch(context.Expression, i, @"\n");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\n');
                    continue;
                }

                i2 = GetLiteralMatch(context.Expression, i, @"\t");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\t');
                    continue;
                }

                i2 = GetLiteralMatch(context.Expression, i, @"\\");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\\');
                    continue;
                }

                i2 = GetLiteralMatch(context.Expression, i, @"\u");
                if (i2 > i)
                {
                    if (i + 6 <= context.Expression.Length) // Checking if there is enough room for 4 hex digits
                    {
                        var unicodeStr = context.Expression.Substring(i + 2, 4);
                        if (int.TryParse(unicodeStr, System.Globalization.NumberStyles.HexNumber, null,
                                out int charValue))
                        {
                            sb.Append((char)charValue);
                            i += 6; // Move past the "\uXXXX"
                            continue;
                        }
                    }
                }

                i2 = GetLiteralMatch(context.Expression, i, $@"\{delimator}");
                if (i2 > i)
                {
                    sb.Append(delimator);
                    i = i2;
                    continue;
                }

                if (i >= context.Expression.Length || GetLiteralMatch(context.Expression, i, delimator) > i)
                    break;
                sb.Append(context.Expression[i]);
                i++;
            }

            i2 = GetLiteralMatch(context.Expression, i, delimator);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"'{delimator}' expected"));
                return index;
            }

            i = i2;
            str = sb.ToString();
            parseNode = new ParseNode(ParseNodeType.LiteralString, index, i - index);
            siblings.Add(parseNode);
            return i;
        }


    }
}
