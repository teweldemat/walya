using System.Text;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetSimpleString(string exp, int index, out String str, out ParseNode pareNode,
            List<SyntaxErrorData> serrors)
        {
            pareNode = null;
            str = null;

            if (index >= exp.Length)
                return index;

            var currentIndex = SkipSpace(exp, index);
            if (currentIndex >= exp.Length)
                return index;

            var i = GetSimpleString(exp, "\"", currentIndex, out str, out pareNode, serrors);
            if (i == currentIndex)
                i = GetSimpleString(exp, "'", currentIndex, out str, out pareNode, serrors);

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

        static int GetSimpleString(string exp, string delimator, int index, out String str, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            str = null;
            var i = GetLiteralMatch(exp, index, delimator);
            if (i == index)
                return index;
            int i2;
            var sb = new StringBuilder();
            while (true)
            {
                i2 = GetLiteralMatch(exp, i, @"\n");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\n');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\t");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\t');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\\");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\\');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\u");
                if (i2 > i)
                {
                    if (i + 6 <= exp.Length) // Checking if there is enough room for 4 hex digits
                    {
                        var unicodeStr = exp.Substring(i + 2, 4);
                        if (int.TryParse(unicodeStr, System.Globalization.NumberStyles.HexNumber, null,
                                out int charValue))
                        {
                            sb.Append((char)charValue);
                            i += 6; // Move past the "\uXXXX"
                            continue;
                        }
                    }
                }

                i2 = GetLiteralMatch(exp, i, $@"\{delimator}");
                if (i2 > i)
                {
                    sb.Append(delimator);
                    i = i2;
                    continue;
                }

                if (i >= exp.Length || GetLiteralMatch(exp, i, delimator) > i)
                    break;
                sb.Append(exp[i]);
                i++;
            }

            i2 = GetLiteralMatch(exp, i, delimator);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"'{delimator}' expected"));
                return index;
            }

            i = i2;
            str = sb.ToString();
            parseNode = new ParseNode(ParseNodeType.LiteralString, index, i - index);
            return i;
        }


    }
}
