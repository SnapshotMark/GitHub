﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend Class LocalRewriter

        Public Overrides Function VisitQueryExpression(node As BoundQueryExpression) As BoundNode
            Return Visit(node.LastOperator)
        End Function

        Public Overrides Function VisitQueryClause(node As BoundQueryClause) As BoundNode
            Return Visit(node.UnderlyingExpression)
        End Function

        Public Overrides Function VisitOrdering(node As BoundOrdering) As BoundNode
            Return Visit(node.UnderlyingExpression)
        End Function

        Public Overrides Function VisitRangeVariableAssignment(node As BoundRangeVariableAssignment) As BoundNode
            Return Visit(node.Value)
        End Function

        Public Overrides Function VisitGroupAggregation(node As BoundGroupAggregation) As BoundNode
            Return Visit(node.Group)
        End Function

        Public Overrides Function VisitQueryLambda(node As BoundQueryLambda) As BoundNode
            ' query expression should be rewritten in the context of corresponding lambda.
            ' since everything in the expression will end up in the body of that lambda.
            ' Conveniently, we already know the lambda's symbol.

            ' BEGIN LAMBDA REWRITE
            Dim originalMethodOrLambda = Me.currentMethodOrLambda
            Me.currentMethodOrLambda = node.LambdaSymbol

            Dim nodeRangeVariables As ImmutableArray(Of RangeVariableSymbol) = node.RangeVariables

            If nodeRangeVariables.Length > 0 Then
                If rangeVariableMap Is Nothing Then
                    rangeVariableMap = New Dictionary(Of RangeVariableSymbol, BoundExpression)()
                End If

                Dim firstUnmappedRangeVariable As Integer = 0

                For Each parameter As ParameterSymbol In node.LambdaSymbol.Parameters
                    Dim parameterName As String = parameter.Name
                    Dim isReservedName As Boolean = parameterName.StartsWith("$"c, StringComparison.Ordinal)

                    If isReservedName AndAlso String.Equals(parameterName, StringConstants.ItAnonymous, StringComparison.Ordinal) Then
                        ' This parameter represents "nameless" range variable, there are no references to it.
                        Continue For
                    End If

                    Dim paramRef As New BoundParameter(node.Syntax,
                                                       parameter,
                                                       False,
                                                       parameter.Type)

                    If isReservedName AndAlso Not String.Equals(parameterName, StringConstants.Group, StringComparison.Ordinal) Then
                        ' Compound variable.
                        ' Each range variable is an Anonymous Type property.
                        Debug.Assert(parameterName.Equals(StringConstants.It) OrElse parameterName.Equals(StringConstants.It1) OrElse parameterName.Equals(StringConstants.It2))
                        PopulateRangeVariableMapForAnonymousType(node.Syntax, paramRef, nodeRangeVariables, firstUnmappedRangeVariable)
                    Else
                        ' Simple case, range variable is a lambda parameter.
                        Debug.Assert(IdentifierComparison.Equals(parameterName, nodeRangeVariables(firstUnmappedRangeVariable).Name))
                        rangeVariableMap.Add(nodeRangeVariables(firstUnmappedRangeVariable), paramRef)
                        firstUnmappedRangeVariable += 1
                    End If
                Next

                Debug.Assert(firstUnmappedRangeVariable = nodeRangeVariables.Length)
            End If

            Dim save_createSequencePointsForTopLevelNonCompilerGeneratedExpressions = createSequencePointsForTopLevelNonCompilerGeneratedExpressions
            Dim createSequencePoint As VisualBasicSyntaxNode = Nothing
            Dim sequencePointSpan As TextSpan

#If Not DEBUG Then
            If GenerateDebugInfo Then
