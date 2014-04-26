﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed class EmbeddedStatementBinder : LocalScopeBinder
    {
        private readonly StatementSyntax embeddedStatement;

        public EmbeddedStatementBinder(MethodSymbol owner, Binder enclosing, StatementSyntax embeddedStatement)
            : base(owner, enclosing, enclosing.Flags)
        {
            this.embeddedStatement = embeddedStatement;
        }

        protected override ImmutableArray<LocalSymbol> BuildLocals()
        {
            return BuildLocals(embeddedStatement,
                               isRoot: false); // Pass false because statements that always introduce a new scope should be ignored by this binder.
        }

        internal override ImmutableArray<LocalSymbol> GetDeclaredLocalsForScope(CSharpSyntaxNode node)
        {
            if (node == embeddedStatement)
            {
                return this.Locals;
            }

            throw ExceptionUtilities.Unreachable;
        }
    }
}
