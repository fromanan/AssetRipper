using AsmResolver.DotNet;
using AssetRipper.Assets;
using AssetRipper.Assets.Export;
using AssetRipper.Export.UnityProjects.Scripts.AssemblyDefinitions;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files.Utils;
using AssetRipper.SourceGenerated.Classes.ClassID_1050;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Plugin;
using System.Diagnostics;

namespace AssetRipper.Export.UnityProjects.Scripts;

public sealed class ScriptExportCollection : ScriptExportCollectionBase
{
	public override string Name => nameof(ScriptExportCollection);

	private readonly List<IMonoScript> m_export = [];
	
	private readonly Dictionary<IUnityObjectBase, IMonoScript> m_scripts = new();
	
	public ScriptExportCollection(ScriptExporter assetExporter, IMonoScript firstScript) : base(assetExporter, firstScript)
	{
		Debug.Assert(assetExporter.AssemblyManager.IsSet);

		// find copies in whole project and skip them
		Dictionary<MonoScriptInfo, IMonoScript> uniqueDictionary = new();
		foreach (IMonoScript assetScript in firstScript.Collection.Bundle.FetchAssetsInHierarchy().OfType<IMonoScript>())
		{
			MonoScriptInfo info = MonoScriptInfo.From(assetScript);
			if (uniqueDictionary.TryGetValue(info, out IMonoScript? uniqueScript))
			{
				m_scripts.Add(assetScript, uniqueScript);
			}
			else
			{
				m_scripts.Add(assetScript, assetScript);
				uniqueDictionary.Add(info, assetScript);
				if (ShouldExport(assetScript))
				{
					m_export.Add(assetScript);
				}
			}
		}
	}

	private bool ShouldExport(IMonoScript script)
	{
		return AssetExporter.GetExportType(script) is AssemblyExportType.Decompile && script.IsScriptPresents(AssetExporter.AssemblyManager);
	}

	public override bool Export(IExportContainer container, string projectDirectory)
	{
		Logger.Info(LogCategory.Export, "Exporting scripts...");

		string assetsDirectoryPath = Path.Combine(projectDirectory, AssetsKeyword);

		Dictionary<string, AssemblyDefinitionDetails> assemblyDefinitionDetailsDictionary = new();

		string pluginsFolder = Path.Combine(assetsDirectoryPath, "Plugins");

		foreach (AssemblyDefinition assembly in AssetExporter.AssemblyManager.GetAssemblies())
		{
			string assemblyName = assembly.Name!;
			AssemblyExportType exportType = AssetExporter.GetExportType(assemblyName);

			if (exportType is AssemblyExportType.Decompile)
			{
				Logger.Info(LogCategory.Export, $"Decompiling {assemblyName}");
				string outputDirectory = Path.Combine(assetsDirectoryPath, GetScriptsFolderName(assemblyName), assemblyName);
				Directory.CreateDirectory(outputDirectory);
				AssetExporter.Decompiler.DecompileWholeProject(assembly, outputDirectory);

				assemblyDefinitionDetailsDictionary.TryAdd(assemblyName, new AssemblyDefinitionDetails(assembly, outputDirectory));
			}
			else if (exportType is AssemblyExportType.Save)
			{
				Logger.Info(LogCategory.Export, $"Saving {assemblyName}");
				Directory.CreateDirectory(pluginsFolder);
				string outputPath = Path.Combine(pluginsFolder, FilenameUtils.AddAssemblyFileExtension(assemblyName));
				AssetExporter.AssemblyManager.SaveAssembly(assembly, outputPath);
				OnAssemblyExported(container, outputPath);
			}
		}

		foreach (IMonoScript asset in m_export)
		{
			GetExportSubPath(asset, out string subFolderPath, out string fileName);
			string folderPath = Path.Combine(assetsDirectoryPath, subFolderPath);
			string filePath = Path.Combine(folderPath, fileName);
			if (!System.IO.File.Exists(filePath))
			{
				Directory.CreateDirectory(folderPath);
				System.IO.File.WriteAllText(filePath, EmptyScript.GetContent(asset));
				string assemblyName = asset.GetAssemblyNameFixed();
				if (!assemblyDefinitionDetailsDictionary.ContainsKey(assemblyName))
				{
					string assemblyDirectoryPath = Path.Combine(assetsDirectoryPath, GetScriptsFolderName(assemblyName), assemblyName);
					AssemblyDefinitionDetails details = new(assemblyName, assemblyDirectoryPath);
					assemblyDefinitionDetailsDictionary.Add(assemblyName, details);
				}
			}

			if (System.IO.File.Exists($"{filePath}.meta"))
			{
				Logger.Error(LogCategory.Export, $"Metafile already exists at {filePath}.meta");
			}
			else
			{
				OnScriptExported(container, asset, filePath);
			}
		}

		// assembly definitions were added in 2017.3
		//     see: https://blog.unity.com/technology/unity-2017-3b-feature-preview-assembly-definition-files-and-transform-tool
		if (assemblyDefinitionDetailsDictionary.Count > 0 && container.ExportVersion.GreaterThanOrEquals(2017, 3))
		{
			var assemblies = assemblyDefinitionDetailsDictionary.Values.Where(details =>
				!ReferenceAssemblies.IsPredefinedAssembly(details.AssemblyName));
			foreach (AssemblyDefinitionDetails details in assemblies)
			{
				AssemblyDefinitionExporter.Export(details);
			}
		}

		return true;
	}

	private void OnAssemblyExported(IExportContainer container, string path)
	{
		UnityGuid guid = ScriptHashing.CalculateAssemblyGuid(Path.GetFileName(path));
		IPluginImporter importer = PluginImporter.Create(Assets.First().Collection, container.ExportVersion);
		if (importer.HasPlatformData())
		{
			PlatformSettingsData_Plugin anyPlatformSettings = importer.AddPlatformSettings("Any", Utf8String.Empty);
			anyPlatformSettings.Enabled = true;

			PlatformSettingsData_Plugin editorPlatformSettings = importer.AddPlatformSettings("Editor", "Editor");
			editorPlatformSettings.Enabled = false;
			editorPlatformSettings.Settings.Add("DefaultValueInitialized", "true");
		}
		
		Meta meta = new(guid, importer);
		ExportMeta(container, meta, path);
	}
}
