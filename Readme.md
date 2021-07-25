# The problem

Sometimes you have an object, which properties you want to update time after time - for instance, suppose you have a singleton config object that lives in a DI-container and which already saved to private fields of other singleton services that depend on that config. The properties of that object must be kept up-to-date with, say, a record in a database. By the way, for various reasons, you can have no access to `IOptionsMonitor<TConfig>` and a custom `ConfigurationProvider` - to the recommended choice for solving this problem.
How can you update the properties of the object? You can call public setters - but that is no the option because setters are not threadsafe. You can change the values of the private fields using reflection. Or you can generate a type in runtime with the same interface as the source object has, and then proxy calls to those properties to the instance of your config that you are free to change in a threadsafe manner, as I did.

# Solution

My code works this way:

```
var factory = new UpdatableProxyFactory();
var proxy = factory.GetProxy<ITestClass>(_initialValue);
Assert.Equal(proxy.Value.Prop1, _initialValue.Prop1);
Assert.Equal(proxy.Value.Prop2, _initialValue.Prop2);
proxy.UpdateValue(_updatedValue);
Assert.Equal(proxy.Value.Prop1, _updatedValue.Prop1);
Assert.Equal(proxy.Value.Prop2, _updatedValue.Prop2);
```

For the instance, for the interface

```
interface IExampleClass
{
    string Prop1 { get; }
    int Prop2 { get; }
}
```

My code will generate the type

```
class ExampleClass : IExampleClass
{
    private IExampleClass _realValue;
    public string Prop1 => _realValue.Prop1;
    public int Prop2 => _realValue.Prop2;

    public void UpdateValue(IExampleClass newRealValue)
    {
        Interlocked.Exchange(ref _realValue, newRealValue);
    }
}
```

And provide a wrapper over that generated type, that will allow you
to change the value of the internal field any time you want:

```
class UpdatableProxy<TInterface>
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
```

I think that the correct solution (of course, without considering the standard Microsoft approach) should change the values of the private fields of properties
(the backside of my solution - by obvious reasons it wont work with internal interfaces), but I have written this solution in a couple of hours only to have some fun with IL emitting.
