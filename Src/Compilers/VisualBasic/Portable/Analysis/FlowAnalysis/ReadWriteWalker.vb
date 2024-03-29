﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' A region analysis walker that records reads and writes of all variables, both inside and outside the region.
    ''' </summary>
    Friend Class ReadWriteWalker
        Inherits AbstractRegionDataFlowPass

        Friend Overloads Shared Sub Analyze(info As FlowAnalysisInfo, region As FlowAnalysisRegionInfo,
                                            ByRef readInside As ImmutableArray(Of ISymbol),
                                            ByRef writtenInside As ImmutableArray(Of ISymbol),
                                            ByRef readOutside As ImmutableArray(Of ISymbol),
                                            ByRef writtenOutside As ImmutableArray(Of ISymbol),
                                            ByRef captured As ImmutableArray(Of ISymbol))

            Dim walker = New ReadWriteWalker(info, region)
            Try
                If walker.Analyze() Then
                    readInside = walker.readInside.Cast(Of ISymbol).ToImmutableArray()
                    writtenInside = walker.writtenInside.Cast(Of ISymbol).ToImmutableArray()
                    readOutside = walker.readOutside.Cast(Of ISymbol).ToImmutableArray()
                    writtenOutside = walker.writtenOutside.Cast(Of ISymbol).ToImmutableArray()
                    captured = walker.captured.Cast(Of ISymbol).ToImmutableArray()
                Else
                    readInside = ImmutableArray(Of ISymbol).Empty
                    writtenInside = readInside
                    readOutside = readInside
                    writtenOutside = readInside
                    captured = readInside
                End If
            Finally
                walker.Free()
            End Try
        End Sub

        Private ReadOnly readInside As HashSet(Of Symbol) = New HashSet(Of Symbol)()
        Private ReadOnly writtenInside As HashSet(Of Symbol) = New HashSet(Of Symbol)()
        Private ReadOnly readOutside As HashSet(Of Symbol) = New HashSet(Of Symbol)()
        Private ReadOnly writtenOutside As HashSet(Of Symbol) = New HashSet(Of Symbol)()
        Private ReadOnly captured As HashSet(Of Symbol) = New HashSet(Of Symbol)()
        Private currentMethodOrLambda As Symbol

        Protected Overrides Sub NoteRead(variable As Symbol)
            If IsCompilerGeneratedTempLocal(variable) Then
                MyBase.NoteRead(variable)
            Else
                Select Case Me._regionPlace
                    Case RegionPlace.Before, RegionPlace.After
                        readOutside.Add(variable)
                    Case RegionPlace.Inside
                        readInside.Add(variable)
                    Case Else
                        Debug.Assert(False)
                End Select
                MyBase.NoteRead(variable)
                CheckCaptured(variable)
            End If
        End Sub

        Protected Overrides Sub NoteWrite(variable As Symbol, value As BoundExpression)
            If IsCompilerGeneratedTempLocal(variable) Then
                MyBase.NoteWrite(variable, value)
            Else
                Select Case Me._regionPlace
                    Case RegionPlace.Before, RegionPlace.After
                        writtenOutside.Add(variable)
                    Case RegionPlace.Inside
                        writtenInside.Add(variable)
                    Case Else
                        Debug.Assert(False)
                End Select
                MyBase.NoteWrite(variable, value)
                CheckCaptured(variable)
            End If
        End Sub

        Protected Overrides Sub NoteRead(fieldAccess As BoundFieldAccess)
            MyBase.NoteRead(fieldAccess)
            If (Me._regionPlace <> RegionPlace.Inside AndAlso fieldAccess.Syntax.Span.Contains(_region)) Then NoteReceiverRead(fieldAccess)
        End Sub

        Protected Overrides Sub NoteWrite(node As BoundExpression, value As BoundExpression)
            MyBase.NoteWrite(node, value)
            If node.Kind = BoundKind.FieldAccess Then NoteReceiverWritten(CType(node, BoundFieldAccess))
        End Sub

        Private Sub NoteReceiverRead(fieldAccess As BoundFieldAccess)
            NoteReceiverReadOrWritten(fieldAccess, Me.readInside)
        End Sub

        Private Sub NoteReceiverWritten(fieldAccess As BoundFieldAccess)
            NoteReceiverReadOrWritten(fieldAccess, Me.writtenInside)
        End Sub

        Private Sub NoteReceiverReadOrWritten(fieldAccess As BoundFieldAccess, readOrWritten As HashSet(Of Symbol))
            If fieldAccess.FieldSymbol.IsShared Then Return
            If fieldAccess.FieldSymbol.ContainingType.IsReferenceType Then Return
            Dim receiver = fieldAccess.ReceiverOpt
            If receiver Is Nothing Then Return
            Dim receiverSyntax = receiver.Syntax
            If receiverSyntax Is Nothing Then Return
            Select Case receiver.Kind
                Case BoundKind.Local
                    If _region.Contains(receiverSyntax.Span) Then readOrWritten.Add(CType(receiver, BoundLocal).LocalSymbol)
                Case BoundKind.MeReference
                    If _region.Contains(receiverSyntax.Span) Then readOrWritten.Add(Me.MeParameter)
                Case BoundKind.MyBaseReference
                    If _region.Contains(receiverSyntax.Span) Then readOrWritten.Add(Me.MeParameter)
                Case BoundKind.Parameter
                    If _region.Contains(receiverSyntax.Span) Then readOrWritten.Add(CType(receiver, BoundParameter).ParameterSymbol)
                Case BoundKind.RangeVariable
                    If _region.Contains(receiverSyntax.Span) Then readOrWritten.Add(CType(receiver, BoundRangeVariable).RangeVariable)
                Case BoundKind.FieldAccess
                    If receiver.Type.IsStructureType AndAlso receiverSyntax.Span.OverlapsWith(_region) Then NoteReceiverReadOrWritten(CType(receiver, BoundFieldAccess), readOrWritten)
            End Select
        End Sub

        Private Function IsCompilerGeneratedTempLocal(variable As Symbol) As Boolean
            Return TypeOf (variable) Is TempLocalSymbol
        End Function

        Private Sub CheckCaptured(variable As Symbol)
            ' Query range variables are read-only, even if they are captured at the end, they are 
            ' effectively captured ByValue, so IDE probably doesn't have to know about the capture
            ' at all.

            Select Case variable.Kind
                Case SymbolKind.Local
                    Dim local = DirectCast(variable, LocalSymbol)
                    If Not (local.IsConst Or Me.currentMethodOrLambda = local.ContainingSymbol) Then
                        Me.captured.Add(local)
                    End If
                Case SymbolKind.Parameter
                    Dim param = DirectCast(variable, ParameterSymbol)
                    If Not (Me.currentMethodOrLambda = param.ContainingSymbol) Then
                        Me.captured.Add(param)
                    End If
            End Select
        End Sub

        Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
            Dim previousMethod = Me.currentMethodOrLambda
            Me.currentMethodOrLambda = node.LambdaSymbol
            Dim result = MyBase.VisitLambda(node)
            Me.currentMethodOrLambda = previousMethod
            Return result
        End Function

        Public Overrides Function VisitQueryLambda(node As BoundQueryLambda) As BoundNode
            Dim previousMethod = Me.currentMethodOrLambda
            Me.currentMethodOrLambda = node.LambdaSymbol
            Dim result = MyBase.VisitQueryLambda(node)
            Me.currentMethodOrLambda = previousMethod
            Return result
        End Function

        Public Overrides Function VisitQueryableSource(node As BoundQueryableSource) As BoundNode
            If Not node.WasCompilerGenerated AndAlso node.RangeVariableOpt IsNot Nothing Then
                ' Adding range variables into a scope, note write for them.
                Debug.Assert(node.RangeVariables.Length = 1)
                NoteWrite(node.RangeVariableOpt, Nothing)
            End If

            Visit(node.Source)
            Return Nothing
        End Function

        Public Overrides Function VisitRangeVariableAssignment(node As BoundRangeVariableAssignment) As BoundNode
            If Not node.WasCompilerGenerated Then
                NoteWrite(node.RangeVariable, Nothing)
            End If

            Visit(node.Value)
            Return Nothing
        End Function

        Private Overloads Function Analyze() As Boolean
            Return Scan()
        End Function

        Private Sub New(info As FlowAnalysisInfo, region As FlowAnalysisRegionInfo)
            MyBase.New(info, region)
            currentMethodOrLambda = symbol
        End Sub

    End Class

End Namespace
