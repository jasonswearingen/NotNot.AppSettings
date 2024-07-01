using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NotNot.AppSettings;

[assembly: InternalsVisibleTo("NotNot.AppSettings.Tests")]

namespace NotNot;

/// <summary>
/// generate settings classes from appsettings.json files.  These will be namespaced as [TargetProjectNamespaceRoot].AppSettings.[ConfigName]
/// </summary>
[Generator]
internal class AppSettingsGen : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
#if DEBUG
		//////if enabled, this will allow you to attach and debug sourcegen when building the target project.
		//if (!Debugger.IsAttached)
		//{
		//	Debugger.Launch();
		//}
#endif


		/////////////  NEW ADDITIONAL FILES WORKFLOW
		{

			//get appsettings*.json via AdditionalFiles
			var regex = new Regex(@"\\appsettings\..*json$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
			var additionalFiles = context.AdditionalTextsProvider.Where(file =>
			{
				var result = regex.IsMatch(file.Path);
				return result;
			});


			// Transform additionalFiles to a single Dictionary entry
			var combinedSourceTextsProvider = additionalFiles.Collect().Select((files, ct) =>
			{
				//Debug.WriteLine($"additionalFiles = {files.Length}");
				var combinedFiles = new Dictionary<string, SourceText>();
				foreach (var file in files)
				{

					//Debug.WriteLine($"file = {file.Path}");
					var sourceText = file.GetText(ct);
					if (sourceText != null)
					{
						combinedFiles[file.Path] = sourceText;
					}
				}

				return combinedFiles;
			});


			var namespaceProvider = context.AnalyzerConfigOptionsProvider
				.Select(static (provider, ct) =>
				{
					provider.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace);
					return rootNamespace;
				});

			var combinedProvider = namespaceProvider.Combine(combinedSourceTextsProvider);



			context.RegisterSourceOutput(
				combinedProvider,
				(spc, content) =>
				{
					var rootNamespace = content.Left;
					var combinedSourceTexts = content.Right;

					ExecuteGenerator(spc, rootNamespace, combinedSourceTexts);
				});

		}
		
		////////////////  OLD FILE.IO WORKFLOW  Works but frowned upon for sourcegen.  Switched to SourceText
		//{
		//	var projectDirProvider = context.AnalyzerConfigOptionsProvider
		//		 .Select(static (provider, ct) =>
		//		 {
		//			 provider.GlobalOptions.TryGetValue("build_property.projectdir", out string? projectDirectory);
		//			 provider.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? assemblyName);
		//			 return (projectDirectory, assemblyName);
		//		 });
		//	context.RegisterSourceOutput(
		//		projectDirProvider,
		//		 (spc, settings) =>
		//		 {
		//			 ExecuteGenerator_FileIo(spc, settings);
		//		 });
		//}
	}

	public void ExecuteGenerator(SourceProductionContext spc, string? rootNamespace, Dictionary<string, SourceText> combinedSourceTexts)
	{
		var diagReports = new List<Diagnostic>();

		var results = GenerateSourceFiles(rootNamespace, combinedSourceTexts, diagReports);

		foreach (var report in diagReports)
		{
			spc.ReportDiagnostic(report);
		}
		foreach (var result in results)
		{
			spc.AddSource(result.Key, result.Value);
		}
		spc._Info("done");
	}
	
	/// <summary>
	/// will generate strongly typed c# classes for each matched (appsettings).json
	/// </summary>
	/// <param name="diagReport">helper for accumulating diag messages.  caller should relay them to appropriate log writer afterwards.</param>
	/// <param name="appSettingsJsonSourceFiles">the appsettings.json files</param>
	/// <param name="rootNamespace">{rootNamespace}.AppSettingsGen is used as the namespace of generated files</param>
	/// <returns>the "output" C#, source generated files</returns>
	public Dictionary<string, SourceText> GenerateSourceFiles(string? rootNamespace, Dictionary<string, SourceText> appSettingsJsonSourceFiles, List<Diagnostic> diagReport)
	{

		var toReturn = new Dictionary<string, SourceText>();

		//if (rootNamespace is null)
		//{
		//	diagReport._Error($"missing required inputs. rootNamespace={rootNamespace}");
		//	return toReturn;
		//}
		if (appSettingsJsonSourceFiles.Count == 0)
		{
			diagReport._Error($"No appSettings.json files were found in your project.  SourceGen aborted.");
			return toReturn;
		}

		diagReport._Info($"rootNamespace={rootNamespace}, appSettingsJsonSourceFiles.Count={appSettingsJsonSourceFiles.Count}");


		var startingNamespace = string.IsNullOrWhiteSpace(rootNamespace)?"AppSettingsGen" : $"{rootNamespace}.AppSettingsGen";


		//merge into one big json
		var allJsonDict = JsonMerger.MergeJsonFiles(appSettingsJsonSourceFiles, diagReport);

		//generate classes for the entire json hiearchy
		GenerateFilesWorker(diagReport, toReturn, allJsonDict, "AppSettings", $"{startingNamespace}");

		AddBinderShims(diagReport, toReturn, startingNamespace);

		return toReturn;

	}
	
	/// <summary>
	/// add helper service to automatically populate appsettings from disk
	/// </summary>
	/// <param name="diagReport"></param>
	/// <param name="toReturn"></param>
	/// <param name="startingNamespace"></param>
	private void AddBinderShims(List<Diagnostic> diagReport, Dictionary<string, SourceText> toReturn, string startingNamespace)
	{
		var builder = new StringBuilder();
		builder.Append(@$"
/** 
 * This file is generated by the NotNot.AppSettings nuget package.
 * Do not edit this file directly, instead edit the appsettings.json files and rebuild the project.
**/

using Microsoft.Extensions.Configuration;
namespace {startingNamespace};


/// <summary>
/// Strongly typed AppSettings.json, recreated every build. 
/// <para>You can use this directly, extend it (it's a partial class), 
/// or get a populated instance of it via the <see cref=""AppSettingsBinder""/> DI service</para>
/// </summary>
public partial class AppSettings
{{
}}

/// <summary>
/// a DI service that contains a strongly-typed copy of your appsettings.json
/// <para><strong>DI Usage:</strong></para>
/// <para><c>builder.Services.AddSingleton&lt;IAppSettingsBinder, AppSettingsBinder&gt;();</c></para>
/// <para><c>var app = builder.Build();</c></para>
///  <para><c>var appSettings = app.Services.GetRequiredService&lt;IAppSettingsBinder&gt;().AppSettings;</c></para>
/// <para><strong>Non-DI Usage:</strong></para>
/// <para><c>var appSettings = AppSettingsBinder.LoadDirect();</c></para>
/// </summary>
public partial class AppSettingsBinder : IAppSettingsBinder
{{
	public AppSettings AppSettings {{ get; protected set; }}

	public AppSettingsBinder(IConfiguration _config)
	{{
		AppSettings = new AppSettings();

		//automatically reads and binds to config file
		_config.Bind(AppSettings);
	}}

	/// <summary>
	/// Manually construct an AppSettings from your appsettings.json files.
	/// <para>NOTE: This method is provided for non-DI users.  If you use DI, don't use this method.  Instead just register this class as a service.</para>
	/// </summary>
	/// <param name=""appSettingsLocation"">folder where to search for appsettings.json.  defaults to current app folder.</param>
	/// <param name=""appSettingsFileNames"">lets you override the files to load up.  defaults to 'appsettings.json' and 'appsettings.{{DOTNET_ENVIRONMENT}}.json'</param>
	/// <param name=""throwIfFilesMissing"">default is to silently ignore if any of the .json files are missing.</param>
	/// <returns>your strongly typed appsettings with values from your .json loaded in</returns>
	public static AppSettings LoadDirect(string? appSettingsLocation = null,IEnumerable<string>? appSettingsFileNames=null,bool throwIfFilesMissing=false )
	{{      
		//pick what .json files to load
		if (appSettingsFileNames is null)
		{{
			//figure out what env
			var env = Environment.GetEnvironmentVariable(""ASPNETCORE_ENVIRONMENT"");
			env ??= Environment.GetEnvironmentVariable(""DOTNET_ENVIRONMENT"");
			env ??= Environment.GetEnvironmentVariable(""ENVIRONMENT"");
			//env ??= ""Development""; //default to ""Development
			if (env is null)
			{{
				appSettingsFileNames = new[] {{ ""appsettings.json"" }};
			}}
			else
			{{
				appSettingsFileNames = new[] {{ ""appsettings.json"", $""appsettings.{{env}}.json"" }};
			}}
		}}

		//build a config from the specified files
		var builder = new ConfigurationBuilder();
		if (appSettingsLocation != null)
		{{
			builder.SetBasePath(appSettingsLocation);
		}}
		var optional = !throwIfFilesMissing;
		foreach (var fileName in appSettingsFileNames)
		{{         
			builder.AddJsonFile(fileName, optional: optional, reloadOnChange: false); // Add appsettings.json
		}}
		IConfigurationRoot configuration = builder.Build();

		//now finally get the appsettings we care about
		var binder = new AppSettingsBinder(configuration);
		return binder.AppSettings;
	}}

	/// <summary>
	/// helper to create an AppSettings from a string containing your json
	/// </summary>
	/// <param name=""appSettingsJsonText""></param>
	/// <returns></returns>
	public static AppSettings LoadDirectFromText(string appSettingsJsonText)
	{{
	

		//build a config from the specified files
		var builder = new ConfigurationBuilder();

		var configurationBuilder = new ConfigurationBuilder();

		IConfigurationRoot configuration;
		using (var stream = new MemoryStream())
		{{
			using (var writer = new StreamWriter(stream))
			{{
				writer.Write(appSettingsJsonText);
				writer.Flush();
				stream.Position = 0;
				configurationBuilder.AddJsonStream(stream);



				configuration = configurationBuilder.Build();
			}}
		}}


		//now finally get the appsettings we care about
		var binder = new AppSettingsBinder(configuration);
		return binder.AppSettings;
	}}

	/// <summary>
	/// helper to create an AppSettings from strings containing your json
	/// </summary>
	/// <param name=""appSettingsJsonText""></param>
	/// <returns></returns>
	public static AppSettings LoadDirectFromTexts(params string[] appSettingsJsonTexts)
	{{

		//build a config from the specified files
		var configurationBuilder = new ConfigurationBuilder();

		IConfigurationRoot RecursiveLoader(Queue<string> textsQueue)
		{{
			using var stream = new MemoryStream();
			using var writer = new StreamWriter(stream);

			if (textsQueue.Count > 0)
			{{
				var appSettingsJsonText = textsQueue.Dequeue();
				writer.Write(appSettingsJsonText);
				writer.Flush();
				stream.Position = 0;
				configurationBuilder.AddJsonStream(stream);

				return RecursiveLoader(textsQueue);
			}}
			else
			{{
				return configurationBuilder.Build();
			}}
		}}


		var configuration = RecursiveLoader(new Queue<string>(appSettingsJsonTexts));


		//now finally get the appsettings we care about
		var binder = new AppSettingsBinder(configuration);
		return binder.AppSettings;
	}}
}}

/// <summary>
/// a DI service that contains a strongly-typed copy of your appsettings.json
/// <para><strong>DI Usage:</strong></para>
/// <para><c>builder.Services.AddSingleton&lt;IAppSettingsBinder, AppSettingsBinder&gt;();</c></para>
/// <para><c>var app = builder.Build();</c></para>
///  <para><c>var appSettings = app.Services.GetRequiredService&lt;IAppSettingsBinder&gt;().AppSettings;</c></para>
/// <para><strong>Non-DI Usage:</strong></para>
/// <para><c>var appSettings = AppSettingsBinder.LoadDirect();</c></para>
/// </summary>
public interface IAppSettingsBinder
{{
	public AppSettings AppSettings {{ get; }}
}}
");

		var source = SourceText.From(builder.ToString(), Encoding.UTF8);
		toReturn.Add("_BinderShims.cs", source);

	}

	/// <summary>
	/// get the c# type of the element.  however keep in mind that arrays won't include the '[]'.  Check if array via `elm.ValueKind == JsonValueKind.Array`
	/// </summary>
	/// <param name="elm"></param>
	/// <param name="currentName"></param>
	/// <param name="currentNamespace"></param>
	/// <returns>returns name of json primitive, "object" for null/undefined nodes,  named nodes for other json objects</returns>
	/// <exception cref="ArgumentException"></exception>
	public string GetSourceTypeName(JsonElement elm, string currentName, string currentNamespace)
	{
		string toReturn;
		switch (elm.ValueKind)
		{
			case JsonValueKind.String:
				toReturn = "string";
				break;
			case JsonValueKind.Number:
				toReturn = "double";
				break;
			case JsonValueKind.True:
			case JsonValueKind.False:
				toReturn = "bool";
				break;
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				toReturn = "object";
				break;
			case JsonValueKind.Object:
				toReturn = $"{currentNamespace}.{currentName}";
				break;
			case JsonValueKind.Array:
				//unify all children into one type
				//if mix of various types (such as object+primitive, or different primitive types), will return back "object" and user will have to cast manually.
				string? unifiedChildType = null;
				foreach (var child in elm.EnumerateArray())
				{
					var childType = GetSourceTypeName(child, currentName, currentNamespace);
					if (unifiedChildType is null)
					{
						unifiedChildType = childType;
					}
					else if (unifiedChildType != childType)
					{
						unifiedChildType = "object";
						break;
					}
				}
				unifiedChildType ??= "object";
				toReturn = unifiedChildType;
				break;
			default:
				throw new ArgumentException($"unknown type returned from json, {elm}", nameof(elm));
		}

		return toReturn;
	}


	/// <summary>
	/// generate files for the given json hierarchy, recursively calling itself for each child node
	/// </summary>
	protected void GenerateFilesWorker(List<Diagnostic> diagReport, Dictionary<string, SourceText> generatedSourceFiles, Dictionary<string, JsonElement> currentNode, string currentNodeName, string currentNamespace)
	{
		//build currentNode into file
		var currentClassName = currentNodeName._ConvertToAlphanumericCaps();
		var filename = $"{currentNamespace}.{currentClassName}.cs";

		var propertyBuilder = new StringBuilder();
		foreach (var kvp in currentNode)
		{
			var propertyName = kvp.Key._ConvertToAlphanumericCaps();
			var propertyNamespace = $"{currentNamespace}._{currentClassName}";
			var valueType = GetSourceTypeName(kvp.Value, propertyName, propertyNamespace);
			if (kvp.Value.ValueKind == JsonValueKind.Array)
			{
				valueType += "[]";
			}
			propertyBuilder.Append($"   public {valueType}? {propertyName}{{get; set;}}\n");
		}

		var sourceBuilder = new StringBuilder();
		sourceBuilder.Append(@$"
/** 
 * This file is generated by the NotNot.AppSettings nuget package.
 * Do not edit this file directly, instead edit the appsettings.json files and rebuild the project.
**/
using System;
using System.Runtime.CompilerServices;
namespace {currentNamespace};

[CompilerGenerated]
public partial class {currentClassName} {{
{propertyBuilder}
}}
");

		var source = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
		generatedSourceFiles.Add(filename, source);

		//recurse into children
		foreach (var kvp in currentNode)
		{
			var propertyNamespace = $"{currentNamespace}._{currentClassName}";
			var jsonKind = kvp.Value.ValueKind;
			var propertyName = kvp.Key._ConvertToAlphanumericCaps();
			switch (jsonKind)
			{
				case JsonValueKind.Object:
					{
						var childNode = kvp.Value.Deserialize<Dictionary<string, JsonElement>>(JsonMerger._serializerOptions)!;
						GenerateFilesWorker(diagReport, generatedSourceFiles, childNode, propertyName, propertyNamespace);
					}
					break;
				case JsonValueKind.Array:
					{

						var childNodes = kvp.Value.Deserialize<List<JsonElement>>(JsonMerger._serializerOptions)!;
						//get name of node
						var arrayTypeName = GetSourceTypeName(kvp.Value, propertyName, propertyNamespace);
						switch (arrayTypeName)
						{
							case "string":
							case "double":
							case "bool":
							case "object": //returns object for null/undefined nodes.  (named nodes for other objects)
												//no need to recurse
								break;
							default:
								//squash children into singular object then generate for it
								var squashedChildren = new Dictionary<string, JsonElement>();
								foreach (var child in childNodes)
								{
									JsonMerger.MergeJson(squashedChildren, child);
								}
								GenerateFilesWorker(diagReport, generatedSourceFiles, squashedChildren, propertyName, propertyNamespace);
								break;
						}

					}
					break;
				default:
					//a "primitive" json type, so no need to recurse
					break;
			}
		}

	}


	//[Obsolete("uses File.IO to read.  Works but frowned upon for sourcegen.  Switched to SourceText", true)]
	//public void ExecuteGenerator_FileIo(SourceProductionContext spc, (string? projectDirectory, string? startingNamespace) settings)
	//{
	//	var diagReports = new List<Diagnostic>();
	//	var results = GenerateSourceFiles_FileIo(settings, diagReports);
	//	foreach (var report in diagReports)
	//	{
	//		spc.ReportDiagnostic(report);
	//	}
	//	foreach (var result in results)
	//	{
	//		spc.AddSource(result.Key, result.Value);
	//	}
	//	spc._Info("done");
	//}


	///// <summary>
	///// for the given fileSearchPattern, will generate strongly typed c# classes for each matched (appsettings).json file found in the projectDirectory
	///// </summary>
	///// <param name="settings"></param>
	///// <param name="diagReport">helper for accumulating diag messages.  caller should relay them to appropriate log writer afterwards.</param>
	///// <param name="fileSearchPattern">defaults to "appsettings*.json"</param>
	///// <returns></returns>
	//[Obsolete("uses File.IO to read.  Works but frowned upon for sourcegen.  Switched to SourceText", true)]
	//public Dictionary<string, SourceText> GenerateSourceFiles_FileIo((string? projectDirectory
	//	, string? startingNamespace) settings, List<Diagnostic> diagReport
	//	, string fileSearchPattern = "appsettings*.json")
	//{
	//	var (projectDir, startingNamespace) = settings;
	//	var toReturn = new Dictionary<string, SourceText>();
	//	if (projectDir is null || startingNamespace is null)
	//	{
	//		diagReport._Error($"null required inputs  projectDir={projectDir}, startingNamespace={startingNamespace}");
	//		return toReturn;
	//	}
	//	else
	//	{
	//		diagReport._Info($"projectDir {projectDir} ");
	//	}
	//	startingNamespace = $"{startingNamespace}.AppSettingsGen";
	//	//do stuff with project dir
	//	var dir = new DirectoryInfo(projectDir);
	//	var files = dir.EnumerateFiles(fileSearchPattern, SearchOption.TopDirectoryOnly).ToList();
	//	diagReport._Info($"files count {files.Count()} ");
	//	//merge into one big json
	//	var allJsonDict = JsonMerger.MergeJsonFiles(files, diagReport);
	//	//generate classes for the entire json hiearchy
	//	GenerateFilesWorker(diagReport, toReturn, allJsonDict, "AppSettings", $"{startingNamespace}");
	//	AddBinderShims(diagReport, toReturn, startingNamespace);
	//	return toReturn;
	//}

}