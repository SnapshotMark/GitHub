﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    partial class IteratorRewriter : StateMachineRewriter
    {
        /// <summary>
        /// Rewrite an iterator method into a state machine class.
        /// </summary>
        /// <param name="body">The original body of the method</param>
        /// <param name="method">The method's identity</param>
        /// <param name="compilationState">The collection of generated methods that result from this transformation and which must be emitted</param>
        /// <param name="diagnostics">Diagnostic bag for diagnostics.</param>
        /// <param name="generateDebugInfo"></param>
        /// <param name="stateMachineType"></param>
        internal static BoundStatement Rewrite(
            BoundStatement body,
            MethodSymbol method,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            bool generateDebugInfo,
            out IteratorStateMachine stateMachineType)
        {
            TypeSymbol elementType = method.IteratorElementType;
            if ((object)elementType == null)
            {
                stateMachineType = null;
                return body;
            }

            // Figure out what kind of iterator we are generating.
            bool isEnumerable;
            switch (method.ReturnType.OriginalDefinition.SpecialType)
            {
                case SpecialType.System_Collections_IEnumerable:
                case SpecialType.System_Collections_Generic_IEnumerable_T:
                    isEnumerable = true;
                    break;

                case SpecialType.System_Collections_IEnumerator:
                case SpecialType.System_Collections_Generic_IEnumerator_T:
                    isEnumerable = false;
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(method.ReturnType.OriginalDefinition.SpecialType);
            }

            stateMachineType = new IteratorStateMachine(method, isEnumerable, elementType, compilationState);
            compilationState.ModuleBuilderOpt.CompilationState.SetStateMachineType(method, stateMachineType);
            return new IteratorRewriter(body, method, isEnumerable, stateMachineType, compilationState, diagnostics, generateDebugInfo).Rewrite();
        }

        private readonly TypeSymbol elementType;

        // true if the iterator implements IEnumerable and IEnumerable<T>,
        // false if it implements IEnumerator and IEnumerator<T>
        private readonly bool isEnumerable;

        private FieldSymbol currentField;
        private FieldSymbol initialThreadIdField;

        private IteratorRewriter(
            BoundStatement body,
            MethodSymbol method,
            bool isEnumerable,
            IteratorStateMachine iteratorClass,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            bool generateDebugInfo)
            : base(body, method, iteratorClass, compilationState, diagnostics, generateDebugInfo)
        {
            // the element type may contain method type parameters, which are now alpha-renamed into type parameters of the generated class
            this.elementType = iteratorClass.ElementType;

            this.isEnumerable = isEnumerable;
        }

        protected override bool PreserveInitialParameterValues
        {
            get { return isEnumerable; }
        }

        protected override void GenerateControlFields()
        {
            base.GenerateControlFields();
            
            // Add a field: T current
            currentField = F.StateMachineField(elementType, GeneratedNames.MakeIteratorCurrentBackingFieldName(), isPublic: false);

            // if it is an iterable, add a field: int initialThreadId
            var threadType = F.Compilation.GetWellKnownType(WellKnownType.System_Threading_Thread);
            initialThreadIdField = isEnumerable && (object)threadType != null && !threadType.IsErrorType()
                ? F.StateMachineField(F.SpecialType(SpecialType.System_Int32), GeneratedNames.MakeIteratorCurrentThreadIdName(), isPublic: false)
                : null;
        }

        protected override void GenerateMethodImplementations()
        {
            try
            {
                BoundExpression managedThreadId = null; // Thread.CurrentThread.ManagedThreadId

                GenerateEnumeratorImplementation();

                if (isEnumerable)
                {
                    GenerateEnumerableImplementation(ref managedThreadId);
                }

                GenerateConstructor(managedThreadId);
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                diagnostics.Add(ex.Diagnostic);
            }
        }

        private void GenerateEnumeratorImplementation()
        {
            var IDisposable_Dispose = F.SpecialMethod(SpecialMember.System_IDisposable__Dispose);

            var IEnumerator_MoveNext = F.SpecialMethod(SpecialMember.System_Collections_IEnumerator__MoveNext);
            var IEnumerator_Reset = F.SpecialMethod(SpecialMember.System_Collections_IEnumerator__Reset);
            var IEnumerator_get_Current = F.SpecialProperty(SpecialMember.System_Collections_IEnumerator__Current).GetMethod;

            var IEnumeratorOfElementType = F.SpecialType(SpecialType.System_Collections_Generic_IEnumerator_T).Construct(elementType);
            var IEnumeratorOfElementType_get_Current = F.SpecialProperty(SpecialMember.System_Collections_Generic_IEnumerator_T__Current).GetMethod.AsMember(IEnumeratorOfElementType);

            // Add bool IEnumerator.MoveNext() and void IDisposable.Dispose()
            {
                var disposeMethod = OpenMethodImplementation(IDisposable_Dispose, debuggerHidden: true, generateDebugInfo: false, hasMethodBodyDependency: true);
                var moveNextMethod = OpenMethodImplementation(IEnumerator_MoveNext, methodName: WellKnownMemberNames.MoveNextMethodName, hasMethodBodyDependency: true, debuggerHidden: IsDebuggerHidden(this.method));
                GenerateMoveNextAndDispose(moveNextMethod, disposeMethod);
            }

            // Add T IEnumerator<T>.Current
            {
                OpenPropertyImplementation(IEnumeratorOfElementType_get_Current, debuggerHidden: true, hasMethodBodyDependency: false);
                F.CloseMethod(F.Return(F.Field(F.This(), currentField)));
            }

            // Add void IEnumerator.Reset()
            {
                OpenMethodImplementation(IEnumerator_Reset, debuggerHidden: true, generateDebugInfo: false, hasMethodBodyDependency: false);
                F.CloseMethod(F.Throw(F.New(F.WellKnownType(WellKnownType.System_NotSupportedException))));
            }

            // Add object IEnumerator.Current
            {
                OpenPropertyImplementation(IEnumerator_get_Current, debuggerHidden: true, hasMethodBodyDependency: false);
                F.CloseMethod(F.Return(F.Field(F.This(), currentField)));
            }
        }

        private void GenerateEnumerableImplementation(ref BoundExpression managedThreadId)
        {
            var IEnumerable_GetEnumerator = F.SpecialMethod(SpecialMember.System_Collections_IEnumerable__GetEnumerator);

            var IEnumerableOfElementType = F.SpecialType(SpecialType.System_Collections_Generic_IEnumerable_T).Construct(elementType);
            var IEnumerableOfElementType_GetEnumerator = F.SpecialMethod(SpecialMember.System_Collections_Generic_IEnumerable_T__GetEnumerator).AsMember(IEnumerableOfElementType);

            // generate the code for GetEnumerator()
            // .NET Core has removed the Thread class. We can the managed thread id by making a call to 
            // Environment.CurrentManagedThreadId. If that method is not present (pre 4.5) fall back to the old behavior.
            //    IEnumerable<elementType> result;
            //    if (this.initialThreadId == Thread.CurrentThread.ManagedThreadId && this.state == -2)
            //    {
            //        this.state = 0;
            //        result = this;
            //    }
            //    else
            //    {
            //        result = new Ints0_Impl(0);
            //    }
            //    result.parameter = this.parameterProxy; // copy all of the parameter proxies

            // Add IEnumerator<elementType> IEnumerable<elementType>.GetEnumerator()
             
            // The implementation doesn't depend on the method body of the iterator method.
            // Only on it's parameters and staticness.
            var getEnumeratorGeneric = OpenMethodImplementation(IEnumerableOfElementType_GetEnumerator, debuggerHidden: true, generateDebugInfo: false, hasMethodBodyDependency: false);

            var bodyBuilder = ArrayBuilder<BoundStatement>.GetInstance();
            var resultVariable = F.SynthesizedLocal(stateMachineClass, null);      // iteratorClass result;
            BoundStatement makeIterator = F.Assignment(F.Local(resultVariable), F.New(stateMachineClass.Constructor, F.Literal(0))); // result = new IteratorClass(0)

            var thisInitialized = F.GenerateLabel("thisInitialized");

            if ((object)initialThreadIdField != null)
            {
                MethodSymbol currentManagedThreadIdMethod = null;

                PropertySymbol currentManagedThreadIdProperty = F.WellKnownMember(WellKnownMember.System_Environment__CurrentManagedThreadId, isOptional: true) as PropertySymbol;

                if ((object)currentManagedThreadIdProperty != null)
                {
                    currentManagedThreadIdMethod = currentManagedThreadIdProperty.GetMethod;
                }

                if ((object)currentManagedThreadIdMethod != null)
                {
                    managedThreadId = F.Call(null, currentManagedThreadIdMethod);
                }
                else
                {
                    managedThreadId = F.Property(F.Property(WellKnownMember.System_Threading_Thread__CurrentThread), WellKnownMember.System_Threading_Thread__ManagedThreadId);
                }

                makeIterator = F.If(
                    condition: F.LogicalAnd(                                   // if (this.state == -2 && this.initialThreadId == Thread.CurrentThread.ManagedThreadId)
                            F.IntEqual(F.Field(F.This(), stateField), F.Literal(StateMachineStates.FinishedStateMachine)),
                        F.IntEqual(F.Field(F.This(), initialThreadIdField), managedThreadId)),
                    thenClause: F.Block(                                       // then
                            F.Assignment(F.Field(F.This(), stateField), F.Literal(StateMachineStates.FirstUnusedState)),  // this.state = 0;
                            F.Assignment(F.Local(resultVariable), F.This()),       // result = this;
                            method.IsStatic || method.ThisParameter.Type.IsReferenceType ?   // if this is a reference type, no need to copy it since it is not assignable
                                F.Goto(thisInitialized) :                          // goto thisInitialized
                                (BoundStatement)F.Block()),
                    elseClauseOpt:
                        makeIterator // else result = new IteratorClass(0)
                        );
            }

            bodyBuilder.Add(makeIterator);

            // Initialize all the parameter copies
            var copySrc = initialParameters;
            var copyDest = variableProxies;
            if (!method.IsStatic)
            {
                // starting with "this"
                CapturedSymbolReplacement proxy;
                if (copyDest.TryGetValue(method.ThisParameter, out proxy))
                {
                    bodyBuilder.Add(
                        F.Assignment(
                            proxy.Replacement(F.Syntax, stateMachineType => F.Local(resultVariable)),
                            copySrc[method.ThisParameter].Replacement(F.Syntax, stateMachineType => F.This())));
                }
            }

            bodyBuilder.Add(F.Label(thisInitialized));

            foreach (var parameter in method.Parameters)
            {
                CapturedSymbolReplacement proxy;
                if (copyDest.TryGetValue(parameter, out proxy))
                {
                    bodyBuilder.Add(
                        F.Assignment(
                            proxy.Replacement(F.Syntax, stateMachineType => F.Local(resultVariable)),
                            copySrc[parameter].Replacement(F.Syntax, stateMachineType => F.This())));
                }
            }

            bodyBuilder.Add(F.Return(F.Local(resultVariable)));
            F.CloseMethod(F.Block(ImmutableArray.Create(resultVariable), bodyBuilder.ToImmutableAndFree()));

            // Generate IEnumerable.GetEnumerator
            var getEnumerator = OpenMethodImplementation(IEnumerable_GetEnumerator, debuggerHidden: true, generateDebugInfo: false);
            F.CloseMethod(F.Return(F.Call(F.This(), getEnumeratorGeneric)));
        }

        private void GenerateConstructor(BoundExpression managedThreadId)
        {
            F.CurrentMethod = stateMachineClass.Constructor;
            var bodyBuilder = ArrayBuilder<BoundStatement>.GetInstance();
            bodyBuilder.Add(F.BaseInitialization());
            bodyBuilder.Add(F.Assignment(F.Field(F.This(), stateField), F.Parameter(F.CurrentMethod.Parameters[0]))); // this.state = state;

            if (managedThreadId != null)
            {
                // this.initialThreadId = Thread.CurrentThread.ManagedThreadId;
                bodyBuilder.Add(F.Assignment(F.Field(F.This(), initialThreadIdField), managedThreadId));
            }

            bodyBuilder.Add(F.Return());
            F.CloseMethod(F.Block(bodyBuilder.ToImmutableAndFree()));
            bodyBuilder = null;
        }

        protected override bool IsStateFieldPublic
        {
            get { return false; }
        }

        protected override void InitializeStateMachine(ArrayBuilder<BoundStatement> bodyBuilder, NamedTypeSymbol frameType, LocalSymbol stateMachineLocal)
        {
            // var stateMachineLocal = new IteratorImplementationClass(N)
            // where N is either 0 (if we're producing an enumerator) or -2 (if we're producing an enumerable)
            int initialState = isEnumerable ? StateMachineStates.FinishedStateMachine : StateMachineStates.FirstUnusedState;
            bodyBuilder.Add(
                F.Assignment(
                    F.Local(stateMachineLocal),
                    F.New(stateMachineClass.Constructor.AsMember(frameType), F.Literal(initialState))));
        }

        protected override BoundStatement GenerateReplacementBody(LocalSymbol stateMachineVariable, NamedTypeSymbol frameType)
        {
            return F.Return(F.Local(stateMachineVariable));
        }

        private void GenerateMoveNextAndDispose(
            SynthesizedImplementationMethod moveNextMethod,
            SynthesizedImplementationMethod disposeMethod)
        {
            var rewriter = new IteratorMethodToStateMachineRewriter(
                F,
                method,
                stateField,
                currentField,
                variablesCaptured,
                variableProxies,
                diagnostics,
                generateDebugInfo);

            rewriter.GenerateMoveNextAndDispose(body, moveNextMethod, disposeMethod);
        }
    }
}
