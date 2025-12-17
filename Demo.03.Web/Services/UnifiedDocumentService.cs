using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;
using UglyToad.PdfPig;
using A = DocumentFormat.OpenXml.Drawing;

namespace Demo.Embedding.Web;

public class UnifiedDocumentService
{
    /// <summary>
    ///  Extrai texto do arquivo salvo no disco
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public string ExtractText(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".pdf" => ExtractFromPdf(filePath),
            ".docx" => ExtractFromDocx(filePath),
            ".xlsx" => ExtractFromExcel(filePath),
            ".pptx" => ExtractFromPowerPoint(filePath),
            ".txt" or ".md" => File.ReadAllText(filePath),
            _ => throw new NotSupportedException($"Formato {extension} não suportado")
        };
    }

    /// <summary>
    /// Extrai texto do stream sem salvar arquivo no disco
    /// </summary>
    public async Task<string> ExtractTextFromStream(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();

        return extension switch
        {
            ".pdf" => await ExtractFromPdfStream(fileStream),
            ".docx" => await ExtractFromDocxStream(fileStream),
            ".txt" => await ExtractFromTextStream(fileStream),
            _ => throw new NotSupportedException($"Formato {extension} não suportado")
        };
    }

    public string ExtractTextFromPdf(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private async Task<string> ExtractFromPdfStream(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        return string.Join("\n\n", document.GetPages().Select(p => p.Text));
    }

    private async Task<string> ExtractFromDocxStream(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart.Document.Body.InnerText;
    }

    private async Task<string> ExtractFromTextStream(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private string ExtractFromPdf(string filePath)
    {
        using var doc = PdfDocument.Open(filePath);
        return string.Join("\n\n", doc.GetPages().Select(p => p.Text));
    }


    private string ExtractFromDocx(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        return doc.MainDocumentPart.Document.Body.InnerText;
    }

    private string ExtractFromExcel(string filePath)
    {
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = document.WorkbookPart;
        var sheets = workbookPart.Workbook.Descendants<Sheet>();

        var text = new StringBuilder();

        foreach (var sheet in sheets)
        {
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            text.AppendLine($"=== {sheet.Name} ===");

            foreach (var row in sheetData.Elements<Row>())
            {
                var rowValues = new List<string>();

                foreach (var cell in row.Elements<Cell>())
                {
                    var cellValue = GetCellValue(cell, workbookPart);
                    rowValues.Add(cellValue);
                }

                text.AppendLine(string.Join("\t", rowValues));
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    private string GetCellValue(Cell cell, WorkbookPart workbookPart)
    {
        if (cell.CellValue == null)
            return string.Empty;

        var value = cell.CellValue.InnerText;

        // Se for string compartilhada (shared string)
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            var stringTable = workbookPart.GetPartsOfType<SharedStringTablePart>()
                .FirstOrDefault()?.SharedStringTable;

            if (stringTable != null)
            {
                return stringTable.ElementAt(int.Parse(value)).InnerText;
            }
        }

        return value;
    }
    private string ExtractFromPowerPoint(string filePath)
    {
        using var document = PresentationDocument.Open(filePath, false);
        var presentationPart = document.PresentationPart;
        var presentation = presentationPart.Presentation;

        var text = new StringBuilder();
        var slideIdList = presentation.SlideIdList;

        if (slideIdList == null)
            return string.Empty;

        int slideNumber = 1;

        foreach (SlideId slideId in slideIdList)
        {
            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId);

            text.AppendLine($"=== Slide {slideNumber} ===");
            text.AppendLine(GetSlideText(slidePart));
            text.AppendLine();

            slideNumber++;
        }

        return text.ToString();
    }

    private string GetSlideText(SlidePart slidePart)
    {
        var text = new StringBuilder();

        // Extrair texto de todos os shapes
        var shapes = slidePart.Slide.Descendants<Shape>();

        foreach (var shape in shapes)
        {
            var textBody = shape.TextBody;
            if (textBody != null)
            {
                foreach (var paragraph in textBody.Descendants<A.Paragraph>())
                {
                    foreach (var run in paragraph.Descendants<A.Run>())
                    {
                        text.Append(run.Text?.Text ?? "");
                    }
                    text.AppendLine();
                }
            }
        }

        return text.ToString();
    }

}

