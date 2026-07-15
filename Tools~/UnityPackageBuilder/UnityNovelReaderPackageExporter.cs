using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class UnityNovelReaderPackageExporter
{
    private const string OutputEnvironmentVariable = "UNITY_NOVEL_READER_PACKAGE_OUTPUT";
    private const string AssetPath = "Assets/UnityNovelReader.Editor.dll";

    public static void Export()
    {
        string outputPath = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(
                $"Environment variable {OutputEnvironmentVariable} is required.");
        }

        outputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new InvalidOperationException("The package output directory is invalid.");
        }

        Directory.CreateDirectory(outputDirectory);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        PluginImporter importer = AssetImporter.GetAtPath(AssetPath) as PluginImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Editor DLL importer not found: {AssetPath}");
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(true);
        importer.SaveAndReimport();
        AssetDatabase.ExportPackage(AssetPath, outputPath, ExportPackageOptions.Default);

        if (!File.Exists(outputPath))
        {
            throw new IOException($"Unity did not create the package: {outputPath}");
        }

        Debug.Log($"UNITY_NOVEL_READER_EXPORT_OK={outputPath}");
    }
}
