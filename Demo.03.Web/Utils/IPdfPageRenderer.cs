using PDFtoImage;
using SkiaSharp;

namespace Demo.Embedding.Web;

public interface IPdfPageRenderer
{

    /// <summary>
    /// Renderiza as páginas do PDF como imagens PNG usando SkiaSharp
    /// </summary>
    /// <param name="pdfBytes"></param>
    /// <param name="maxPages"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<byte[]>> SkiaSharpPdfRenderPagesAsPngAsync(byte[] pdfBytes, int maxPages, CancellationToken ct);
}

public class PdfPageRenderer : IPdfPageRenderer
{
    public async Task<List<byte[]>> SkiaSharpPdfRenderPagesAsPngAsync(
       byte[] pdfBytes,
       int maxPages,
       CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var images = new List<byte[]>();

            using var pdfStream = new MemoryStream(pdfBytes);

            // Renderiza com alta qualidade
            var bitmaps = Conversion.ToImages(pdfStream);

            int count = 0;
            foreach (var bitmap in bitmaps)
            {
                if (count >= maxPages) break;
                ct.ThrowIfCancellationRequested();

                using (bitmap)
                {
                    var pngBytes = bitmap.Encode(
                        SKEncodedImageFormat.Png,
                        100).ToArray();
                    images.Add(pngBytes);
                }
                count++;
            }

            return images;
        }, ct);
    }
}