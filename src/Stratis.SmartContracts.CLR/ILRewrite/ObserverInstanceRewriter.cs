﻿using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    public class ObserverInstanceRewriter : IILRewriter
    {
        private const string InjectedNamespace = "<Stratis>";
        public const string InjectedTypeName = "<RuntimeObserverInstance>";
        public const string MethodName = "SetObserver";
        public const string ParameterName = "observer";
        private const string InjectedPropertyName = "Instance";
        public const string ConstructorName = ".cctor";

        /// <summary>
        /// The individual rewriters to be applied to each method, which use the injected type.
        /// </summary>
        private static readonly List<IObserverMethodRewriter> methodRewriters = new List<IObserverMethodRewriter>
        {
            new GasInjectorRewriter(),
            new MemoryLimitRewriter()
        };

        /// <summary>
        /// Completely rewrites a module with all of the code required to meter memory and gas.
        /// Includes the injection of the specific observer for this contract.
        /// </summary>
        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            (FieldDefinition observerInstanceField, TypeDefinition observerType) = this.GetRuntimeInstance(module);
            var observer = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.GetTypes())
            {
                this.RewriteType(type, observer);
            }
            
            module.Types.Add(observerType);

            return module;
        }

        /// <summary>
        /// Inserts a static type into the module which gives access to an instance of <see cref="Observer"/>.
        /// Because this is injected per module, it counts as a separate type and will not be a shared static.
        /// </summary>
        private (FieldDefinition, TypeDefinition) GetRuntimeInstance(ModuleDefinition module)
        {
            // Add new type that can't be instantiated
            var instanceType = new TypeDefinition(
                InjectedNamespace, InjectedTypeName,
                TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                module.ImportReference(typeof(object))
            );

            // Add a field - an instance of our Observer!
            var instanceField = new FieldDefinition(
                InjectedPropertyName,
                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
                module.ImportReference(typeof(Observer))
            );
            instanceType.Fields.Add(instanceField);

            // Add a static method that can be used to set the observer instance.
            var method = new MethodDefinition(
                MethodName,
                MethodAttributes.Public | MethodAttributes.Static,
                module.ImportReference(typeof(void))
            );

            method.Parameters.Add(new ParameterDefinition(ParameterName, ParameterAttributes.None, module.ImportReference(typeof(Observer))));

            ILProcessor il = method.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, instanceField);
            il.Emit(OpCodes.Ret);
            instanceType.Methods.Add(method);

            // When this type is created, retrieve the Observer from our global static dictionary so it can be used.
            var constructor = new MethodDefinition(
                ConstructorName, MethodAttributes.Private | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Static,
                module.ImportReference(typeof(void))
            );
            ILProcessor constructorIl = constructor.Body.GetILProcessor();
            constructorIl.Emit(OpCodes.Ret);
            instanceType.Methods.Add(constructor);

            return (instanceField, instanceType);
        }

        private void RewriteType(TypeDefinition type, ObserverReferences observer)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                this.RewriteMethod(method, observer);
            }
        }

        /// <summary>
        /// Makes the <see cref="Observer"/> available to the given method as a variable and then
        /// applies all of the individual rewriters to the method.
        /// </summary>
        private void RewriteMethod(MethodDefinition methodDefinition, ObserverReferences observer)
        {
            if (!methodDefinition.HasBody || methodDefinition.Body.Instructions.Count == 0)
                return; // don't inject on method without a Body 

            // Inject observer instance to method.
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            var observerVariable = new VariableDefinition(observer.InstanceField.FieldType);
            il.Body.Variables.Add(observerVariable);
            Instruction start = methodDefinition.Body.Instructions[0];
            il.InsertBefore(start, il.Create(OpCodes.Ldsfld, observer.InstanceField));
            il.InsertBefore(start, il.CreateStlocBest(observerVariable));

            var context = new ObserverRewriterContext(observer, observerVariable);

            foreach (IObserverMethodRewriter rewriter in methodRewriters)
            {
                rewriter.Rewrite(methodDefinition, il, context);
            }
        }
    }
}