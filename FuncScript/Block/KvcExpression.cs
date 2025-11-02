using System.ComponentModel.Design;
using System.Data;
using FuncScript.Core;
using FuncScript.Model;
using System.Security.Cryptography;
using System.Text;
using FuncScript.Error;

namespace FuncScript.Block
{
    public class KvcExpression : ExpressionBlock
    {
        private class KvcExpressionProvider : IFsDataProvider
        {
            private readonly KvcExpression _parent;
            private readonly Dictionary<string, object> _valCache = new Dictionary<string, object>();
            public IFsDataProvider ParentProvider { get; }

            public bool IsDefined(string key)
            {
                return _parent.index.ContainsKey(key);
            }

            public KvcExpressionProvider(IFsDataProvider provider, KvcExpression parent)
            {
                this.ParentProvider = provider;
                _parent = parent;
            }
            String _evaluating = null;
            public object Get(string name)
            {
                if (_valCache.TryGetValue(name, out var val))
                    return val;
                if (_evaluating == null || name != _evaluating)
                {
                    if (_parent.index.TryGetValue(name, out var exp) && exp.ValueExpression != null)
                    {
                        _evaluating = name;
                        var v = exp.ValueExpression.Evaluate(this);
                        _evaluating = null;
                        _valCache[name] = v;
                        return v;
                    }
                }
                return ParentProvider.Get(name);
            }
        }
        
        public class KeyValueExpression
        {
            public String Key;
            public String KeyLower;
            public ExpressionBlock ValueExpression;
        }

        public class ConnectionExpression
        {
            public ExpressionBlock Source;
            public ExpressionBlock Sink;
            public ExpressionBlock Catch;
        }


        public IList<KeyValueExpression> _keyValues;
        public ExpressionBlock singleReturn = null;

        private Dictionary<string, KeyValueExpression> index;

        class ConnectionInfo
        {
            public object sink;
            public object vref;

            public CodeLocation location;

            protected void ThrowInvalidConnectionError(string msg)
            {
                throw new Error.EvaluationException(location, new TypeMismatchError(msg));
            }
        }

        

        public String SetKeyValues(IList<KeyValueExpression> kv, ExpressionBlock retExpression)
        {
            _keyValues = kv;
            this.singleReturn = retExpression;

            if (_keyValues == null)
                index = null;
            else
            {
                index = new Dictionary<string, KeyValueExpression>();
                foreach (var k in _keyValues)
                {
                    k.KeyLower = k.Key.ToLower();
                    if (this.index.ContainsKey(k.KeyLower))
                        return $"Key {k.KeyLower} is duplicated";
                    this.index.Add(k.KeyLower, k);
                }
            }

            return null;
        }

        public IList<KeyValueExpression> KeyValues => _keyValues;

        public override object Evaluate(IFsDataProvider provider)
        {
            var evalProvider = new KvcExpressionProvider(provider, this);

            if (singleReturn != null)
            {
                return singleReturn.Evaluate(evalProvider);
            }
            var kvc = new SimpleKeyValueCollection(null, this._keyValues
                .Select(kv => KeyValuePair.Create<string, object>(kv.Key,
                    evalProvider.Get(kv.KeyLower))).ToArray());
            return kvc;
        }

        public override IList<ExpressionBlock> GetChilds()
        {
            var ret = new List<ExpressionBlock>();
            ret.AddRange(this.KeyValues.Select(x => x.ValueExpression));
            return ret;
        }

        public override string ToString()
        {
            return "Key-values";
        }

        public override string AsExpString(IFsDataProvider provider)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            foreach (var kv in this.KeyValues)
            {
                sb.Append($"\t\n{kv.Key}: {kv.ValueExpression.AsExpString(provider)},");
            }

            if (this.singleReturn != null)
            {
                sb.Append($"return {this.singleReturn.AsExpString(provider)}");
            }

            sb.Append("}");
            return sb.ToString();
        }
    }
}
