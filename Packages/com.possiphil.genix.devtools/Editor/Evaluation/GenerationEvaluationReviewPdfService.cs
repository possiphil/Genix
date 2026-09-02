using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Builds one lossless, report-scoped PDF from validated visual-review contact sheets.</summary>
    internal static class GenerationEvaluationReviewPdfService
    {
        internal const string FileName = "visual-review-contact-sheets.pdf";

        private const float PageWidth = 841.89f;
        private const float PageHeight = 595.28f;
        private const float HorizontalMargin = 24f;
        private const float ImageAreaBottom = 50f;
        private const float ImageAreaTop = 535f;

        /// <summary>Validates every reviewable run and writes one contact sheet per PDF page.</summary>
        public static bool Build(
            GenerationEvaluationReport report,
            out string pdfPath,
            out int pageCount,
            out string error)
        {
            pdfPath = string.Empty;
            pageCount = 0;
            error = string.Empty;

            if (!report)
            {
                error = "No evaluation report is selected.";
                return false;
            }

            List<ReviewPdfPage> pages = new();
            int missingLayoutCount = 0;
            int invalidCaptureCount = 0;
            string firstCaptureError = string.Empty;
            for (int runIndex = 0; runIndex < report.Runs.Count; runIndex++)
            {
                GenerationEvaluationRunRecord run = report.Runs[runIndex];
                if (!run.HasLayoutReference)
                    continue;

                if (run.HasMissingLayoutAsset)
                {
                    missingLayoutCount++;
                    continue;
                }

                GenerationEvaluationReviewCaptureService.ReviewCaptureStatus status =
                    GenerationEvaluationReviewCaptureService.GetCaptureStatus(
                        report,
                        runIndex,
                        out string contactSheetPath,
                        out string captureError);
                if (status != GenerationEvaluationReviewCaptureService.ReviewCaptureStatus.Valid)
                {
                    invalidCaptureCount++;
                    if (string.IsNullOrWhiteSpace(firstCaptureError))
                        firstCaptureError = captureError;
                    continue;
                }

                pages.Add(new ReviewPdfPage(
                    contactSheetPath,
                    runIndex,
                    run.scenario,
                    run.seed,
                    run.requestedCount,
                    run.placedCount,
                    Path.GetFileNameWithoutExtension(run.scene)));
            }

            if (missingLayoutCount > 0 || invalidCaptureCount > 0)
            {
                List<string> problems = new();
                if (missingLayoutCount > 0)
                    problems.Add($"{missingLayoutCount:N0} saved layout(s) are missing");
                if (invalidCaptureCount > 0)
                    problems.Add($"{invalidCaptureCount:N0} review capture(s) are missing or invalid");

                error = "The review PDF was not created because " + string.Join(" and ", problems) + ".";
                if (!string.IsNullOrWhiteSpace(firstCaptureError))
                    error += " " + firstCaptureError;
                return false;
            }

            if (pages.Count == 0)
            {
                error = "The selected report contains no reviewable saved layouts.";
                return false;
            }

            pdfPath = GetPdfPath(report);
            string stagingPath = pdfPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pdfPath) ?? string.Empty);
                WritePdf(stagingPath, report.name, pages);
                CommitFile(stagingPath, pdfPath);
                pageCount = pages.Count;
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteFile(stagingPath);
                pdfPath = string.Empty;
                pageCount = 0;
                error = $"Could not create the review PDF: {exception.Message}";
                return false;
            }
        }

        /// <summary>Gets the retained PDF path when the selected report already has one.</summary>
        public static string GetExistingPdfPath(GenerationEvaluationReport report)
        {
            if (!report)
                return string.Empty;

            string path = GetPdfPath(report);
            return File.Exists(path) ? path : string.Empty;
        }

        internal static void WritePdf(
            string path,
            string reportName,
            IReadOnlyList<ReviewPdfPage> pages)
        {
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("A review PDF requires at least one page.", nameof(pages));

            const int catalogObject = 1;
            const int pagesObject = 2;
            const int boldFontObject = 3;
            const int regularFontObject = 4;
            const int infoObject = 5;
            const int firstPageObject = 6;
            int objectCount = infoObject + pages.Count * 3;
            long[] offsets = new long[objectCount + 1];

            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            WriteAscii(stream, "%PDF-1.4\n");
            stream.WriteByte((byte)'%');
            stream.Write(new byte[] { 0xE2, 0xE3, 0xCF, 0xD3 }, 0, 4);
            WriteAscii(stream, "\n");

            WriteObject(
                stream,
                offsets,
                catalogObject,
                $"<< /Type /Catalog /Pages {pagesObject} 0 R >>");

            string kids = string.Join(
                " ",
                Enumerable.Range(0, pages.Count)
                    .Select(index => $"{GetPageObject(index, firstPageObject)} 0 R"));
            WriteObject(
                stream,
                offsets,
                pagesObject,
                $"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>");
            WriteObject(
                stream,
                offsets,
                boldFontObject,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
            WriteObject(
                stream,
                offsets,
                regularFontObject,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            WriteObject(
                stream,
                offsets,
                infoObject,
                $"<< /Title ({EscapePdfString(reportName + " - Visual Review")}) " +
                "/Creator (Genix Evaluation) " +
                $"/CreationDate (D:{DateTime.UtcNow:yyyyMMddHHmmss}Z) >>");

            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                ReviewPdfPage page = pages[pageIndex];
                PngImageData image = ReadPng(page.ContactSheetPath);
                int pageObject = GetPageObject(pageIndex, firstPageObject);
                int imageObject = pageObject + 1;
                int contentObject = pageObject + 2;
                string imageName = "Im" + (pageIndex + 1).ToString(CultureInfo.InvariantCulture);

                WriteObject(
                    stream,
                    offsets,
                    pageObject,
                    $"<< /Type /Page /Parent {pagesObject} 0 R " +
                    $"/MediaBox [0 0 {FormatNumber(PageWidth)} {FormatNumber(PageHeight)}] " +
                    $"/Resources << /Font << /F1 {boldFontObject} 0 R /F2 {regularFontObject} 0 R >> " +
                    $"/XObject << /{imageName} {imageObject} 0 R >> >> " +
                    $"/Contents {contentObject} 0 R >>");

                string imageDictionary =
                    $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} " +
                    "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode " +
                    $"/DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns {image.Width} >> " +
                    $"/Length {image.CompressedPixels.Length} >>";
                WriteStreamObject(
                    stream,
                    offsets,
                    imageObject,
                    imageDictionary,
                    image.CompressedPixels);

                byte[] content = Encoding.ASCII.GetBytes(
                    BuildPageContent(page, pageIndex, pages.Count, imageName, image.Width, image.Height));
                WriteStreamObject(
                    stream,
                    offsets,
                    contentObject,
                    $"<< /Length {content.Length} >>",
                    content);
            }

            long xrefOffset = stream.Position;
            WriteAscii(stream, $"xref\n0 {objectCount + 1}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            for (int objectNumber = 1; objectNumber <= objectCount; objectNumber++)
            {
                WriteAscii(
                    stream,
                    offsets[objectNumber].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
            }

            WriteAscii(
                stream,
                $"trailer\n<< /Size {objectCount + 1} /Root {catalogObject} 0 R /Info {infoObject} 0 R >>\n" +
                $"startxref\n{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        }

        private static string BuildPageContent(
            ReviewPdfPage page,
            int pageIndex,
            int pageCount,
            string imageName,
            int imageWidth,
            int imageHeight)
        {
            float availableWidth = PageWidth - HorizontalMargin * 2f;
            float availableHeight = ImageAreaTop - ImageAreaBottom;
            float scale = Mathf.Min(availableWidth / imageWidth, availableHeight / imageHeight);
            float renderedWidth = imageWidth * scale;
            float renderedHeight = imageHeight * scale;
            float imageX = (PageWidth - renderedWidth) * 0.5f;
            float imageY = ImageAreaBottom + (availableHeight - renderedHeight) * 0.5f;

            string scenario = Truncate(page.Scenario, 110);
            string details =
                $"Run {page.RunIndex + 1:N0} | Page {pageIndex + 1:N0} of {pageCount:N0} | " +
                $"Seed {page.Seed} | Placed {page.PlacedCount:N0} of {page.RequestedCount:N0}";
            if (!string.IsNullOrWhiteSpace(page.SceneName))
                details += " | Scene " + page.SceneName;

            StringBuilder content = new();
            content.Append("BT /F1 12 Tf ")
                .Append(FormatNumber(HorizontalMargin)).Append(' ')
                .Append(FormatNumber(568f)).Append(" Td (")
                .Append(EscapePdfString(scenario)).Append(") Tj ET\n");
            content.Append("BT /F2 9 Tf ")
                .Append(FormatNumber(HorizontalMargin)).Append(' ')
                .Append(FormatNumber(552f)).Append(" Td (")
                .Append(EscapePdfString(details)).Append(") Tj ET\n");
            content.Append("q ")
                .Append(FormatNumber(renderedWidth)).Append(" 0 0 ")
                .Append(FormatNumber(renderedHeight)).Append(' ')
                .Append(FormatNumber(imageX)).Append(' ')
                .Append(FormatNumber(imageY)).Append(" cm /")
                .Append(imageName).Append(" Do Q\n");
            content.Append("BT /F2 8 Tf ")
                .Append(FormatNumber(HorizontalMargin)).Append(' ')
                .Append(FormatNumber(30f)).Append(" Td (")
                .Append(EscapePdfString(
                    "Quadrants: overview (top left), top (top right), side X (bottom left), side Z (bottom right)"))
                .Append(") Tj ET\n");
            return content.ToString();
        }

        private static PngImageData ReadPng(string path)
        {
            using FileStream stream = File.OpenRead(path);
            byte[] signature = new byte[8];
            if (stream.Read(signature, 0, signature.Length) != signature.Length ||
                !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                throw new InvalidDataException($"'{path}' is not a PNG image.");
            }

            int width = 0;
            int height = 0;
            using MemoryStream compressedPixels = new();
            while (stream.Position < stream.Length)
            {
                int length = ReadBigEndianInt32(stream);
                if (length < 0 || length > stream.Length - stream.Position - 8)
                    throw new InvalidDataException($"'{path}' contains an invalid PNG chunk.");

                byte[] typeBytes = ReadExactly(stream, 4);
                string type = Encoding.ASCII.GetString(typeBytes);
                byte[] data = ReadExactly(stream, length);
                ReadExactly(stream, 4);

                if (type == "IHDR")
                {
                    if (data.Length != 13 || data[8] != 8 || data[9] != 2 ||
                        data[10] != 0 || data[11] != 0 || data[12] != 0)
                    {
                        throw new InvalidDataException(
                            $"'{path}' must be a non-interlaced 8-bit RGB PNG for PDF export.");
                    }

                    width = ReadBigEndianInt32(data, 0);
                    height = ReadBigEndianInt32(data, 4);
                }
                else if (type == "IDAT")
                {
                    compressedPixels.Write(data, 0, data.Length);
                }
                else if (type == "IEND")
                {
                    break;
                }
            }

            if (width <= 0 || height <= 0 || compressedPixels.Length == 0)
                throw new InvalidDataException($"'{path}' does not contain complete PNG image data.");

            return new PngImageData(width, height, compressedPixels.ToArray());
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(bytes, offset, count - offset);
                if (read <= 0)
                    throw new EndOfStreamException("Unexpected end of PNG data.");
                offset += read;
            }

            return bytes;
        }

        private static int ReadBigEndianInt32(Stream stream) =>
            ReadBigEndianInt32(ReadExactly(stream, 4), 0);

        private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
            bytes[offset] << 24 |
            bytes[offset + 1] << 16 |
            bytes[offset + 2] << 8 |
            bytes[offset + 3];

        private static int GetPageObject(int pageIndex, int firstPageObject) =>
            firstPageObject + pageIndex * 3;

        private static void WriteObject(
            Stream stream,
            long[] offsets,
            int objectNumber,
            string value)
        {
            offsets[objectNumber] = stream.Position;
            WriteAscii(stream, $"{objectNumber} 0 obj\n{value}\nendobj\n");
        }

        private static void WriteStreamObject(
            Stream stream,
            long[] offsets,
            int objectNumber,
            string dictionary,
            byte[] bytes)
        {
            offsets[objectNumber] = stream.Position;
            WriteAscii(stream, $"{objectNumber} 0 obj\n{dictionary}\nstream\n");
            stream.Write(bytes, 0, bytes.Length);
            WriteAscii(stream, "\nendstream\nendobj\n");
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string EscapePdfString(string value)
        {
            StringBuilder result = new();
            foreach (char character in value ?? string.Empty)
            {
                char ascii = character is >= ' ' and <= '~' ? character : '?';
                if (ascii is '\\' or '(' or ')')
                    result.Append('\\');
                result.Append(ascii);
            }

            return result.ToString();
        }

        private static string Truncate(string value, int maximumLength)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "Unnamed Scenario" : value.Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength - 3) + "...";
        }

        private static string FormatNumber(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string GetPdfPath(GenerationEvaluationReport report) =>
            Path.Combine(
                GenerationEvaluationReviewCaptureService.GetReportDirectory(report.name),
                FileName);

        private static void CommitFile(string stagingPath, string finalPath)
        {
            string backupPath = finalPath + ".backup-" + Guid.NewGuid().ToString("N");
            bool movedPrevious = false;
            try
            {
                if (File.Exists(finalPath))
                {
                    File.Move(finalPath, backupPath);
                    movedPrevious = true;
                }

                File.Move(stagingPath, finalPath);
                TryDeleteFile(backupPath);
            }
            catch
            {
                TryDeleteFile(finalPath);
                if (movedPrevious && File.Exists(backupPath))
                    File.Move(backupPath, finalPath);
                throw;
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A stale temporary or backup file can be removed on the next export.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the completed output when cleanup is not permitted.
            }
        }

        internal sealed class ReviewPdfPage
        {
            public string ContactSheetPath { get; }
            public int RunIndex { get; }
            public string Scenario { get; }
            public int Seed { get; }
            public int RequestedCount { get; }
            public int PlacedCount { get; }
            public string SceneName { get; }

            public ReviewPdfPage(
                string contactSheetPath,
                int runIndex,
                string scenario,
                int seed,
                int requestedCount,
                int placedCount,
                string sceneName)
            {
                ContactSheetPath = contactSheetPath;
                RunIndex = runIndex;
                Scenario = scenario;
                Seed = seed;
                RequestedCount = requestedCount;
                PlacedCount = placedCount;
                SceneName = sceneName;
            }
        }

        private readonly struct PngImageData
        {
            public int Width { get; }
            public int Height { get; }
            public byte[] CompressedPixels { get; }

            public PngImageData(int width, int height, byte[] compressedPixels)
            {
                Width = width;
                Height = height;
                CompressedPixels = compressedPixels;
            }
        }
    }
}
