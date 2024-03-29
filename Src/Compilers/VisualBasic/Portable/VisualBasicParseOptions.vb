﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.SyntaxFacts

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' Represents Visual Basic parse options.
    ''' </summary>
    Public NotInheritable Class VisualBasicParseOptions
        Inherits ParseOptions
        Implements IEquatable(Of VisualBasicParseOptions)

        Public Shared ReadOnly [Default] As VisualBasicParseOptions = New VisualBasicParseOptions()
        Private Shared _defaultPreprocessorSymbols As ImmutableArray(Of KeyValuePair(Of String, Object))

        Private _preprocessorSymbols As ImmutableArray(Of KeyValuePair(Of String, Object))
        Private _languageVersion As LanguageVersion

        ''' <summary>
        ''' Creates an instance of VisualBasicParseOptions.
        ''' </summary>
        ''' <param name="languageVersion">The parser language version.</param>
        ''' <param name="documentationMode">The comentation mode.</param>
        ''' <param name="kind">The kind of source code.<see cref="SourceCodeKind"/></param>
        ''' <param name="preprocessorSymbols">An immutable array of KeyValuePair representing pre processor symbols.</param>
        Public Sub New(
            Optional languageVersion As LanguageVersion = LanguageVersion.VisualBasic11,
            Optional documentationMode As DocumentationMode = DocumentationMode.Parse,
            Optional kind As SourceCodeKind = SourceCodeKind.Regular,
            Optional preprocessorSymbols As IEnumerable(Of KeyValuePair(Of String, Object)) = Nothing)

            MyClass.New(languageVersion,
                        documentationMode,
                        kind,
                        If(preprocessorSymbols Is Nothing, DefaultPreprocessorSymbols, ImmutableArray.CreateRange(preprocessorSymbols)))

            If Not languageVersion.IsValid Then
                Throw New ArgumentOutOfRangeException("languageVersion")
            End If

            If Not kind.IsValid Then
                Throw New ArgumentOutOfRangeException("kind")
            End If

            ValidatePreprocessorSymbols(preprocessorSymbols, "preprocessorSymbols")
        End Sub

        Private Shared Sub ValidatePreprocessorSymbols(preprocessorSymbols As IEnumerable(Of KeyValuePair(Of String, Object)),
                                                       parameterName As String)
            If preprocessorSymbols Is Nothing Then
                Return
            End If

            For Each symbol In preprocessorSymbols
                If Not IsValidIdentifier(symbol.Key) OrElse
                   SyntaxFacts.GetKeywordKind(symbol.Key) <> SyntaxKind.None Then

                    Throw New ArgumentException(parameterName)
                End If

                Debug.Assert(SyntaxFactory.ParseTokens(symbol.Key).Select(Function(t) t.VisualBasicKind).SequenceEqual({SyntaxKind.IdentifierToken, SyntaxKind.EndOfFileToken}))

                Dim constant = InternalSyntax.CConst.TryCreate(symbol.Value)
                If constant Is Nothing Then
                    Throw New ArgumentException(String.Format(VBResources.InvalidPreprocessorConstantType, symbol.Key, symbol.Value.GetType()), parameterName)
                End If
            Next
        End Sub

        ' Does not perform validation.
        Friend Sub New(
            languageVersion As LanguageVersion,
            documentationMode As DocumentationMode,
            kind As SourceCodeKind,
            preprocessorSymbols As ImmutableArray(Of KeyValuePair(Of String, Object)))

            MyBase.New(kind, documentationMode)

            Debug.Assert(Not preprocessorSymbols.IsDefault)
            _languageVersion = languageVersion
            _preprocessorSymbols = preprocessorSymbols
        End Sub

        Private Sub New(other As VisualBasicParseOptions)
            MyClass.New(
                languageVersion:=other._languageVersion,
                documentationMode:=other.DocumentationMode,
                kind:=other.Kind,
                preprocessorSymbols:=other._preprocessorSymbols)
        End Sub

        Private Shared ReadOnly Property DefaultPreprocessorSymbols As ImmutableArray(Of KeyValuePair(Of String, Object))
            Get
                If _defaultPreprocessorSymbols.IsDefaultOrEmpty Then
                    _defaultPreprocessorSymbols = ImmutableArray.Create(KeyValuePair.Create("_MYTYPE", CObj("Empty")))
                End If

                Return _defaultPreprocessorSymbols
            End Get
        End Property

        ''' <summary>
        ''' Returns the parser language version.
        ''' </summary>        
        Public ReadOnly Property LanguageVersion As LanguageVersion
            Get
                Return _languageVersion
            End Get
        End Property

        ''' <summary>
        ''' The preprocessor symbols to parse with. 
        ''' </summary>
        ''' <remarks>
        ''' May contain duplicate keys. The last one wins. 
        ''' </remarks>
        Public ReadOnly Property PreprocessorSymbols As ImmutableArray(Of KeyValuePair(Of String, Object))
            Get
                Return _preprocessorSymbols
            End Get
        End Property

        ''' <summary>
        ''' Returns a collection of preprocessor symbol names. 
        ''' </summary>
        Public Overrides ReadOnly Property PreprocessorSymbolNames As IEnumerable(Of String)
            Get
                Return _preprocessorSymbols.Select(Function(ps) ps.Key)
            End Get
        End Property

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified language version.
        ''' </summary>
        ''' <param name="version">The parser language version.</param>
        ''' <returns>A new instance of VisualBasicParseOptions if different language version is different; otherwise current instance.</returns>
        Public Shadows Function WithLanguageVersion(version As LanguageVersion) As VisualBasicParseOptions
            If version = _languageVersion Then
                Return Me
            End If

            If Not version.IsValid Then
                Throw New ArgumentOutOfRangeException("version")
            End If

            Return New VisualBasicParseOptions(Me) With {._languageVersion = version}
        End Function

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified source code kind.
        ''' </summary>
        ''' <param name="kind">The parser source code kind.</param>
        ''' <returns>A new instance of VisualBasicParseOptions if source code kind is different; otherwise current instance.</returns>
        Public Shadows Function WithKind(kind As SourceCodeKind) As VisualBasicParseOptions
            If kind = Me.Kind Then
                Return Me
            End If

            If Not kind.IsValid Then
                Throw New ArgumentOutOfRangeException("kind")
            End If

            Return New VisualBasicParseOptions(Me) With {.Kind = kind}
        End Function

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified documentation mode.
        ''' </summary>
        ''' <param name="documentationMode"></param>
        ''' <returns>A new instance of VisualBasicParseOptions if documentation mode is different; otherwise current instance.</returns>
        Public Overloads Function WithDocumentationMode(documentationMode As DocumentationMode) As VisualBasicParseOptions
            If documentationMode = Me.DocumentationMode Then
                Return Me
            End If

            If Not documentationMode.IsValid() Then
                Throw New ArgumentOutOfRangeException("documentationMode")
            End If

            Return New VisualBasicParseOptions(Me) With {.DocumentationMode = documentationMode}
        End Function

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified collection of KeyValuePairs representing pre-processor symbols.
        ''' </summary>
        ''' <param name="symbols">A collection representing pre-processor symbols</param>
        ''' <returns>A new instance of VisualBasicParseOptions.</returns>
        Public Shadows Function WithPreprocessorSymbols(symbols As IEnumerable(Of KeyValuePair(Of String, Object))) As VisualBasicParseOptions
            Return WithPreprocessorSymbols(symbols.AsImmutableOrNull())
        End Function

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified collection of KeyValuePairs representing pre-processor symbols.
        ''' </summary>
        ''' <param name="symbols">An parameter array of KeyValuePair representing pre-processor symbols.</param>
        ''' <returns>A new instance of VisualBasicParseOptions.</returns>
        Public Shadows Function WithPreprocessorSymbols(ParamArray symbols As KeyValuePair(Of String, Object)()) As VisualBasicParseOptions
            Return WithPreprocessorSymbols(symbols.AsImmutableOrNull())
        End Function

        ''' <summary>
        ''' Returns a VisualBasicParseOptions instance for a specified collection of KeyValuePairs representing pre-processor symbols.
        ''' </summary>
        ''' <param name="symbols">An ImmutableArray of KeyValuePair representing pre-processor symbols.</param>
        ''' <returns>A new instance of VisualBasicParseOptions.</returns>
        Public Shadows Function WithPreprocessorSymbols(symbols As ImmutableArray(Of KeyValuePair(Of String, Object))) As VisualBasicParseOptions
            If symbols.IsDefault Then
                symbols = ImmutableArray(Of KeyValuePair(Of String, Object)).Empty
            End If

            If symbols.Equals(Me.PreprocessorSymbols) Then
                Return Me
            End If

            ValidatePreprocessorSymbols(symbols, "symbols")

            Return New VisualBasicParseOptions(Me) With {._preprocessorSymbols = symbols}
        End Function

        ''' <summary>
        ''' Returns a ParseOptions instance for a specified Source Code Kind.
        ''' </summary>
        ''' <param name="kind">The parser source code kind.</param>
        ''' <returns>A new instance of ParseOptions.</returns>
        Protected Overrides Function CommonWithKind(kind As SourceCodeKind) As ParseOptions
            Return WithKind(kind)
        End Function

        ''' <summary>
        ''' Returns a ParseOptions instance for a specified Documentation Mode.
        ''' </summary>
        ''' <param name="documentationMode">The documentation mode.</param>
        ''' <returns>A new instance of ParseOptions.</returns>
        Protected Overrides Function CommonWithDocumentationMode(documentationMode As DocumentationMode) As ParseOptions
            Return WithDocumentationMode(documentationMode)
        End Function

        ''' <summary>
        ''' Determines whether the current object is equal to another object of the same type.
        ''' </summary>
        ''' <param name="other">An VisualBasicParseOptions object to compare with this object</param>
        ''' <returns>A boolean value.  True if the current object is equal to the other parameter; otherwise, False.</returns>
        Public Overloads Function Equals(other As VisualBasicParseOptions) As Boolean Implements IEquatable(Of VisualBasicParseOptions).Equals
            If Me Is other Then
                Return True
            End If

            If Not MyBase.EqualsHelper(other) Then
                Return False
            End If

            If Me.LanguageVersion <> other.LanguageVersion Then
                Return False
            End If

            If Not Me.PreprocessorSymbols.SequenceEqual(other.PreprocessorSymbols) Then
                Return False
            End If

            Return True
        End Function

        ''' <summary>
        ''' Indicates whether the current object is equal to another object.
        ''' </summary>
        ''' <param name="obj">An object to compare with this object</param>
        ''' <returns>A boolean value.  True if the current object is equal to the other parameter; otherwise, False.</returns>
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Equals(TryCast(obj, VisualBasicParseOptions))
        End Function

        ''' <summary>
        ''' Returns a hashcode for this instance.
        ''' </summary>
        ''' <returns>A hashcode representing this instance.</returns>
        Public Overrides Function GetHashCode() As Integer
            Return Hash.Combine(MyBase.GetHashCodeHelper(), CInt(Me.LanguageVersion))
        End Function
    End Class
End Namespace
