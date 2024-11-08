// https://github.com/LostBeard/BlazorWASMScriptLoader/blob/main/BlazorWASMScriptLoader/Services/ScriptLoaderService.cs

using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Text;

namespace Client
{
    // requires "Microsoft.CodeAnalysis.CSharp"
    // can be added via nuget
    public class ScriptLoaderService
    {
        HttpClient _httpClient = new HttpClient();

        public ScriptLoaderService(NavigationManager navigationManager)
        {
            _httpClient.BaseAddress = new Uri(navigationManager.BaseUri);
        }

        Dictionary<string, MetadataReference> MetadataReferenceCache = new Dictionary<string, MetadataReference>();
        async Task<MetadataReference> GetAssemblyMetadataReference(Assembly assembly)
        {
            MetadataReference? ret = null;
            var assemblyName = assembly.GetName().Name;
            if (MetadataReferenceCache.TryGetValue(assemblyName, out ret)) return ret;
            var assemblyUrl = $"./_framework/{assemblyName}.dll";
            try
            {
                var tmp = await _httpClient.GetAsync(assemblyUrl);
                if (tmp.IsSuccessStatusCode)
                {
                    var bytes = await tmp.Content.ReadAsByteArrayAsync();
                    ret = MetadataReference.CreateFromImage(bytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"metadataReference not loaded: {assembly} {ex.Message}");
            }
            if (ret == null) throw new Exception("ReferenceMetadata nto found. If using .Net 8, <WasmEnableWebcil>false</WasmEnableWebcil> must be set in the project .csproj file.");
            MetadataReferenceCache[assemblyName] = ret;
            return ret;
        }

        private static string[] GetUsings(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();
            
            var usings = root.Usings
                            .Select(usingDirective => usingDirective.Name.ToString())
                            .ToArray();
            
            return usings;
        }

        private static string GetAssemblyName(string namespaceName)
        {
            Console.WriteLine($"Namespace: {namespaceName}");

            // A manual example, consider using a more sophisticated approach.
            switch (namespaceName)
            {
                case "AElf.Sdk.CSharp":
                case "AElf.Sdk.CSharp.State":
                    return "AElf.Sdk.CSharp";
                case "Google.Protobuf.WellKnownTypes":
                    return "Google.Protobuf";
                default:
                    throw new ArgumentException($"Unknown namespace: {namespaceName}");
            }
        }

        private static Assembly LoadAssembly(string assemblyName)
        {
            try
            {
                return Assembly.Load(new AssemblyName(assemblyName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading assembly: {ex.Message}");
                return null;
            }
        }

        public async Task<string> CompileToDLLString(string sourceCode, string assemblyName = "", bool release = true, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
        {
            if (string.IsNullOrEmpty(assemblyName)) assemblyName = Path.GetRandomFileName();
            var codeString = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp11).WithKind(sourceCodeKind);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

            var references = new List<MetadataReference>();

            var appAssemblies = Assembly.GetEntryAssembly()!.GetReferencedAssemblies().Select(o => Assembly.Load(o)).ToList();
            appAssemblies.Add(typeof(object).Assembly);
            foreach (var assembly in appAssemblies)
            {
                var metadataReference = await GetAssemblyMetadataReference(assembly);
                references.Add(metadataReference);
            }

            var usings = GetUsings(sourceCode);
            foreach (var ns in usings)
            {
                var _assemblyName = GetAssemblyName(ns);
                if (_assemblyName != null)
                {
                    var assembly = LoadAssembly(_assemblyName);
                    var metadataReference = await GetAssemblyMetadataReference(assembly);
                    references.Add(metadataReference);
                }
            }

            CSharpCompilation compilation;
            if (sourceCodeKind == SourceCodeKind.Script)
            {
                compilation = CSharpCompilation.CreateScriptCompilation(
                assemblyName,
                syntaxTree: parsedSyntaxTree,
                references: references,
                options: new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        concurrentBuild: false,
                        optimizationLevel: release ? OptimizationLevel.Release : OptimizationLevel.Debug
                    )
                );
            }
            else
            {
                compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { parsedSyntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        concurrentBuild: false,
                        optimizationLevel: release ? OptimizationLevel.Release : OptimizationLevel.Debug
                    )
                );
            }
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);
                if (!result.Success)
                {
                    var errors = new StringBuilder();
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (Diagnostic diagnostic in failures)
                    {
                        var startLinePos = diagnostic.Location.GetLineSpan().StartLinePosition;
                        var err = $"Line: {startLinePos.Line} Col:{startLinePos.Character} Code: {diagnostic.Id} Message: {diagnostic.GetMessage()}";
                        errors.AppendLine(err);
                        Console.Error.WriteLine(err);
                    }
                    throw new Exception(errors.ToString());
                }
                else
                {
                    // ms.Seek(0, SeekOrigin.Begin);
                    // var assembly = Assembly.Load(ms.ToArray());
                    // return assembly;
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }
}