#End If
                If node.Syntax.Kind = SyntaxKind.AggregateClause Then
                    Dim aggregateClause = DirectCast(node.Syntax, AggregateClauseSyntax)

                    If aggregateClause.AggregationVariables.Count = 1 Then
                        ' We are dealing with a simple case of an Aggregate clause - a single aggregate
                        ' function in the Into clause. This lambda is responsible for calculating that
                        ' aggregate function. Actually, it includes all code generated for the entire
                        ' Aggregate clause. We should create sequence point for the entire clause
                        ' rather than sequence points for the top level expressions within the lambda.
                        createSequencePoint = aggregateClause
                        sequencePointSpan = aggregateClause.Span
                    Else
                        ' We are dealing with a complex case of an Aggregate clause - two or more aggregate
                        ' functions in the Into clause. There will be two lambdas assosiated with an Aggregate
                        ' clause like this: 
                        '     - one that calculates and caches the group;
                        '     - and the other that calculates aggregate functions.
                        ' If we are dealing with the first kind of lambda, we should create sequence point 
                        ' that spans from begining of the Aggregate clause to the begining of the Into clause
                        ' because all that code is involved into group calculation.
                        Dim haveAggregation As Boolean = False

                        If node.Expression.Kind = BoundKind.AnonymousTypeCreationExpression Then
                            For Each n In DirectCast(node.Expression, BoundAnonymousTypeCreationExpression).Arguments
                                If n.Syntax.Kind = SyntaxKind.AggregationRangeVariable Then
                                    haveAggregation = True
                                    Exit For
                                End If
                            Next
                        End If

                        If Not haveAggregation Then
                            createSequencePoint = aggregateClause
                            If aggregateClause.AdditionalQueryOperators.Count = 0 Then
                                sequencePointSpan = TextSpan.FromBounds(aggregateClause.SpanStart,
                                                                            aggregateClause.Variables.Last.Span.End)
                            Else
                                sequencePointSpan = TextSpan.FromBounds(aggregateClause.SpanStart,
                                                                            aggregateClause.AdditionalQueryOperators.Last.Span.End)
                            End If
                        End If
                    End If
                End If

                createSequencePointsForTopLevelNonCompilerGeneratedExpressions = (createSequencePoint Is Nothing)
#If Not DEBUG Then
            End If
