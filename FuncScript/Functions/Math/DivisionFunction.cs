using FuncScript.Core;
using FuncScript.Model;

namespace FuncScript.Functions.Math
{
    public class DivisionFunction : IFsFunction
    {
        public int MaxParsCount => -1;

        public CallType CallType => CallType.Infix;

        public string Symbol => "/";

        public int Precedence => 50;

        public object Evaluate(IFsDataProvider parent, IParameterList pars)
        {
            var ret = EvaluateInteral(pars, (i) =>
            {
                var ret = pars.GetParameter(parent, i);
                return (true, ret);
            });
            return ret;
        }
        object EvaluateInteral(IParameterList pars,Func<int,(bool,object)> getPar)
        {
            bool isInt = false, isLong = false, isDouble = false;
            int intTotal = 1;
            long longTotal = 1;
            double doubleTotal = 1;
            int count = pars.Count;

            if (count > 0)
            {
                var p = getPar(0);
                if (!p.Item1)
                    return null;
                var d = p.Item2;

                if (d is int)
                {
                    isInt = true;
                    intTotal = (int)d;
                }
                else if (d is long)
                {
                    isLong = true;
                    longTotal = (long)d;
                }
                else if (d is double)
                {
                    isDouble = true;
                    doubleTotal = (double)d;
                }
                else
                {
                    isInt = true;
                    intTotal = 1;
                }
            }

            for (int i = 1; i < count; i++)
            {
                var p = getPar(i);
                if (!p.Item1)
                    return null;
                var d = p.Item2;

                if (isInt)
                {
                    if (d is int intDiv)
                    {
                        DivideInt(intDiv);
                        continue;
                    }

                    if (d is long longDiv)
                    {
                        PromoteIntToLong();
                        DivideLong(longDiv);
                        continue;
                    }

                    if (d is double doubleDiv)
                    {
                        PromoteIntToDouble();
                        doubleTotal /= doubleDiv;
                        continue;
                    }
                }

                if (isLong)
                {
                    if (d is int intDiv)
                    {
                        DivideLong(intDiv);
                        continue;
                    }

                    if (d is long longDiv)
                    {
                        DivideLong(longDiv);
                        continue;
                    }

                    if (d is double doubleDiv)
                    {
                        PromoteLongToDouble();
                        doubleTotal /= doubleDiv;
                        continue;
                    }
                }

                if (isDouble)
                {
                    if (d is int intDiv)
                    {
                        doubleTotal /= intDiv;
                    }
                    else if (d is long longDiv)
                    {
                        doubleTotal /= longDiv;
                    }
                    else if (d is double doubleDiv)
                    {
                        doubleTotal /= doubleDiv;
                    }
                }
            }

            if (isDouble)
                return doubleTotal;
            if (isLong)
                return longTotal;
            if (isInt)
                return intTotal;

            return null;

            void PromoteIntToLong()
            {
                if (!isInt)
                    return;
                isInt = false;
                isLong = true;
                longTotal = intTotal;
            }

            void PromoteIntToDouble()
            {
                if (isDouble)
                {
                    isInt = false;
                    return;
                }

                isInt = false;
                isDouble = true;
                doubleTotal = intTotal;
            }

            void PromoteLongToDouble()
            {
                if (isDouble)
                {
                    isLong = false;
                    return;
                }

                isLong = false;
                isDouble = true;
                doubleTotal = longTotal;
            }

            void DivideInt(int divisor)
            {
                if (intTotal % divisor == 0)
                {
                    intTotal /= divisor;
                }
                else
                {
                    PromoteIntToDouble();
                    doubleTotal /= divisor;
                }
            }

            void DivideLong(long divisor)
            {
                if (longTotal % divisor == 0)
                {
                    longTotal /= divisor;
                }
                else
                {
                    PromoteLongToDouble();
                    doubleTotal /= divisor;
                }
            }
        }

        public string ParName(int index)
        {
            return $"Op {index + 1}";
        }
    }
}
