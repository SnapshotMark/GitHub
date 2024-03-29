﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public partial class SyntaxNodeTests : TestBase
    {
        [Fact]
        public void TestReplaceOneNodeAsync()
        {
            var text = @"public class C { public int X; }";
            var expected = @"public class C { public int Y; }";

            var tree = SyntaxFactory.ParseSyntaxTree(text);
            var root = tree.GetRoot();

            var node = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
            var newRoot = root.ReplaceNodesAsync(new[] { node }, (o, n, c) =>
            {
                var decl = (VariableDeclaratorSyntax)n;
                return Task.FromResult<SyntaxNode>(decl.WithIdentifier(SyntaxFactory.Identifier("Y")));
            }, CancellationToken.None).Result;

            var actual = newRoot.ToString();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestReplaceNestedNodesAsync()
        {
            var text = @"public class C { public int X; }";
            var expected = @"public class C1 { public int X1; }";

            var tree = SyntaxFactory.ParseSyntaxTree(text);
            var root = tree.GetRoot();

            var nodes = root.DescendantNodes().Where(n => n is VariableDeclaratorSyntax || n is ClassDeclarationSyntax).ToList();
            int computations = 0;
            var newRoot = root.ReplaceNodesAsync(nodes, (o, n, c) =>
            {
                computations++;
                var classDecl = n as ClassDeclarationSyntax;
                if (classDecl != null)
                {
                    var id = classDecl.Identifier;
                    return Task.FromResult<SyntaxNode>(classDecl.WithIdentifier(SyntaxFactory.Identifier(id.LeadingTrivia, id.ToString() + "1", id.TrailingTrivia)));
                }

                var varDecl = n as VariableDeclaratorSyntax;
                if (varDecl != null)
                {
                    var id = varDecl.Identifier;
                    return Task.FromResult<SyntaxNode>(varDecl.WithIdentifier(SyntaxFactory.Identifier(id.LeadingTrivia, id.ToString() + "1", id.TrailingTrivia)));
                }

                return Task.FromResult<SyntaxNode>(n);
            }, CancellationToken.None).Result;

            var actual = newRoot.ToString();

            Assert.Equal(expected, actual);
            Assert.Equal(computations, nodes.Count);
        }

        [Fact]
        public void TestTrackNodesWithDocument()
        {
            var pid = ProjectId.CreateNewId();
            var did = DocumentId.CreateNewId(pid);

            var sourceText = @"public class C { void M() { } }";

            var sol = new CustomWorkspace().CurrentSolution
                .AddProject(pid, "proj", "proj", LanguageNames.CSharp)
                .AddDocument(did, "doc", sourceText);

            var doc = sol.GetDocument(did);

            // find initial nodes of interest
            var root = doc.GetSyntaxRootAsync().Result;
            var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var methodDecl = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

            // track these nodes
            var trackedRoot = root.TrackNodes(classDecl, methodDecl);

            // use some fancy document centric rewrites
            var comp = doc.Project.GetCompilationAsync().Result;

            var cgenField = CodeGenerationSymbolFactory.CreateFieldSymbol(
                attributes: null,
                accessibility: Accessibility.Private,
                modifiers: new SymbolModifiers(),
                type: comp.GetSpecialType(SpecialType.System_Int32),
                name: "X");

            var currentClassDecl = trackedRoot.GetCurrentNodes(classDecl).First();
            var classDeclWithField = Formatter.Format(
                                        CodeGenerator.AddFieldDeclaration(currentClassDecl, cgenField, sol.Workspace),
                                        sol.Workspace);

            // we can find related bits even from sub-tree fragments
            var latestMethod = classDeclWithField.GetCurrentNodes(methodDecl).First();
            Assert.NotNull(latestMethod);
            Assert.NotEqual(latestMethod, methodDecl);

            trackedRoot = trackedRoot.ReplaceNode(currentClassDecl, classDeclWithField);

            // put back into document (branch solution, etc)
            doc = doc.WithSyntaxRoot(trackedRoot);

            // re-get root of new document
            var root2 = doc.GetSyntaxRootAsync().Result;
            Assert.NotEqual(trackedRoot, root2);

            // we can still find the tracked node in the new document
            var finalClassDecl = root2.GetCurrentNodes(classDecl).First();
            Assert.Equal(@"public class C { private System.Int32 X; void M() { } }", finalClassDecl.ToString());

            // and other tracked nodes too
            var finalMethodDecl = root2.GetCurrentNodes(methodDecl).First();
            Assert.NotNull(finalMethodDecl);
            Assert.NotEqual(finalMethodDecl, methodDecl);
        }
    }
}