#End If

            Dim returnstmt As BoundStatement = New BoundReturnStatement(node.Syntax,
                                                                        VisitExpressionNode(node.Expression),
                                                                        Nothing,
                                                                        Nothing)

            If createSequencePoint IsNot Nothing AndAlso GenerateDebugInfo Then
                returnstmt = New BoundSequencePointWithSpan(createSequencePoint, returnstmt, sequencePointSpan)
            End If

            createSequencePointsForTopLevelNonCompilerGeneratedExpressions = save_createSequencePointsForTopLevelNonCompilerGeneratedExpressions

            For Each rangeVar As RangeVariableSymbol In nodeRangeVariables
                rangeVariableMap.Remove(rangeVar)
            Next

            Dim lambdaBody = New BoundBlock(node.Syntax,
                                            Nothing,
                                            ImmutableArray(Of LocalSymbol).Empty,
                                            ImmutableArray.Create(returnstmt))

            Me.hasLambdas = True

            Dim result As BoundLambda = New BoundLambda(node.Syntax,
                                   node.LambdaSymbol,
                                   lambdaBody,
                                   ImmutableArray(Of Diagnostic).Empty,
                                   Nothing,
                                   ConversionKind.DelegateRelaxationLevelNone,
                                   MethodConversionKind.Identity)

            result.MakeCompilerGenerated()

            ' Done with lambda body rewrite, restore current lambda.
            ' END LAMBDA REWRITE
            Me.currentMethodOrLambda = originalMethodOrLambda

            Return result
        End Function

        Private Sub PopulateRangeVariableMapForAnonymousType(
            syntax As VisualBasicSyntaxNode,
            anonymousTypeInstance As BoundExpression,
            rangeVariables As ImmutableArray(Of RangeVariableSymbol),
            ByRef firstUnmappedRangeVariable As Integer
        )
            Dim anonymousType = DirectCast(anonymousTypeInstance.Type, AnonymousTypeManager.AnonymousTypePublicSymbol)

            For Each propertyDef As PropertySymbol In anonymousType.Properties
                Dim getCallOrPropertyAccess As BoundExpression = Nothing
                If inExpressionLambda Then
                    ' NOTE: If we are in context of a lambda to be converted to an expression tree we need to use PropertyAccess.
                    getCallOrPropertyAccess = New BoundPropertyAccess(syntax,
                                                                      propertyDef,
                                                                      Nothing,
                                                                      PropertyAccessKind.Get,
                                                                      anonymousTypeInstance,
                                                                      ImmutableArray(Of BoundExpression).Empty,
                                                                      propertyDef.Type)
                Else
                    Dim getter = propertyDef.GetMethod
                    getCallOrPropertyAccess = New BoundCall(syntax,
                                                            getter,
                                                            Nothing,
                                                            anonymousTypeInstance,
                                                            ImmutableArray(Of BoundExpression).Empty,
                                                            Nothing,
                                                            getter.ReturnType)
                End If

                Dim propertyDefName As String = propertyDef.Name

                If propertyDefName.StartsWith("$"c, StringComparison.Ordinal) AndAlso Not String.Equals(propertyDefName, StringConstants.Group, StringComparison.Ordinal) Then
                    ' Nested compound variable.
                    Debug.Assert(propertyDefName.Equals(StringConstants.It) OrElse propertyDefName.Equals(StringConstants.It1) OrElse propertyDefName.Equals(StringConstants.It2))
                    PopulateRangeVariableMapForAnonymousType(syntax, getCallOrPropertyAccess, rangeVariables, firstUnmappedRangeVariable)

                Else
                    Debug.Assert(IdentifierComparison.Equals(propertyDefName, rangeVariables(firstUnmappedRangeVariable).Name))
                    rangeVariableMap.Add(rangeVariables(firstUnmappedRangeVariable), getCallOrPropertyAccess)
                    firstUnmappedRangeVariable += 1
                End If
            Next
        End Sub

        Public Overrides Function VisitRangeVariable(node As BoundRangeVariable) As BoundNode
            Return rangeVariableMap(node.RangeVariable)
        End Function

        Public Overrides Function VisitQueryableSource(node As BoundQueryableSource) As BoundNode
            Return Visit(node.Source)
        End Function

        Public Overrides Function VisitQuerySource(node As BoundQuerySource) As BoundNode
            Return Visit(node.Expression)
        End Function

        Public Overrides Function VisitToQueryableCollectionConversion(node As BoundToQueryableCollectionConversion) As BoundNode
            Return Visit(node.ConversionCall)
        End Function

        Public Overrides Function VisitAggregateClause(node As BoundAggregateClause) As BoundNode
            If node.CapturedGroupOpt IsNot Nothing Then
                Debug.Assert(node.GroupPlaceholderOpt IsNot Nothing)
                Dim groupLocal = New TempLocalSymbol(Me.currentMethodOrLambda, node.CapturedGroupOpt.Type)

                AddPlaceholderReplacement(node.GroupPlaceholderOpt,
                                              New BoundLocal(node.Syntax, groupLocal, False, groupLocal.Type))

                Dim result = New BoundSequence(node.Syntax,
                                                               ImmutableArray.Create(Of LocalSymbol)(groupLocal),
                                                               ImmutableArray.Create(Of BoundExpression)(
                                                                   New BoundAssignmentOperator(node.Syntax,
                                                                                               New BoundLocal(node.Syntax, groupLocal, True, groupLocal.Type),
                                                                                               VisitExpressionNode(node.CapturedGroupOpt),
                                                                                               True,
                                                                                               groupLocal.Type)),
                                                                VisitExpressionNode(node.UnderlyingExpression),
                                                                node.Type)

                RemovePlaceholderReplacement(node.GroupPlaceholderOpt)

                Return result
            End If

            Return Visit(node.UnderlyingExpression)
        End Function
    End Class

End Namespace
