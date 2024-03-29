﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

'-----------------------------------------------------------------------------------------------------------
'
'  Extension methods to st leading/trailing trivia in syntax nodes.
'-----------------------------------------------------------------------------------------------------------

Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

    Friend Module SyntaxExtensions

        <Extension()>
        Public Function WithAnnotations(Of TNode As VisualBasicSyntaxNode)(node As TNode, ParamArray annotations() As SyntaxAnnotation) As TNode
            If annotations Is Nothing Then Throw New ArgumentNullException("annotations")
            Return CType(node.SetAnnotations(annotations), TNode)
        End Function

        <Extension()>
        Public Function WithAdditionalAnnotations(Of TNode As VisualBasicSyntaxNode)(node As TNode, ParamArray annotations() As SyntaxAnnotation) As TNode
            If annotations Is Nothing Then Throw New ArgumentNullException("annotations")
            Return CType(node.SetAnnotations(node.GetAnnotations().Concat(annotations).ToArray()), TNode)
        End Function

        <Extension()>
        Public Function WithoutAnnotations(Of TNode As VisualBasicSyntaxNode)(node As TNode, ParamArray removalAnnotations() As SyntaxAnnotation) As TNode
            Dim newAnnotations = ArrayBuilder(Of SyntaxAnnotation).GetInstance()
            Dim annotations = node.GetAnnotations()
            For Each candidate In annotations
                If Array.IndexOf(removalAnnotations, candidate) < 0 Then
                    newAnnotations.Add(candidate)
                End If
            Next
            Return CType(node.SetAnnotations(newAnnotations.ToArrayAndFree()), TNode)
        End Function

        <Extension()>
        Public Function WithAdditionalDiagnostics(Of TNode As VisualBasicSyntaxNode)(node As TNode, ParamArray diagnostics As DiagnosticInfo()) As TNode
            Dim current As DiagnosticInfo() = node.GetDiagnostics
            If current IsNot Nothing Then
                Return DirectCast(node.SetDiagnostics(current.Concat(diagnostics).ToArray()), TNode)
            Else
                Return node.WithDiagnostics(diagnostics)
            End If
        End Function

        <Extension()>
        Public Function WithDiagnostics(Of TNode As VisualBasicSyntaxNode)(node As TNode, ParamArray diagnostics As DiagnosticInfo()) As TNode
            Return DirectCast(node.SetDiagnostics(diagnostics), TNode)
        End Function

        <Extension()>
        Public Function WithoutDiagnostics(Of TNode As VisualBasicSyntaxNode)(node As TNode) As TNode
            Dim current As DiagnosticInfo() = node.GetDiagnostics
            If ((current Is Nothing) OrElse (current.Length = 0)) Then
                Return node
            End If
            Return DirectCast(node.SetDiagnostics(Nothing), TNode)
        End Function

        <Extension()>
        Public Function LastTriviaIfAny(node As VisualBasicSyntaxNode) As VisualBasicSyntaxNode
            Dim trailingTriviaNode = node.GetTrailingTrivia()
            If trailingTriviaNode Is Nothing Then
                Return Nothing
            End If
            Return New SyntaxList(Of VisualBasicSyntaxNode)(trailingTriviaNode).Last
        End Function

        <Extension()>
        Public Function EndsWithEndOfLineOrColonTrivia(node As VisualBasicSyntaxNode) As Boolean
            Dim trailingTrivia = node.LastTriviaIfAny()
            Return trailingTrivia IsNot Nothing AndAlso
                (trailingTrivia.Kind = SyntaxKind.EndOfLineTrivia OrElse trailingTrivia.Kind = SyntaxKind.ColonTrivia)
        End Function
    End Module

    Friend Module SyntaxNodeExtensions

        Private Function IsMissingToken(token As SyntaxToken) As Boolean
            Return token.Width = 0 AndAlso token.Kind <> SyntaxKind.EmptyToken
        End Function

