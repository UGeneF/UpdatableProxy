using System.Reflection;

namespace Ugenef.UpdatableProxy
{
    public class UpdatableProxy<TInterface>
        where TInterface : class
    {
        public TInterface Value { get; }

        private readonly MethodInfo _refreshMethod;

        internal UpdatableProxy(TInterface value, MethodInfo refreshMethod)
        {
            Value = value;
            _refreshMethod = refreshMethod;
        }

        public void UpdateValue(TInterface newValue)
        {
            _refreshMethod.Invoke(Value, new object[] {newValue});
        }
    }
}