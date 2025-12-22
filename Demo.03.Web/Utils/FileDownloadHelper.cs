namespace Demo.Embedding.Web.Utils;

using System;
using System.IO;
using System.Threading.Tasks;

public static class FileDownloadHelper
{
    /// <summary>
    /// Salva um arquivo (imagem, PDF, etc) na pasta Downloads do usuário
    /// </summary>
    /// <param name="fileName">Nome do arquivo com extensão (ex: "pagina-1.png")</param>
    /// <param name="fileBytes">Bytes do arquivo</param>
    /// <returns>Caminho completo onde o arquivo foi salvo</returns>
    public static async Task<string> SaveToDownloadsAsync(
        string fileName,
        byte[] fileBytes)
    {
        // Obter pasta Downloads
        string downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        // Criar pasta se não existir
        Directory.CreateDirectory(downloadsPath);

        // Caminho completo do arquivo
        string fullPath = Path.Combine(downloadsPath, fileName);

        // Se arquivo já existir, adicionar número
        fullPath = GetUniqueFilePath(fullPath);

        // Salvar arquivo
        await File.WriteAllBytesAsync(fullPath, fileBytes);

        return fullPath;
    }

    /// <summary>
    /// Versão síncrona
    /// </summary>
    public static string SaveToDownloads(string fileName, byte[] fileBytes)
    {
        string downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsPath);

        string fullPath = Path.Combine(downloadsPath, fileName);
        fullPath = GetUniqueFilePath(fullPath);

        File.WriteAllBytes(fullPath, fileBytes);

        return fullPath;
    }

    /// <summary>
    /// Gera nome único se arquivo já existir
    /// </summary>
    private static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        string directory = Path.GetDirectoryName(filePath);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        int counter = 1;

        while (File.Exists(filePath))
        {
            string newFileName = $"{fileNameWithoutExt} ({counter}){extension}";
            filePath = Path.Combine(directory, newFileName);
            counter++;
        }

        return filePath;
    }
}
