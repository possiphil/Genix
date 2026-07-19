using System;
using System.IO;
using Genix.Editor.Genix.Editor.Assets;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Infrastructure
{
    internal static class StarterContentInstaller
    {
        private const string MenuPath = "Tools/Genix/Import Starter Content";
        private const string PackageName = "com.possiphil.genix";
        private const string StarterContentPath = "Samples~/Starter Content/Genix";

        [MenuItem(MenuPath, false, 1)]
        public static void Import()
        {
            if (!TryGetStarterContentPath(out string sourceRoot))
            {
                EditorUtility.DisplayDialog(
                    "Genix Starter Content",
                    "Starter Content could not be found in the Genix package.",
                    "OK");
                return;
            }

            AssetFileService.EnsureFolder(ProjectContentPaths.Root);

            string targetRoot = ToFullPath(ProjectContentPaths.Root);
            CopyStats stats = new();
            CopyDirectory(sourceRoot, targetRoot, stats);

            AssetDatabase.Refresh();
            AssetCatalogService.Refresh();

            EditorUtility.DisplayDialog(
                "Genix Starter Content",
                CreateResultMessage(stats),
                "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanImport()
        {
            return TryGetStarterContentPath(out _);
        }

        private static bool TryGetStarterContentPath(out string sourceRoot)
        {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(StarterContentInstaller).Assembly);
            string packageRoot = packageInfo?.resolvedPath;

            if (string.IsNullOrWhiteSpace(packageRoot))
                packageRoot = Path.Combine("Packages", PackageName);

            sourceRoot = Path.Combine(packageRoot, StarterContentPath);
            return Directory.Exists(sourceRoot);
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory, CopyStats stats)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (string sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                string directoryName = Path.GetFileName(sourceSubdirectory);
                CopyDirectory(sourceSubdirectory, Path.Combine(targetDirectory, directoryName), stats);
            }

            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
                CopyFileIfMissing(sourceFile, Path.Combine(targetDirectory, Path.GetFileName(sourceFile)), stats);
        }

        private static void CopyFileIfMissing(string sourceFile, string targetFile, CopyStats stats)
        {
            if (File.Exists(targetFile))
            {
                stats.Skipped++;
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? string.Empty);
                File.Copy(sourceFile, targetFile);
                stats.Copied++;
            }
            catch (Exception exception)
            {
                stats.Failed++;
                Debug.LogWarning($"Could not import Genix Starter Content file '{sourceFile}': {exception.Message}");
            }
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string CreateResultMessage(CopyStats stats)
        {
            if (stats.Failed > 0)
            {
                return $"Starter Content import finished with {stats.Failed} failed file(s).\n\n" +
                       $"Copied: {stats.Copied}\nSkipped existing: {stats.Skipped}";
            }

            if (stats.Copied == 0)
                return "Starter Content is already imported. No files were changed.";

            return $"Starter Content imported into {ProjectContentPaths.Root}.\n\n" +
                   $"Copied: {stats.Copied}\nSkipped existing: {stats.Skipped}";
        }

        private sealed class CopyStats
        {
            public int Copied;
            public int Skipped;
            public int Failed;
        }
    }
}