#Region "AddLeading"
        ' Add "trivia" as a leading trivia of node. If node is not a token, traverses down to the tree to add it it to the first token.
        <Extension()>
        Private Function AddLeadingTrivia(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, trivia As SyntaxList(Of VisualBasicSyntaxNode)) As TSyntax
            If node Is Nothing Then
                Throw New ArgumentNullException("node")
            End If

            If Not trivia.Any Then
                Return node
            End If

            Dim tk = TryCast(node, SyntaxToken)
            Dim result As TSyntax
            If tk IsNot Nothing Then
                ' Cannot add unexpected tokens as leading trivia on a missing token since
                ' if the unexpected tokens end with a statement terminator, the missing
                ' token would follow the statement terminator. That would result in an
                ' incorrect syntax tree and if this missing token is the end of an expression,
                ' and the expression represents a transition between VB and XML, the
                ' terminator will be overlooked (see ParseXmlEmbedded for instance).
                If IsMissingToken(tk) Then
                    Dim leadingTrivia = trivia.GetStartOfTrivia()
                    Dim trailingTrivia = trivia.GetEndOfTrivia()
                    tk = SyntaxToken.AddLeadingTrivia(tk, leadingTrivia).AddTrailingTrivia(trailingTrivia)
                Else
                    tk = SyntaxToken.AddLeadingTrivia(tk, trivia)
                End If

                result = DirectCast(CObj(tk), TSyntax)
            Else
                result = FirstTokenReplacer.Replace(node, Function(t) SyntaxToken.AddLeadingTrivia(t, trivia))
            End If

            'Debug.Assert(result.hasDiagnostics)

            Return result
        End Function

        ' Add "unexpected" as skipped leading trivia to "node". Leaves any diagnostics in place, and also adds a diagnostic with code "errorId"
        ' to the first token in the list.
        <Extension()>
        Friend Function AddLeadingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxList(Of SyntaxToken), errorId As ERRID) As TSyntax
            Dim diagnostic = ErrorFactory.ErrorInfo(errorId)
            If unexpected.Node IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected.Node,
                                                                              preserveDiagnostics:=True,
                                                                              addDiagnosticToFirstTokenOnly:=True,
                                                                              addDiagnostic:=diagnostic)
                Return AddLeadingTrivia(node, trivia)
            Else
                Return DirectCast(node.AddError(diagnostic), TSyntax)
            End If
            Return node
        End Function

        ' Add "unexpected" as skipped leading trivia of "node". Removes all diagnostics from "unexpected", replacing them with 
        ' a new diagnostic with the given "errorId".
        <Extension()>
        Friend Function AddLeadingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxToken, errorId As ERRID) As TSyntax
            Return node.AddLeadingSyntax(DirectCast(unexpected, VisualBasicSyntaxNode), errorId)
        End Function

        ' Add "unexpected" as skipped leading trivia of "node". Leaves any diagnostics in place (possibly reattaching them to the created trivia node).
        <Extension()>
        Friend Function AddLeadingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxList(Of SyntaxToken)) As TSyntax
            Return node.AddLeadingSyntax(unexpected.Node)
        End Function

        ' Add "unexpected" as skipped leading trivia of "node". Removes all diagnostics from "unexpected", replacing them with 
        ' a new diagnostic with the given "errorId".
        <Extension()>
        Friend Function AddLeadingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As VisualBasicSyntaxNode, errorId As ERRID) As TSyntax
            Dim diagnostic = ErrorFactory.ErrorInfo(errorId)
            If unexpected IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected,
                                                                              preserveDiagnostics:=False,
                                                                              addDiagnosticToFirstTokenOnly:=False,
                                                                              addDiagnostic:=diagnostic)
                Return AddLeadingTrivia(node, trivia)
            Else
                Return DirectCast(node.AddError(diagnostic), TSyntax)
            End If
            Return node
        End Function

        ' Add "unexpected" as skipped leading trivia of "node". Leaves any diagnostics in place (possibly reattaching them to the created trivia node).
        <Extension()>
        Friend Function AddLeadingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As VisualBasicSyntaxNode) As TSyntax
            If unexpected IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected,
                                                                              preserveDiagnostics:=True,
                                                                              addDiagnosticToFirstTokenOnly:=False,
                                                                              addDiagnostic:=Nothing)
                Return AddLeadingTrivia(node, trivia)
            End If
            Return node
        End Function

