namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetNumber(String exp, int index, out object number, out ParseNode parseNode,
            List<SyntaxErrorData> serros)
        {
            parseNode = null;
            var hasDecimal = false;
            var hasExp = false;
            var hasLong = false;
            number = null;
            if (index >= exp.Length)
                return index;

            var currentIndex = SkipSpace(exp, index);
            if (currentIndex >= exp.Length)
                return index;

            int i = currentIndex;
            var i2 = GetInt(exp, true, i, out var intDigits, out var nodeDigits);
            if (i2 == i)
                return index;
            i = i2;

            i2 = GetLiteralMatch(exp, i, ".");
            if (i2 > i)
                hasDecimal = true;
            i = i2;
            if (hasDecimal)
            {
                i = GetInt(exp, false, i, out var decimalDigits, out var nodeDecimlaDigits);
            }

            i2 = GetLiteralMatch(exp, i, "E");
            if (i2 > i)
                hasExp = true;
            i = i2;
            String expDigits = null;
            ParseNode nodeExpDigits;
            if (hasExp)
                i = GetInt(exp, true, i, out expDigits, out nodeExpDigits);

            if (!hasDecimal) //if no decimal we check if there is the 'l' suffix
            {
                i2 = GetLiteralMatch(exp, i, "l");
                if (i2 > i)
                    hasLong = true;
                i = i2;
            }

            if (hasDecimal) //if it has decimal we treat it as 
            {
                if (!double.TryParse(exp.Substring(currentIndex, i - currentIndex), out var dval))
                {
                    serros.Add(new SyntaxErrorData(currentIndex, i - currentIndex,
                        $"{exp.Substring(currentIndex, i - currentIndex)} couldn't be parsed as floating point"));
                    return index; //we don't expect this to happen
                }

                number = dval;
                parseNode = new ParseNode(ParseNodeType.LiteralDouble, currentIndex, i - currentIndex);
                return i;
            }

            if (hasExp) //it e is included without decimal, zeros are appended to the digits
            {
                if (!int.TryParse(expDigits, out var e) || e < 0)
                {
                    serros.Add(new SyntaxErrorData(currentIndex, expDigits == null ? 0 : expDigits.Length,
                        $"Invalid exponentional {expDigits}"));
                    return index;
                }

                var maxLng = long.MaxValue.ToString();
                if (maxLng.Length + 1 < intDigits.Length + e) //check overflow by length
                {
                    serros.Add(new SyntaxErrorData(currentIndex, expDigits.Length,
                        $"Exponential {expDigits} is out of range"));
                    return index;
                }

                intDigits = intDigits + new string('0', e);
            }

            long longVal;

            if (hasLong) //if l suffix is found
            {
                if (!long.TryParse(intDigits, out longVal))
                {
                    serros.Add(new SyntaxErrorData(currentIndex, expDigits.Length,
                        $"{intDigits} couldn't be parsed to 64bit integer"));
                    return index;
                }

                number = longVal;
                parseNode = new ParseNode(ParseNodeType.LiteralLong, currentIndex, i - currentIndex);
                return i;
            }

            if (int.TryParse(intDigits, out var intVal)) //try parsing as int
            {
                number = intVal;
                parseNode = new ParseNode(ParseNodeType.LiteralInteger, currentIndex, i - currentIndex);
                return i;
            }

            if (long.TryParse(intDigits, out longVal)) //try parsing as long
            {
                number = longVal;
                parseNode = new ParseNode(ParseNodeType.LiteralLong, currentIndex, i - currentIndex);
                return i;
            }

            return index; //all failed
        }
    }
}
