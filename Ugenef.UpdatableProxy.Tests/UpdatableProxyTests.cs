using Xunit;

namespace Ugenef.UpdatableProxy.Tests
{
    public class UpdatableProxyTests
    {
        [Fact]
        public void ShouldUpdateValue()
        {
            var factory = new UpdatableProxyFactory();
            var proxy = factory.GetProxy<ITestClass>(_initialValue);
            Assert.Equal(proxy.Value.Prop1, _initialValue.Prop1);
            Assert.Equal(proxy.Value.Prop2, _initialValue.Prop2);
            proxy.UpdateValue(_updatedValue);
            Assert.Equal(proxy.Value.Prop1, _updatedValue.Prop1);
            Assert.Equal(proxy.Value.Prop2, _updatedValue.Prop2);
        }

        [Fact]
        public void ShouldKeepSameReferenceAtValueProperty()
        {
            var factory = new UpdatableProxyFactory();
            var proxy = factory.GetProxy<ITestClass>(_initialValue);
            var proxyObjBeforeUpdate = proxy.Value;
            proxy.UpdateValue(_updatedValue);
            Assert.Equal(proxy.Value, proxyObjBeforeUpdate);
        }

        private readonly TestClass _initialValue = new() {Prop1 = "Init value", Prop2 = 1};
        private readonly TestClass _updatedValue = new() {Prop1 = "Updated value", Prop2 = 2};

        public interface ITestClass
        {
            string Prop1 { get; }
            int Prop2 { get; }
        }

        private class TestClass : ITestClass
        {
            public string Prop1 { get; set; }
            public int Prop2 { get; set; }
        }
    }
}