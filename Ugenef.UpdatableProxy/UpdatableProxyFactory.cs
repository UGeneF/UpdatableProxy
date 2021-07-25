using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Ugenef.UpdatableProxy
{
    public class UpdatableProxyFactory
    {
        private const string UpdateValueMethodName = "UpdateValue";
        private readonly Lazy<ModuleBuilder> _dynamicModule = new(CreateDynamicModule, LazyThreadSafetyMode.None);

        public UpdatableProxy<TInterface> GetProxy<TInterface>(TInterface initialValue)
            where TInterface: class
        {
            ValidateAndThrow(initialValue);

            var proxyType = EmitType(typeof(TInterface));
            var updateMethod = proxyType.GetMethod(UpdateValueMethodName);
            var ctor = proxyType.GetConstructors().Single();
            var updatableProxy = (TInterface) ctor.Invoke(new object[] {initialValue});
            return new UpdatableProxy<TInterface>(updatableProxy, updateMethod);
        }

        private void ValidateAndThrow<TInterface>(TInterface initialValue) 
            where TInterface : class
        {
            _ = initialValue ?? throw new ArgumentNullException(nameof(initialValue), "Initial value must be not null");

            var typeArgument = typeof(TInterface);
            if (!typeArgument.IsInterface)
            {
                throw new ArgumentException($"Type argument {nameof(TInterface)} must be an interface");
            }
        }

        private Type EmitType(Type interfaceType)
        {
            var propsToGenerate = interfaceType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var tb = GetTypeBuilder($"{interfaceType.Name}UpdatableProxy-{Guid.NewGuid():N}");
            tb.AddInterfaceImplementation(interfaceType);

            var realValueField = tb.DefineField("_realValue", interfaceType, FieldAttributes.Private);
            tb = DefineConstructor(tb, realValueField);
            tb = DefineUpdateMethod(tb, realValueField);

            foreach (var prop in propsToGenerate)
            {
                var propBuilder = tb.DefineProperty(prop.Name, PropertyAttributes.HasDefault, prop.PropertyType, null);
                var newGetter = EmitGetMethod(prop, tb, realValueField);
                propBuilder.SetGetMethod(newGetter);
            }

            return tb.CreateType();
        }

        private MethodBuilder EmitGetMethod(PropertyInfo propertyInfo, TypeBuilder tb, FieldBuilder realValue)
        {
            //TODO an interesting exception occurs if remove the MethodAttributes.Virtual flag
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            var mb = tb.DefineMethod($"get_{propertyInfo.Name}", attr, propertyInfo.PropertyType, Type.EmptyTypes);

            var ilGen = mb.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, realValue);
            ilGen.Emit(OpCodes.Callvirt, propertyInfo.GetMethod);
            ilGen.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(mb, 
                realValue.FieldType.GetMethod($"get_{propertyInfo.Name}", BindingFlags.Public | BindingFlags.Instance));
            return mb;
        }

        private TypeBuilder DefineUpdateMethod(TypeBuilder tb, FieldInfo realValueField)
        {
            var realValueFieldType = realValueField.FieldType;
            var mb = tb.DefineMethod(
                UpdateValueMethodName, 
                MethodAttributes.Public, 
                CallingConventions.HasThis, 
                null, 
                new [] {realValueFieldType});

            var ilGen = mb.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldflda, realValueField);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.Emit(OpCodes.Call, GetInterlockedExchangeMethod(realValueFieldType));
            ilGen.Emit(OpCodes.Pop);
            ilGen.Emit(OpCodes.Ret);

            return tb;
        }

        private TypeBuilder DefineConstructor(TypeBuilder tb, FieldInfo realValueField)
        {
            var fieldType = realValueField.FieldType;
            var cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new[] {fieldType});
            var objCtor = typeof(object).GetConstructor(Array.Empty<Type>());

            var ilGen = cb.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Call, objCtor);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.Emit(OpCodes.Stfld, realValueField);
            ilGen.Emit(OpCodes.Ret);

            return tb;
        }

        private MethodInfo GetInterlockedExchangeMethod(Type typeParameter)
        {
            var method = typeof(Interlocked)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Interlocked.Exchange) && m.ContainsGenericParameters);
            return method.MakeGenericMethod(typeParameter);
        }
        private TypeBuilder GetTypeBuilder(string typeName)
        {
            var moduleBuilder = _dynamicModule.Value;
            var tb = moduleBuilder.DefineType(typeName,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);
            return tb;
        }

        private static ModuleBuilder CreateDynamicModule()
        {
            var assemblyName = new AssemblyName($"UpdatableProxyDynamicAssembly_{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            return assemblyBuilder.DefineDynamicModule("MainModule");
        }
    }
}