﻿using DeltaKustoLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaKustoIntegration
{
    public class FileGateway : IFileGateway
    {
        private readonly string _rootFolder;

        public FileGateway() : this(string.Empty)
        {
        }

        private FileGateway(string rootFolder)
        {
            _rootFolder = rootFolder;
        }

        IFileGateway IFileGateway.ChangeFolder(string folderPath)
        {
            var newRootFolder = Path.Combine(_rootFolder, folderPath);

            return new FileGateway(newRootFolder);
        }

        async Task<string> IFileGateway.GetFileContentAsync(
            string filePath,
            CancellationToken ct)
        {
            var text = await File.ReadAllTextAsync(
                filePath,
                Encoding.UTF8,
                ct);

            return text;
        }

        async Task IFileGateway.SetFileContentAsync(
            string filePath,
            string content,
            CancellationToken ct)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                EnsureDirectoryExists(directory);
            }

            await File.WriteAllTextAsync(
                filePath,
                content,
                ct);
        }

        async IAsyncEnumerable<(string path, string content)> IFileGateway.GetFolderContentsAsync(
            string folderPath,
            IEnumerable<string>? extensions,
            [EnumeratorCancellation]
            CancellationToken ct)
        {
            var fileGateway = (IFileGateway)this;
            var directories = Directory.GetDirectories(folderPath);
            var files = Directory.GetFiles(folderPath);

            foreach (var file in files)
            {
                if (HasExtension(file, extensions))
                {
                    var script = await fileGateway.GetFileContentAsync(file, ct);

                    yield return (file, script);
                }
            }
            foreach (var directory in directories)
            {
                var scripts = fileGateway.GetFolderContentsAsync(directory, extensions, ct);

                await foreach (var script in scripts)
                {
                    yield return script;
                }
            }
        }

        private void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private bool HasExtension(string filePath, IEnumerable<string>? extensions)
        {
            if (extensions == null)
            {
                return true;
            }
            else
            {
                var extensionMatch = extensions
                    .Where(e => filePath.EndsWith("." + e));
                var match = extensionMatch.Any();

                return match;
            }
        }
    }
}