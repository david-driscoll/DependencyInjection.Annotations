using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;

namespace Blacklite.Framework.DI.Compiler.Tests
{

    /// <summary>
    /// Summary description for BeforeCompileContext
    /// </summary>
    public class BeforeCompileContext : IBeforeCompileContext
    {
        public CSharpCompilation CSharpCompilation { get; set; }

        public IList<ResourceDescription> Resources { get; } = new List<ResourceDescription>();

        public IList<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
    }
}