#End Region

#Region "AddTrailing"
        ' Add "trivia" as a trailing trivia of node. If node is not a token, traverses down to the tree to add it it to the last token.
        <Extension()>
        Friend Function AddTrailingTrivia(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, trivia As SyntaxList(Of VisualBasicSyntaxNode)) As TSyntax
            If node Is Nothing Then
                Throw New ArgumentNullException("node")
            End If

            Dim tk = TryCast(node, SyntaxToken)
            Dim result As TSyntax
            If tk IsNot Nothing Then
                result = DirectCast(CObj(SyntaxToken.AddTrailingTrivia(tk, trivia)), TSyntax)
            Else
                result = LastTokenReplacer.Replace(node, Function(t) SyntaxToken.AddTrailingTrivia(t, trivia))
            End If

            'Debug.Assert(result.ContainsDiagnostics)
            Return result
        End Function

        ' Add "unexpected" as skipped trailing trivia to "node". Leaves any diagnostics in place, and also adds a diagnostic with code "errorId"
        ' to the first token in the list.
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxList(Of SyntaxToken), errorId As ERRID) As TSyntax
            Dim diagnostic = ErrorFactory.ErrorInfo(errorId)
            If unexpected.Node IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected.Node,
                                                                                  preserveDiagnostics:=True,
                                                                                  addDiagnosticToFirstTokenOnly:=True,
                                                                                  addDiagnostic:=diagnostic)
                Return AddTrailingTrivia(node, trivia)
            Else
                Return DirectCast(node.AddError(diagnostic), TSyntax)
            End If
            Return node
        End Function

        ' Add "unexpected" as skipped trailing trivia of "node". Removes all diagnostics from "unexpected", replacing them with 
        ' a new diagnostic with the given "errorId".
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxToken, errorId As ERRID) As TSyntax
            Return node.AddTrailingSyntax(DirectCast(unexpected, VisualBasicSyntaxNode), errorId)
        End Function

        ' Add "unexpected" as skipped trailing trivia of "node". Leaves any diagnostics in place (possibly reattaching them to the created trivia node).
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxList(Of SyntaxToken)) As TSyntax
            Return node.AddTrailingSyntax(unexpected.Node)
        End Function

        ' Add "unexpected" as skipped trailing trivia of "node". Leaves any diagnostics in place (possibly reattaching them to the created trivia node).
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As SyntaxToken) As TSyntax
            Return node.AddTrailingSyntax(DirectCast(unexpected, VisualBasicSyntaxNode))
        End Function

        ' Add "unexpected" as skipped trailing trivia of "node". Removes all diagnostics from "unexpected", replacing them with 
        ' a new diagnostic with the given "errorId".
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As VisualBasicSyntaxNode, errorId As ERRID) As TSyntax
            Dim diagnostic = ErrorFactory.ErrorInfo(errorId)
            If unexpected IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected,
                                                               preserveDiagnostics:=False,
                                                               addDiagnosticToFirstTokenOnly:=False,
                                                               addDiagnostic:=diagnostic)
                Return AddTrailingTrivia(node, trivia)
            Else
                Return DirectCast(node.AddError(diagnostic), TSyntax)
            End If
            Return node
        End Function

        ' Add "unexpected" as skipped trailing trivia of "node". Leaves any diagnostics in place (possibly reattaching them to the created trivia node).
        <Extension()>
        Friend Function AddTrailingSyntax(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, unexpected As VisualBasicSyntaxNode) As TSyntax
            If unexpected IsNot Nothing Then
                Dim trivia As SyntaxList(Of VisualBasicSyntaxNode) = CreateSkippedTrivia(unexpected, preserveDiagnostics:=True, addDiagnosticToFirstTokenOnly:=False, addDiagnostic:=Nothing)
                Return AddTrailingTrivia(node, trivia)
            End If
            Return node
        End Function

        <Extension()>
        Friend Function AddError(Of TSyntax As VisualBasicSyntaxNode)(node As TSyntax, errorId As ERRID) As TSyntax
            Return DirectCast(node.AddError(ErrorFactory.ErrorInfo(errorId)), TSyntax)
        End Function

