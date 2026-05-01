using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Adapter;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.Domain;
using DataGraph.Editor.IO;
using DataGraph.Editor.Public;
using DataGraph.Editor.Parsing;
using DataGraph.Editor.Reflection;
using DataGraph.Editor.Serialization;
using DataGraph.Editor.UI;
using DataGraph.Runtime;
using UnityEngine;
using UnityEditor;

namespace DataGraph.Editor.Commands
{
    /// <summary>
    /// Orchestrates the full parse pipeline for a single graph:
    /// Adapt -> Fetch -> Parse -> Validate -> Generate -> Serialize.
    /// Logs each step to a GraphLogGroup for console display.
    /// </summary>
    internal sealed class ParseGraphCommand
    {
        private readonly GraphModelAdapter _adapter = new();
        private readonly ParserEngine _parserEngine = new();
        private readonly DataValidator _dataValidator = new();

        /// <summary>
        /// Output format selection for a parse run.
        /// </summary>
        internal sealed class FormatSelection
        {
            public bool GenerateSO { get; set; }
            public bool GenerateJSON { get; set; }
            public bool GenerateBlob { get; set; }
            public bool GenerateQuantum { get; set; }
        }

        /// <summary>
        /// Runs the full pipeline, logging each step to the provided log group.
        /// </summary>
        public async Task<bool> ExecuteAsync(
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            FormatSelection formats,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken = default)
        {
            var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                ? graphAsset.GraphName
                : graphAsset.name;

            try
            {
                // 1. Adapt
                log.LogInfo("Adapt: reading graph structure...");
                var adaptResult = _adapter.ReadGraph(graphAsset);
                if (adaptResult.IsFailure)
                {
                    log.LogError($"Adapt failed: {adaptResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var graph = adaptResult.Value;
                log.LogInfo($"Adapt: {graph.AllNodes.Count} nodes resolved");

                // Early branch for Enum/Flag definition graphs
                if (graph.Root is ParseableEnumRoot || graph.Root is ParseableFlagRoot)
                {
                    return await ExecuteEnumPipelineAsync(
                        graph, graphAsset, provider, outputBasePath, log, cancellationToken);
                }

                // Extract referenced columns for optimized fetching
                var columns = ColumnExtractor.GetReferencedColumns(graph);
                if (columns != null)
                    log.LogInfo($"Fetch: optimized — requesting {columns.Count} columns: {string.Join(", ", columns)}");

                // 2. Fetch
                log.LogInfo($"Fetch: requesting data from sheet...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName, columns);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var tableData = fetchResult.Value;
                log.LogInfo($"Fetch: {tableData.RowCount} data rows, {tableData.ColumnCount} columns");

                // 3. Parse
                log.LogInfo("Parse: processing table data...");
                var parseResult = _parserEngine.Parse(tableData, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var dataTree = parseResult.Value;
                foreach (var warning in dataTree.ParseWarnings)
                {
                    log.LogWarning($"Parse: {warning.Message}");
                }

                log.LogInfo("Parse: completed");

                // 4. Validate
                log.LogInfo("Validate: checking parsed data...");
                var report = _dataValidator.Validate(dataTree);
                foreach (var entry in report.Entries)
                {
                    switch (entry.Severity)
                    {
                        case ValidationSeverity.Error:
                            log.LogError($"Validate: {entry.Message}");
                            break;
                        case ValidationSeverity.Warning:
                            log.LogWarning($"Validate: {entry.Message}");
                            break;
                        case ValidationSeverity.Info:
                            log.LogInfo($"Validate: {entry.Message}");
                            break;
                    }
                }

                if (report.HasErrors)
                {
                    log.LogError("Validate: failed with errors, aborting output generation");
                    log.Complete(false);
                    return false;
                }

                var generatedFiles = new List<string>();
                var graphOutputPath = Path.Combine(outputBasePath, graphName);

                // 5. Generate C# (if SO)
                if (formats.GenerateSO)
                {
                    log.LogInfo("Generate: creating C# classes...");
                    var soPath = Path.Combine(graphOutputPath, "SO");
                    var codeGen = new CodeGenerator();

                    var entriesResult = codeGen.GenerateEntries(graph);
                    if (entriesResult.IsFailure)
                    {
                        log.LogError($"Generate failed: {entriesResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var csPath = Path.Combine(soPath, $"{graphName}.cs");
                    PathUtilities.EnsureDirectory(csPath);
                    File.WriteAllText(csPath, entriesResult.Value);
                    generatedFiles.Add(csPath);

                    var dbResult = codeGen.GenerateDatabase(graph);
                    if (dbResult.IsFailure)
                    {
                        log.LogError($"Generate failed: {dbResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var dbCsPath = Path.Combine(soPath, $"{graphName}Database.cs");
                    File.WriteAllText(dbCsPath, dbResult.Value);
                    generatedFiles.Add(dbCsPath);

                    log.LogInfo($"Generate: SO/{graphName}.cs + SO/{graphName}Database.cs");
                }

                // 5b. Generate Blob structs (if Blob)
                if (formats.GenerateBlob)
                {
                    log.LogInfo("Generate: creating Blob structs...");
                    var blobPath = Path.Combine(graphOutputPath, "Blob");
                    var blobGenResult = InvokeBlobCodeGen(graph, blobPath, graphName);
                    if (blobGenResult.IsFailure)
                    {
                        log.LogError($"Blob generate failed: {blobGenResult.Error}");
                        log.Complete(false);
                        return false;
                    }
                    generatedFiles.AddRange(blobGenResult.Value);
                    log.LogInfo($"Generate: Blob/{graphName}Blob.cs + Blob/{graphName}BlobDatabase.cs + Blob/{graphName}BlobBuilder.cs");
                }

                // 5c. Generate Quantum AssetObject (if Quantum)
                if (formats.GenerateQuantum)
                {
                    log.LogInfo("Generate: creating Quantum AssetObject classes...");
                    var quantumGen = new CodeGen.QuantumCodeGenerator();
                    var quantumResult = quantumGen.Generate(graph);
                    if (quantumResult.IsFailure)
                    {
                        log.LogError($"Quantum generate failed: {quantumResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var quantumPath = $"Assets/QuantumUser/Simulation/DataGraph/{graphName}";
                    var quantumCsPath = Path.Combine(quantumPath, $"{graphName}QuantumDatabase.cs");
                    PathUtilities.EnsureDirectory(quantumCsPath);
                    File.WriteAllText(quantumCsPath, quantumResult.Value);
                    generatedFiles.Add(quantumCsPath);
                    log.LogInfo($"Generate: QuantumUser/Simulation/DataGraph/{graphName}/{graphName}QuantumDatabase.cs");
                }

                // 6. Serialize JSON
                if (formats.GenerateJSON)
                {
                    log.LogInfo("Serialize: writing JSON output...");
                    var jsonPath = Path.Combine(graphOutputPath, "JSON");
                    var jsonSerializer = new JsonDataSerializer();
                    var jsonResult = jsonSerializer.Serialize(dataTree);
                    if (jsonResult.IsFailure)
                    {
                        log.LogError($"Serialize failed: {jsonResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var jsonFilePath = Path.Combine(jsonPath, $"{graphName}.json");
                    PathUtilities.EnsureDirectory(jsonFilePath);
                    File.WriteAllText(jsonFilePath, jsonResult.Value);
                    generatedFiles.Add(jsonFilePath);

                    var schemaGen = new JsonSchemaGenerator();
                    var schemaResult = schemaGen.Generate(graph);
                    if (schemaResult.IsSuccess)
                    {
                        var schemaPath = Path.Combine(jsonPath, $"{graphName}.schema.json");
                        File.WriteAllText(schemaPath, schemaResult.Value);
                        generatedFiles.Add(schemaPath);
                    }

                    log.LogInfo($"Serialize: JSON/{graphName}.json + schema");
                }

                AssetDatabase.Refresh();

                log.LogSuccess($"Done: {generatedFiles.Count} file(s) generated");
                log.Complete(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                log.LogWarning("Cancelled by user");
                log.Complete(false);
                return false;
            }
            catch (Exception ex)
            {
                log.LogError($"Unexpected error: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        /// <summary>
        /// Runs only Adapt + Parse using cached data. For JSON Preview.
        /// Pass maxEntries=1 for first-element preview, 0 for full.
        /// </summary>
        public Result<ParsedDataTree> ParseFromCache(
            DataGraphAsset graphAsset,
            RawTableData cachedData,
            int maxEntries = 0)
        {
            var adaptResult = _adapter.ReadGraph(graphAsset);
            if (adaptResult.IsFailure)
                return Result<ParsedDataTree>.Failure(adaptResult.Error);

            return _parserEngine.Parse(cachedData, adaptResult.Value, maxEntries);
        }

        /// <summary>
        /// Creates SO .asset files from parsed data. Must be called after
        /// code generation and Unity compilation since it needs the generated
        /// types to exist at runtime.
        /// </summary>
        public async Task<bool> CreateSOAssetsAsync(
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken = default)
        {
            var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                ? graphAsset.GraphName
                : graphAsset.name;

            try
            {
                log.LogInfo("Adapt: reading graph structure...");
                var adaptResult = _adapter.ReadGraph(graphAsset);
                if (adaptResult.IsFailure)
                {
                    log.LogError($"Adapt failed: {adaptResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var graph = adaptResult.Value;

                log.LogInfo("Fetch: requesting data from sheet...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Parse: processing table data...");
                var parseResult = _parserEngine.Parse(fetchResult.Value, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Serialize: creating SO assets...");
                var soSerializer = new Serialization.SODataSerializer();
                var graphOutputPath = Path.Combine(outputBasePath, graphName, "SO");
                var soResult = soSerializer.Serialize(parseResult.Value, graph, graphOutputPath);
                if (soResult.IsFailure)
                {
                    log.LogError($"SO serialize failed: {soResult.Error}");
                    log.Complete(false);
                    return false;
                }

                AssetDatabase.Refresh();

                var soAsset = AssetDatabase.LoadAssetAtPath<DataGraph.Data.DataGraphDatabaseAsset>(soResult.Value);
                if (soAsset != null)
                    UpdateRegistry(registry => registry.RegisterSO(soAsset));

                log.LogSuccess($"Done: SO asset created at {soResult.Value}");
                log.Complete(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                log.LogWarning("Cancelled by user");
                log.Complete(false);
                return false;
            }
            catch (Exception ex)
            {
                log.LogError($"Unexpected error: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        /// <summary>
        /// Creates a .blob file by populating generated Source structs from
        /// ParsedDataTree, invoking the generated BlobBuilder, and writing
        /// the raw blob bytes to disk.
        /// </summary>
        public async Task<bool> CreateBlobAssetsAsync(
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken = default)
        {
            var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                ? graphAsset.GraphName
                : graphAsset.name;

            try
            {
                log.LogInfo("Adapt: reading graph structure...");
                var adaptResult = _adapter.ReadGraph(graphAsset);
                if (adaptResult.IsFailure)
                {
                    log.LogError($"Adapt failed: {adaptResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var graph = adaptResult.Value;

                log.LogInfo("Fetch: requesting data from source...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Parse: processing table data...");
                var parseResult = _parserEngine.Parse(fetchResult.Value, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Serialize: creating Blob asset...");
                var graphOutputPath = Path.Combine(outputBasePath, graphName, "Blob");

                var builderTypeName = $"DataGraph.Data.{graphName}BlobDatabaseBuilder";
                var builderType = TypeFinder.Find(builderTypeName);
                if (builderType == null)
                {
                    log.LogError($"Type '{builderTypeName}' not found. Run Parse with Blob enabled first.");
                    log.Complete(false);
                    return false;
                }

                var buildMethod = builderType.GetMethod("Build",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (buildMethod == null)
                {
                    log.LogError("Build method not found on generated BlobBuilder.");
                    log.Complete(false);
                    return false;
                }

                var sourceTypeName = $"DataGraph.Data.{graphName}BlobDatabaseBuilder+{RootTypeResolver.GetTypeName(graph.Root)}BlobSource";
                var sourceType = TypeFinder.Find(sourceTypeName);
                if (sourceType == null)
                {
                    sourceType = builderType.GetNestedType($"{RootTypeResolver.GetTypeName(graph.Root)}BlobSource");
                }
                if (sourceType == null)
                {
                    log.LogError($"Source type not found for '{graphName}'.");
                    log.Complete(false);
                    return false;
                }

                var buildParams = buildMethod.GetParameters();
                object[] buildArgs;
                var blobPopulator = new ReflectionPopulator(
                    new ReflectionCache(), new BlobConverter());

                if (parseResult.Value.Root is Domain.ParsedDictionary dict)
                {
                    var keyType = dict.KeyTypeName == "int" ? typeof(int) : typeof(string);
                    var keys = Array.CreateInstance(keyType, dict.Entries.Count);
                    var values = Array.CreateInstance(sourceType, dict.Entries.Count);

                    int idx = 0;
                    foreach (var kvp in dict.Entries)
                    {
                        var key = keyType == typeof(int) ? (object)Convert.ToInt32(kvp.Key) : kvp.Key.ToString();
                        keys.SetValue(key, idx);
                        var source = blobPopulator.Populate(sourceType, kvp.Value);
                        values.SetValue(source, idx);
                        idx++;
                    }

                    buildArgs = new object[] { keys, values };
                }
                else if (parseResult.Value.Root is Domain.ParsedArray arr)
                {
                    var values = Array.CreateInstance(sourceType, arr.Elements.Count);
                    for (int i = 0; i < arr.Elements.Count; i++)
                    {
                        var source = blobPopulator.Populate(sourceType, arr.Elements[i]);
                        values.SetValue(source, i);
                    }
                    buildArgs = new object[] { values };
                }
                else if (parseResult.Value.Root is Domain.ParsedObject obj)
                {
                    var source = blobPopulator.Populate(sourceType, obj);
                    buildArgs = new object[] { source };
                }
                else
                {
                    log.LogError("Unknown root type for Blob serialization.");
                    log.Complete(false);
                    return false;
                }

                var blobRef = buildMethod.Invoke(null, buildArgs);

                var blobFilePath = Path.Combine(graphOutputPath, $"{graphName}.blob");
                PathUtilities.EnsureDirectory(blobFilePath);

                var saveMethod = builderType.GetMethod("Save",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (saveMethod == null)
                {
                    log.LogError("Save method not found on generated BlobBuilder. Re-parse with Blob enabled.");
                    log.Complete(false);
                    return false;
                }
                saveMethod.Invoke(null, new[] { blobRef, blobFilePath });

                var disposeMethod = blobRef.GetType().GetMethod("Dispose");
                disposeMethod?.Invoke(blobRef, null);

                UnityEditor.AssetDatabase.Refresh();

                var blobFileName = $"{graphName}.blob";
                var asmName = builderType.Assembly.GetName().Name;
                UpdateRegistry(registry => registry.RegisterBlob(
                    $"{builderTypeName}, {asmName}", blobFileName));

                log.LogSuccess($"Done: Blob asset created at {blobFilePath}");
                log.Complete(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                log.LogWarning("Cancelled by user");
                log.Complete(false);
                return false;
            }
            catch (Exception ex)
            {
                log.LogError($"Unexpected error: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        /// <summary>
        /// Creates a Quantum AssetObject .asset file by populating
        /// the generated database class with FP-converted data.
        /// </summary>
        public async Task<bool> CreateQuantumAssetsAsync(
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken = default)
        {
            var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                ? graphAsset.GraphName
                : graphAsset.name;

            try
            {
                log.LogInfo("Adapt: reading graph structure...");
                var adaptResult = _adapter.ReadGraph(graphAsset);
                if (adaptResult.IsFailure)
                {
                    log.LogError($"Adapt failed: {adaptResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var graph = adaptResult.Value;

                log.LogInfo("Fetch: requesting data from source...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Parse: processing table data...");
                var parseResult = _parserEngine.Parse(fetchResult.Value, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo("Serialize: creating Quantum asset...");
                var dbTypeName = $"DataGraph.Data.{graphName}QuantumDatabase";
                var dbType = TypeFinder.Find(dbTypeName);
                if (dbType == null)
                {
                    log.LogError($"Type '{dbTypeName}' not found. Run Parse with Quantum enabled first.");
                    log.Complete(false);
                    return false;
                }

                var entryTypeName = $"DataGraph.Data.{RootTypeResolver.GetTypeName(graph.Root)}QuantumEntry";
                var entryType = TypeFinder.Find(entryTypeName);
                if (entryType == null)
                {
                    log.LogError($"Type '{entryTypeName}' not found.");
                    log.Complete(false);
                    return false;
                }

                var dbAsset = UnityEngine.ScriptableObject.CreateInstance(dbType);
                var entriesField = dbType.GetField("entries");
                if (entriesField == null)
                {
                    log.LogError("'entries' field not found on Quantum database.");
                    log.Complete(false);
                    return false;
                }

                var entriesList = entriesField.GetValue(dbAsset) as System.Collections.IList;
                if (entriesList == null)
                {
                    log.LogError("'entries' field is not a list.");
                    log.Complete(false);
                    return false;
                }

                var quantumReflectionCache = new ReflectionCache();
                var quantumPopulator = new ReflectionPopulator(
                    quantumReflectionCache, new QuantumConverter(quantumReflectionCache));

                object MakeQuantumEntry(Domain.ParsedNode node, object key)
                {
                    var instance = Activator.CreateInstance(entryType);
                    if (key != null)
                    {
                        var idField = quantumReflectionCache.GetField(entryType, "id");
                        if (idField != null)
                            idField.SetValue(instance,
                                ScalarConverter.Convert(idField.FieldType, key));
                    }
                    if (node is Domain.ParsedObject obj)
                        quantumPopulator.PopulateInto(entryType, obj, instance);
                    return instance;
                }

                switch (parseResult.Value.Root)
                {
                    case Domain.ParsedDictionary dict:
                        foreach (var kvp in dict.Entries)
                            entriesList.Add(MakeQuantumEntry(kvp.Value, kvp.Key));
                        break;
                    case Domain.ParsedArray arr:
                        foreach (var element in arr.Elements)
                            entriesList.Add(MakeQuantumEntry(element, null));
                        break;
                    case Domain.ParsedObject obj:
                        entriesList.Add(MakeQuantumEntry(obj, null));
                        break;
                }

                var quantumOutputPath = $"Assets/QuantumUser/Simulation/DataGraph/{graphName}";
                var assetPath = Path.Combine(quantumOutputPath, $"{graphName}QuantumDatabase.asset");
                PathUtilities.EnsureDirectory(assetPath);

                var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(assetPath);
                if (existing != null)
                    AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.CreateAsset(dbAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                log.LogSuccess($"Done: Quantum asset created at {assetPath}");
                log.Complete(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                log.LogWarning("Cancelled by user");
                log.Complete(false);
                return false;
            }
            catch (Exception ex)
            {
                log.LogError($"Unexpected error: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        // Reflection-based population of generated entry/source/Quantum
        // types now lives in Editor/Reflection (ReflectionPopulator +
        // {SO,Blob,Quantum}Converter). ParseGraphCommand only orchestrates
        // the pipeline.

        private static void UpdateRegistry(Action<DataGraph.Runtime.DataGraphRegistry> action)
        {
            var guids = AssetDatabase.FindAssets("t:DataGraphRegistry");
            DataGraph.Runtime.DataGraphRegistry registry;

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                registry = AssetDatabase.LoadAssetAtPath<DataGraph.Runtime.DataGraphRegistry>(path);
            }
            else
            {
                registry = UnityEngine.ScriptableObject.CreateInstance<DataGraph.Runtime.DataGraphRegistry>();
                var registryPath = "Assets/DataGraph/DataGraphRegistry.asset";
                PathUtilities.EnsureDirectory(registryPath);
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            if (registry == null) return;

            action(registry);
            registry.CleanUp();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Shortened pipeline for Enum/Flag graphs: Fetch -> Parse -> Generate enum C#.
        /// No SO/Blob/Quantum/JSON output — only a single .cs file with the enum definition.
        /// </summary>
        private async Task<bool> ExecuteEnumPipelineAsync(
            ParseableGraph graph,
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken)
        {
            var graphName = graph.GraphName;
            var isFlag = graph.Root is ParseableFlagRoot;
            var kind = isFlag ? "Flag" : "Enum";

            try
            {
                log.LogInfo($"Fetch: requesting data for {kind} '{graphName}'...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo($"Fetch: {fetchResult.Value.RowCount} rows");

                log.LogInfo($"Parse: extracting {kind} members...");
                var parseResult = _parserEngine.Parse(fetchResult.Value, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                foreach (var warning in parseResult.Value.ParseWarnings)
                    log.LogWarning($"Parse: {warning.Message}");

                var enumDef = parseResult.Value.Root as ParsedEnumDefinition;
                if (enumDef == null)
                {
                    log.LogError("Parse produced unexpected result type.");
                    log.Complete(false);
                    return false;
                }

                log.LogInfo($"Parse: {enumDef.Members.Count} members found");

                log.LogInfo($"Generate: creating {kind} source...");
                var codeGen = new CodeGen.CodeGenerator();
                var genResult = codeGen.GenerateEnum(enumDef);
                if (genResult.IsFailure)
                {
                    log.LogError($"Generate failed: {genResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var subfolder = isFlag ? "Flags" : "Enums";
                string csPath;

                if (ProviderRegistry.IsQuantumAvailable())
                    csPath = $"Assets/QuantumUser/Simulation/DataGraph/{subfolder}/{enumDef.TypeName}.cs";
                else
                    csPath = Path.Combine(outputBasePath, subfolder, $"{enumDef.TypeName}.cs");

                PathUtilities.EnsureDirectory(csPath);
                File.WriteAllText(csPath, genResult.Value);
                log.LogSuccess($"Generated: {csPath} ({enumDef.Members.Count} members)");

                AssetDatabase.Refresh();
                log.Complete(true);
                return true;
            }
            catch (Exception ex)
            {
                log.LogError($"{kind} pipeline failed: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        /// <summary>
        /// Invokes BlobCodeGenerator through reflection since DataGraph.Editor
        /// does not reference DataGraph.Blob directly.
        /// </summary>
        private static Result<List<string>> InvokeBlobCodeGen(
            ParseableGraph graph, string outputPath, string graphName)
        {
            try
            {
                var gen = new CodeGen.BlobCodeGenerator();
                var files = new List<string>();

                var entriesResult = gen.GenerateEntries(graph);
                if (entriesResult.IsFailure)
                    return Result<List<string>>.Failure(entriesResult.Error);

                var blobCsPath = Path.Combine(outputPath, $"{graphName}Blob.cs");
                PathUtilities.EnsureDirectory(blobCsPath);
                File.WriteAllText(blobCsPath, entriesResult.Value);
                files.Add(blobCsPath);

                var dbResult = gen.GenerateDatabase(graph);
                if (dbResult.IsFailure)
                    return Result<List<string>>.Failure(dbResult.Error);

                var dbCsPath = Path.Combine(outputPath, $"{graphName}BlobDatabase.cs");
                File.WriteAllText(dbCsPath, dbResult.Value);
                files.Add(dbCsPath);

                var builderResult = gen.GenerateBuilder(graph);
                if (builderResult.IsFailure)
                    return Result<List<string>>.Failure(builderResult.Error);

                var builderCsPath = Path.Combine(outputPath, $"{graphName}BlobBuilder.cs");
                File.WriteAllText(builderCsPath, builderResult.Value);
                files.Add(builderCsPath);

                return Result<List<string>>.Success(files);
            }
            catch (Exception ex)
            {
                return Result<List<string>>.Failure($"Blob code generation failed: {ex.Message}");
            }
        }
    }
}
