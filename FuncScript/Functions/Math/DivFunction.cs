using FuncScript.Core;
using FuncScript.Error;

namespace FuncScript.Functions.Math
{
    public class DivFunction : IFsFunction
    {
        public int MaxParsCount => -1;

        public CallType CallType => CallType.Infix;

        public string Symbol => "div";

        public int Precedence => 50;

        public object Evaluate(IFsDataProvider parent, IParameterList pars)
        {
            return EvaluateInternal(pars, i =>
            {
                var value = pars.GetParameter(parent, i);
                return (true, value);
            });
        }

        object EvaluateInternal(IParameterList pars, Func<int, (bool, object)> getPar)
        {
            bool isInt = false, isLong = false;
            int intTotal = 0;
            long longTotal = 0;
            int count = pars.Count;

            if (count == 0)
                return null;

            var first = getPar(0);
            if (!first.Item1)
                return null;

            var firstValue = first.Item2;
            if (firstValue is int firstInt)
            {
                isInt = true;
                intTotal = firstInt;
            }
            else if (firstValue is long firstLong)
            {
                isLong = true;
                longTotal = firstLong;
            }
            else
            {
                throw new TypeMismatchError($"{Symbol}: integer parameters expected");
            }

            for (int i = 1; i < count; i++)
            {
                var parameter = getPar(i);
                if (!parameter.Item1)
                    return null;

                var divisor = parameter.Item2;
                if (divisor is int intDivisor)
                {
                    if (isInt)
                    {
                        intTotal /= intDivisor;
                    }
                    else if (isLong)
                    {
                        longTotal /= intDivisor;
                    }
                }
                else if (divisor is long longDivisor)
                {
                    if (isInt)
                    {
                        PromoteToLong();
                    }

                    longTotal /= longDivisor;
                }
                else
                {
                    throw new TypeMismatchError($"{Symbol}: integer parameters expected");
                }
            }

            if (isLong)
                return longTotal;
            if (isInt)
                return intTotal;

            return null;

            void PromoteToLong()
            {
                if (!isInt)
                    return;
                isInt = false;
                isLong = true;
                longTotal = intTotal;
            }
        }

        public string ParName(int index)
        {
            return $"Op {index + 1}";
        }
    }
}