#End Region

        <Extension()>
        Friend Function GetStartOfTrivia(trivia As SyntaxList(Of VisualBasicSyntaxNode)) As SyntaxList(Of VisualBasicSyntaxNode)
            Return trivia.GetStartOfTrivia(trivia.GetIndexOfEndOfTrivia())
        End Function

        <Extension()>
        Friend Function GetStartOfTrivia(trivia As SyntaxList(Of VisualBasicSyntaxNode), indexOfEnd As Integer) As SyntaxList(Of VisualBasicSyntaxNode)
            If indexOfEnd = 0 Then
                Return Nothing
            ElseIf indexOfEnd = trivia.Count Then
                Return trivia
            Else
                Dim builder = SyntaxListBuilder(Of VisualBasicSyntaxNode).Create()
                For i = 0 To indexOfEnd - 1
                    builder.Add(trivia(i))
                Next
                Return builder.ToList()
            End If
        End Function

        <Extension()>
        Friend Function GetEndOfTrivia(trivia As SyntaxList(Of VisualBasicSyntaxNode)) As SyntaxList(Of VisualBasicSyntaxNode)
            Return trivia.GetEndOfTrivia(trivia.GetIndexOfEndOfTrivia())
        End Function

        <Extension()>
        Friend Function GetEndOfTrivia(trivia As SyntaxList(Of VisualBasicSyntaxNode), indexOfEnd As Integer) As SyntaxList(Of VisualBasicSyntaxNode)
            If indexOfEnd = 0 Then
                Return trivia
            ElseIf indexOfEnd = trivia.Count Then
                Return Nothing
            Else
                Dim builder = SyntaxListBuilder(Of VisualBasicSyntaxNode).Create()
                For i = indexOfEnd To trivia.Count - 1
                    builder.Add(trivia(i))
                Next
                Return builder.ToList()
            End If
        End Function

        ''' <summary>
        ''' Return the length of the common ending between the two
        ''' sets of trivia. The valid trivia (following skipped tokens)
        ''' of one must be contained in the valid trivia of the other. 
        ''' </summary>
        Friend Function GetLengthOfCommonEnd(trivia1 As SyntaxList(Of VisualBasicSyntaxNode), trivia2 As SyntaxList(Of VisualBasicSyntaxNode)) As Integer
            Dim n1 = trivia1.Count
            Dim n2 = trivia2.Count
            Dim offset1 = trivia1.GetIndexAfterLastSkippedToken()
            Dim offset2 = trivia2.GetIndexAfterLastSkippedToken()
            Dim n = Math.Min(n1 - offset1, n2 - offset2)
#If DEBUG Then
            For i = 0 To n - 1
                Dim t1 = trivia1(i + n1 - n)
                Dim t2 = trivia2(i + n2 - n)
                Debug.Assert(t1.Kind = t2.Kind)
                Debug.Assert(t1.ToFullString() = t2.ToFullString())
            Next
#End If
            Return n
        End Function

        <Extension()>
        Private Function GetIndexAfterLastSkippedToken(trivia As SyntaxList(Of VisualBasicSyntaxNode)) As Integer
            Dim n = trivia.Count
            For i = n - 1 To 0 Step -1
                If trivia(i).Kind = SyntaxKind.SkippedTokensTrivia Then
                    Return i + 1
                End If
            Next
            Return 0
        End Function

        ''' <summary>
        ''' Return the index within the trivia of what would be considered trailing
        ''' single-line trivia by the Scanner. This behavior must match ScanSingleLineTrivia.
        ''' In short, search walks backwards and stops at the second terminator
        ''' (colon or EOL) from the end, ignoring EOLs preceeded by line continuations.
        ''' </summary>
        <Extension()>
        Private Function GetIndexOfEndOfTrivia(trivia As SyntaxList(Of VisualBasicSyntaxNode)) As Integer
            Dim n = trivia.Count
            If n > 0 Then
                Dim i = n - 1
                Select Case trivia(i).Kind
                    Case SyntaxKind.ColonTrivia
                        Return i

                    Case SyntaxKind.EndOfLineTrivia
                        If i > 0 Then
                            Select Case trivia(i - 1).Kind
                                Case SyntaxKind.LineContinuationTrivia
                                    ' An EOL preceded by a line continuation should
                                    ' be considered whitespace rather than EOL.
                                    Return n
                                Case SyntaxKind.CommentTrivia
                                    Return i - 1
                                Case Else
                                    Return i
                            End Select
                        Else
                            Return i
                        End If

                    Case SyntaxKind.LineContinuationTrivia,
                         SyntaxKind.IfDirectiveTrivia,
                        SyntaxKind.ElseIfDirectiveTrivia,
                        SyntaxKind.ElseDirectiveTrivia,
                        SyntaxKind.EndIfDirectiveTrivia,
                        SyntaxKind.RegionDirectiveTrivia,
                        SyntaxKind.EndRegionDirectiveTrivia,
                        SyntaxKind.ConstDirectiveTrivia,
                        SyntaxKind.ExternalSourceDirectiveTrivia,
                        SyntaxKind.EndExternalSourceDirectiveTrivia,
                        SyntaxKind.ExternalChecksumDirectiveTrivia,
                        SyntaxKind.EnableWarningDirectiveTrivia,
                        SyntaxKind.DisableWarningDirectiveTrivia,
                        SyntaxKind.ReferenceDirectiveTrivia,
                        SyntaxKind.BadDirectiveTrivia

                        Throw ExceptionUtilities.UnexpectedValue(trivia(i).Kind)
                End Select
            End If
            Return n
        End Function

#Region "Skipped trivia creation"
        ' In order to handle creating SkippedTokens trivia correctly, we need to know if any structured
        ' trivia is present in a trivia list (because structured trivia can't contain structured trivia). 
        Private Function TriviaListContainsStructuredTrivia(triviaList As VisualBasicSyntaxNode) As Boolean
            If triviaList Is Nothing Then
                Return False
            End If

            Dim trivia = New SyntaxList(Of VisualBasicSyntaxNode)(triviaList)

            For i = 0 To trivia.Count - 1
                Select Case trivia.ItemUntyped(i).RawKind
                    Case SyntaxKind.XmlDocument,
                        SyntaxKind.SkippedTokensTrivia,
                        SyntaxKind.IfDirectiveTrivia,
                        SyntaxKind.ElseIfDirectiveTrivia,
                        SyntaxKind.ElseDirectiveTrivia,
                        SyntaxKind.EndIfDirectiveTrivia,
                        SyntaxKind.RegionDirectiveTrivia,
                        SyntaxKind.EndRegionDirectiveTrivia,
                        SyntaxKind.ConstDirectiveTrivia,
                        SyntaxKind.ExternalSourceDirectiveTrivia,
                        SyntaxKind.EndExternalSourceDirectiveTrivia,
                        SyntaxKind.ExternalChecksumDirectiveTrivia,
                        SyntaxKind.ReferenceDirectiveTrivia,
                        SyntaxKind.EnableWarningDirectiveTrivia,
                        SyntaxKind.DisableWarningDirectiveTrivia,
                        SyntaxKind.BadDirectiveTrivia
                        Return True
                End Select
            Next

            Return False
        End Function

        ' Simple class to create the best representation of skipped trivia as a combination of "regular" trivia
        ' and SkippedNode trivia. The initial trivia and trailing trivia are preserved as regular trivia, as well
        ' as any structured trivia. We also remove any missing tokens and promote their trivia. Otherwise we try to put
        ' as many consecutive tokens as possible into a SkippedTokens trivia node.
        Private Class SkippedTriviaBuilder
            ' Maintain the list of trivia that we're accumulating.
            Private triviaListBuilder As SyntaxListBuilder(Of VisualBasicSyntaxNode) = SyntaxListBuilder(Of VisualBasicSyntaxNode).Create()

            ' Maintain a list of tokens we're accumulating to put into a SkippedNodes trivia.
            Private skippedTokensBuilder As SyntaxListBuilder(Of SyntaxToken) = SyntaxListBuilder(Of SyntaxToken).Create()

            Private preserveExistingDiagnostics As Boolean
            Private addDiagnosticsToFirstTokenOnly As Boolean
            Private diagnosticsToAdd As IEnumerable(Of DiagnosticInfo)

            ' Add a trivia to the triva we are accumulating.
            Private Sub AddTrivia(trivia As VisualBasicSyntaxNode)
                FinishInProgressTokens()
                triviaListBuilder.AddRange(trivia)
            End Sub

            ' Create a SkippedTokens trivia from any tokens currently accumulated into the skippedTokensBuilder. If not,
            ' don't do anything.
            Private Sub FinishInProgressTokens()
                If skippedTokensBuilder.Count > 0 Then
                    Dim skippedTokensTrivia As VisualBasicSyntaxNode = SyntaxFactory.SkippedTokensTrivia(skippedTokensBuilder.ToList())

                    If diagnosticsToAdd IsNot Nothing Then
                        For Each d In diagnosticsToAdd
                            skippedTokensTrivia = skippedTokensTrivia.AddError(d)
                        Next
                        diagnosticsToAdd = Nothing ' only add once.
                    End If

                    triviaListBuilder.Add(skippedTokensTrivia)

                    skippedTokensBuilder.Clear()
                End If
            End Sub

            Public Sub New(preserveExistingDiagnostics As Boolean,
                           addDiagnosticsToFirstTokenOnly As Boolean,
                           diagnosticsToAdd As IEnumerable(Of DiagnosticInfo))
                Me.addDiagnosticsToFirstTokenOnly = addDiagnosticsToFirstTokenOnly
                Me.preserveExistingDiagnostics = preserveExistingDiagnostics
                Me.diagnosticsToAdd = diagnosticsToAdd
            End Sub

            ' Process a token. and add to the list of triva/tokens we're accumulating.
            Public Sub AddToken(token As SyntaxToken, isFirst As Boolean, isLast As Boolean)
                Dim isMissing As Boolean = token.IsMissing

                If token.HasLeadingTrivia() AndAlso (isFirst OrElse isMissing OrElse TriviaListContainsStructuredTrivia(token.GetLeadingTrivia())) Then
                    FinishInProgressTokens()
                    AddTrivia(token.GetLeadingTrivia())
                    token = DirectCast(token.WithLeadingTrivia(Nothing), SyntaxToken)
                End If

                If Not preserveExistingDiagnostics Then
                    token = token.WithoutDiagnostics()
                End If

                Dim trailingTrivia As VisualBasicSyntaxNode = Nothing

                If token.HasTrailingTrivia() AndAlso (isLast OrElse isMissing OrElse TriviaListContainsStructuredTrivia(token.GetTrailingTrivia())) Then
                    trailingTrivia = token.GetTrailingTrivia()
                    token = DirectCast(token.WithTrailingTrivia(Nothing), SyntaxToken)
                End If

                If isMissing Then
                    ' Don't add missing tokens to skipped tokens, but preserve their diagnostics.
                    If token.ContainsDiagnostics() Then
                        ' Move diagnostics on missing token to next token.
                        If diagnosticsToAdd IsNot Nothing Then
                            diagnosticsToAdd = diagnosticsToAdd.Concat(token.GetDiagnostics())
                        Else
                            diagnosticsToAdd = token.GetDiagnostics()
                        End If
                        addDiagnosticsToFirstTokenOnly = True
                    End If
                Else
                    skippedTokensBuilder.Add(token)
                End If

                If trailingTrivia IsNot Nothing Then
                    FinishInProgressTokens()
                    AddTrivia(trailingTrivia)
                End If

                If isFirst AndAlso addDiagnosticsToFirstTokenOnly Then
                    FinishInProgressTokens() ' implicitly adds the diagnostics.
                End If
            End Sub

            ' Get the final list of trivia nodes we should attached.
            Public Function GetTriviaList() As SyntaxList(Of VisualBasicSyntaxNode)
                FinishInProgressTokens()
                If diagnosticsToAdd IsNot Nothing AndAlso diagnosticsToAdd.Any() Then
                    ' Still have diagnostics. Add to the last item.
                    If triviaListBuilder.Count > 0 Then
                        triviaListBuilder(triviaListBuilder.Count - 1) = triviaListBuilder(triviaListBuilder.Count - 1).WithAdditionalDiagnostics(diagnosticsToAdd.ToArray())
                    End If
                End If
                Return triviaListBuilder.ToList()
            End Function
        End Class

        ' From a syntax node, create a list of trivia node that encapsulates the same text. We use SkippedTokens trivia
        ' to encapsulate the tokens, plus extract trivia from those tokens into the trivia list because:
        '    - We want leading trivia and trailing trivia to be directly visible in the trivia list, not on the tokens
        '      inside the skipped tokens trivia.
        '    - We have to expose structured trivia directives.
        '
        ' Several options controls how diagnostics are handled:
        '   "preserveDiagnostics" means existing diagnostics are preserved, otherwise they are thrown away
        '   "addDiagnostic", if not Nothing, is added as a diagnostics
        '   "addDiagnosticsToFirstTokenOnly" means that "addDiagnostics" is attached only to the first token, otherwise
        '    it is attached to all tokens.
        Private Function CreateSkippedTrivia(node As VisualBasicSyntaxNode,
                                             preserveDiagnostics As Boolean,
                                             addDiagnosticToFirstTokenOnly As Boolean,
                                             addDiagnostic As DiagnosticInfo) As SyntaxList(Of VisualBasicSyntaxNode)
            If node.Kind = SyntaxKind.SkippedTokensTrivia Then
                ' already skipped trivia
                If addDiagnostic IsNot Nothing Then
                    node = node.AddError(addDiagnostic)
                End If
                Return node
            End If

            ' Get the tokens and diagnostics.
            Dim diagnostics As IList(Of DiagnosticInfo) = New List(Of DiagnosticInfo)
            Dim tokenListBuilder = SyntaxListBuilder(Of SyntaxToken).Create

            node.CollectConstituentTokensAndDiagnostics(tokenListBuilder, diagnostics)

            ' Adjust diagnostics based on input.
            If Not preserveDiagnostics Then
                diagnostics.Clear()
            End If
            If addDiagnostic IsNot Nothing Then
                diagnostics.Add(addDiagnostic)
            End If

            Dim skippedTriviaBuilder As New SkippedTriviaBuilder(preserveDiagnostics, addDiagnosticToFirstTokenOnly, diagnostics)

            ' Get through each token and add it. 
            For i As Integer = 0 To tokenListBuilder.Count - 1
                Dim currentToken As SyntaxToken = tokenListBuilder(i)

                skippedTriviaBuilder.AddToken(currentToken, isFirst:=(i = 0), isLast:=(i = tokenListBuilder.Count - 1))
            Next

            Return skippedTriviaBuilder.GetTriviaList()
        End Function

#End Region

#Region "Whitespace Containment"
        <Extension()>
        Friend Function ContainsWhitespaceTrivia(this As VisualBasicSyntaxNode) As Boolean
            If this Is Nothing Then
                Return False
            End If

            Dim trivia = New SyntaxList(Of VisualBasicSyntaxNode)(this)

            For i = 0 To trivia.Count - 1
                Dim kind = trivia.ItemUntyped(i).RawKind
                If kind = SyntaxKind.WhitespaceTrivia OrElse
                    kind = SyntaxKind.EndOfLineTrivia Then

                    Return True
                End If
            Next

            Return False
        End Function
#End Region

        ' This was Semantics::ExtractAnonTypeMemberName in Dev 10
        <Extension()>
        Friend Function ExtractAnonymousTypeMemberName(input As ExpressionSyntax,
                                           ByRef isNameDictinaryAccess As Boolean,
                                           ByRef isRejectedXmlName As Boolean) As SyntaxToken
TryAgain:
            Select Case input.Kind
                Case SyntaxKind.IdentifierName
                    Return DirectCast(input, IdentifierNameSyntax).Identifier

                Case SyntaxKind.XmlName
                    Dim xmlNameInferredFrom = DirectCast(input, XmlNameSyntax)
                    If Not Scanner.IsIdentifier(xmlNameInferredFrom.LocalName.ToString) Then
                        isRejectedXmlName = True
                        Return Nothing
                    End If

                    Return xmlNameInferredFrom.LocalName

                Case SyntaxKind.XmlBracketedName
                    ' handles something like <a-a>
                    Dim xmlNameInferredFrom = DirectCast(input, XmlBracketedNameSyntax)
                    input = xmlNameInferredFrom.Name
                    GoTo TryAgain

                Case SyntaxKind.SimpleMemberAccessExpression,
                     SyntaxKind.DictionaryAccessExpression

                    Dim memberAccess = DirectCast(input, MemberAccessExpressionSyntax)

                    If input.Kind = SyntaxKind.SimpleMemberAccessExpression Then
                        ' See if this is an identifier qualifed with XmlElementAccessExpression or XmlDescendantAccessExpression

                        If memberAccess.Expression IsNot Nothing Then
                            Select Case memberAccess.Expression.Kind
                                Case SyntaxKind.XmlElementAccessExpression,
                                    SyntaxKind.XmlDescendantAccessExpression

                                    input = memberAccess.Expression
                                    GoTo TryAgain
                            End Select
                        End If
                    End If

                    isNameDictinaryAccess = input.Kind = SyntaxKind.DictionaryAccessExpression
                    input = memberAccess.Name
                    GoTo TryAgain

                Case SyntaxKind.XmlElementAccessExpression,
                     SyntaxKind.XmlAttributeAccessExpression,
                     SyntaxKind.XmlDescendantAccessExpression

                    Dim xmlAccess = DirectCast(input, XmlMemberAccessExpressionSyntax)

                    input = xmlAccess.Name
                    GoTo TryAgain

                Case SyntaxKind.InvocationExpression
                    Dim invocation = DirectCast(input, InvocationExpressionSyntax)

                    If invocation.ArgumentList Is Nothing OrElse invocation.ArgumentList.Arguments.Count = 0 Then
                        input = invocation.Expression
                        GoTo TryAgain
                    End If

                    Debug.Assert(invocation.ArgumentList IsNot Nothing)

                    If invocation.ArgumentList.Arguments.Count = 1 Then
                        ' See if this is an indexed XmlElementAccessExpression or XmlDescendantAccessExpression
                        Select Case invocation.Expression.Kind
                            Case SyntaxKind.XmlElementAccessExpression,
                                SyntaxKind.XmlDescendantAccessExpression
                                input = invocation.Expression
                                GoTo TryAgain
                        End Select
                    End If

            End Select

            Return Nothing
        End Function

        Friend Function IsExecutableStatementOrItsPart(node As VisualBasicSyntaxNode) As Boolean
            If TypeOf node Is ExecutableStatementSyntax Then
                Return True
            End If

            ' Parser parses some statements part-by-part and then wraps them with executable 
            ' statements, so we may stumble on such a part in error-recovery scenarios in case parser 
            ' didn't wrap it because of some parse error; in some cases such statements should 
            ' be considered equal to executable statements
            ' 
            ' Example: parsing a simple text 'If True' produces just an IfStatementSyntax (which
            ' is not an executable statement) which is supposed to be wrapped with MultiLineIfBlockSyntax
            ' or SingleLineIfStatement in non-error scenario.
            Select Case node.Kind
                Case SyntaxKind.IfStatement,
                     SyntaxKind.ElseIfStatement,
                     SyntaxKind.ElseStatement,
                     SyntaxKind.WithStatement,
                     SyntaxKind.TryStatement,
                     SyntaxKind.CatchStatement,
                     SyntaxKind.FinallyStatement,
                     SyntaxKind.SyncLockStatement,
                     SyntaxKind.WhileStatement,
                     SyntaxKind.UsingStatement,
                     SyntaxKind.SelectStatement,
                     SyntaxKind.CaseStatement,
                     SyntaxKind.CaseElseStatement,
                     SyntaxKind.DoStatement,
                     SyntaxKind.ForStatement,
                     SyntaxKind.ForEachStatement
                    Return True
            End Select

            Return False
        End Function

    End Module
End Namespace

