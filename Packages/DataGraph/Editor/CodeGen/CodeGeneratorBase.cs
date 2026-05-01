using System;
using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Runtime;

namespace DataGraph.Editor.CodeGen
{
    /// <summary>
    /// Base class shared by SO/Blob/Quantum code generators. Provides a
    /// template method <see cref="Generate"/> for the boilerplate every
    /// generated file needs (try/catch, CodeWriter, header, namespace
    /// block) and a static walker for emitting nested type declarations.
    /// Concrete generators keep their existing public API
    /// (GenerateEntries / GenerateDatabase / GenerateBuilder / Generate
    /// / GenerateEnum) and call into Generate from each one — this base
    /// is a deduplication aid, not a single-entry-point visitor.
    /// </summary>
    internal abstract class CodeGeneratorBase
    {
        protected const string GeneratedNamespace = "DataGraph.Data";

        /// <summary>
        /// Skeleton: instantiates a CodeWriter, emits the per-format header
        /// via <see cref="WriteHeader"/>, opens the
        /// "namespace DataGraph.Data" block, runs <paramref name="writeBody"/>,
        /// closes the block, and wraps the result in <see cref="Result{T}"/>.
        /// Exceptions become <c>Result.Failure</c> with
        /// <paramref name="failurePrefix"/> as the leading message.
        /// </summary>
        protected Result<string> Generate(string failurePrefix, Action<CodeWriter> writeBody)
            => GenerateWithHeader(failurePrefix, WriteHeader, writeBody);

        /// <summary>
        /// Variant of <see cref="Generate"/> that lets a generator emit a
        /// non-default header (e.g. BlobCodeGenerator.GenerateBuilder uses
        /// a different using set than GenerateEntries / GenerateDatabase).
        /// </summary>
        protected Result<string> GenerateWithHeader(
            string failurePrefix,
            Action<CodeWriter> writeHeader,
            Action<CodeWriter> writeBody)
        {
            try
            {
                var w = new CodeWriter();
                writeHeader(w);
                w.BeginBlock($"namespace {GeneratedNamespace}");
                writeBody(w);
                w.EndBlock();
                return Result<string>.Success(w.ToString());
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"{failurePrefix}: {ex.Message}");
            }
        }

        /// <summary>
        /// Emits the per-format file header (auto-gen comment + using
        /// directives). Called once at the top of <see cref="Generate"/>.
        /// </summary>
        protected abstract void WriteHeader(CodeWriter w);

        /// <summary>
        /// Walks one node and, if it represents a nested type
        /// (Object, vertical Array with structural children, Dictionary
        /// with structural children), invokes <paramref name="emit"/> with
        /// that node's TypeName and child list, prefixed by a blank line.
        /// Concrete generators pass their own "write a class/struct here"
        /// callback so the walk logic stays in one place.
        /// </summary>
        protected static void WalkNestedDeclaration(CodeWriter w, ParseableNode node,
            Action<string, IReadOnlyList<ParseableNode>> emit)
        {
            switch (node)
            {
                case ParseableObjectField obj:
                    w.BlankLine();
                    emit(obj.TypeName, obj.Children);
                    break;
                case ParseableArrayField arr
                    when arr.Mode == ArrayMode.Vertical
                        && CodeGenHelpers.HasStructuralChildren(arr):
                    w.BlankLine();
                    emit(arr.TypeName, arr.Children);
                    break;
                case ParseableDictionaryField dict
                    when CodeGenHelpers.HasStructuralChildren(dict):
                    w.BlankLine();
                    emit(dict.TypeName, dict.Children);
                    break;
            }
        }
    }
}
