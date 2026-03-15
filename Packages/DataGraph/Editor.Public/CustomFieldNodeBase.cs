using System;
using Unity.GraphToolkit.Editor;

namespace DataGraph.Editor.Public
{
    /// <summary>
    /// Abstract base class for user-created custom field nodes.
    /// Inherit from this to create new leaf node types that parse
    /// cell values into custom C# types.
    /// 
    /// Subclasses must define their own GTK options via OnDefineOptions
    /// and implement GetFieldName, GetColumn, and GetOutputTypeName.
    /// 
    /// Example:
    /// <code>
    /// [Serializable]
    /// public class DateFieldNode : CustomFieldNodeBase
    /// {
    ///     protected override void OnDefineOptions(IOptionDefinitionContext context)
    ///     {
    ///         base.OnDefineOptions(context);
    ///         context.AddOption&lt;string&gt;("DateFormat").WithDisplayName("Date Format").WithDefaultValue("yyyy-MM-dd");
    ///     }
    ///     
    ///     public override string GetOutputTypeName() => "DateTime";
    /// }
    /// </code>
    /// </summary>
    public abstract class CustomFieldNodeBase : Node
    {
        /// <summary>
        /// The field name used in generated C# classes.
        /// Read from GTK option "FieldName".
        /// </summary>
        public string GetFieldName()
        {
            var option = GetNodeOptionByName("FieldName");
            if (option != null && option.TryGetValue<string>(out var val) && !string.IsNullOrEmpty(val))
                return val;
            return "customField";
        }

        /// <summary>
        /// The spreadsheet column this node reads from.
        /// Read from GTK option "Column".
        /// </summary>
        public string GetColumn()
        {
            var option = GetNodeOptionByName("Column");
            if (option != null && option.TryGetValue<string>(out var val) && !string.IsNullOrEmpty(val))
                return val;
            return "A";
        }

        /// <summary>
        /// The C# type name this node produces (e.g. "DateTime", "TimeSpan").
        /// Used by code generator for the output field type.
        /// </summary>
        public abstract string GetOutputTypeName();

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("FieldName").WithDisplayName("Field Name");
            context.AddOption<string>("Column").WithDisplayName("Column").WithDefaultValue("A");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<object>("Parent").WithDisplayName("Parent").Build();
        }
    }
}
