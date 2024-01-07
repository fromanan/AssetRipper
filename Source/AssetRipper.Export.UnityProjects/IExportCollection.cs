﻿using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Export;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Files.SerializedFiles;

namespace AssetRipper.Export.UnityProjects
{
	public interface IExportCollection
	{
		/// <summary>
		/// Export the assets in this collection.
		/// </summary>
		/// <param name="container"></param>
		/// <param name="projectDirectory">The directory containing the whole project including Assets and ProjectSettings.</param>
		/// <returns>True if export was successful.</returns>
		bool Export(IExportContainer container, string projectDirectory);
		/// <summary>
		/// Is the asset part of this collection?
		/// </summary>
		bool Contains(IUnityObjectBase asset);
		/// <summary>
		/// Get the export ID of the asset.
		/// </summary>
		long GetExportID(IUnityObjectBase asset);
		MetaPtr CreateExportPointer(IUnityObjectBase asset, bool isLocal);

		AssetCollection File { get; }
		TransferInstructionFlags Flags { get; }
		IEnumerable<IUnityObjectBase> Assets { get; }
		IEnumerable<IUnityObjectBase> ExportableAssets => Assets;
		string Name { get; }
	}
}