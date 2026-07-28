using ClosedXML.Excel;
using FastReport.Export.PdfSimple;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;

namespace Svod_Reports.BE.API.Controller;

[ApiController]
[Route("api/reports")] // Задает базовый роут для совпадения с вашим React BACKEND_API_URL
public class SvodReportsController : ControllerBase
{
    [HttpGet("get-pdf-report")] // Оставляем роут прежним, чтобы не менять фронтенд
    public IActionResult GetPdfReport(int id)
    {
        // Инициализируем стандартный отчет
        var report = new FastReport.Report();

        try
        {
            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "Reports", $"Report_{id}.frx");

            if (!System.IO.File.Exists(reportPath))
            {
                return NotFound($"Файл шаблона отчета №{id} не найден.");
            }

            // 1. Загружаем и строим отчет
            report.Load(reportPath);
            report.Prepare();

            // 2. Инициализируем плагин экспорта в PDF
            using (var ms = new MemoryStream())
            using (var pdfExport = new PDFSimpleExport())
            {
                // Экспортируем построенный отчет в поток памяти (MemoryStream)
                pdfExport.Export(report, ms);

                // 3. Возвращаем массив байтов с правильным MIME-типом PDF
                // Браузер внутри iframe автоматически отобразит полноценный PDF-ридер
                return File(ms.ToArray(), "application/pdf");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка генерации PDF: {ex.Message}");
        }
        finally
        {
            report.Dispose(); // Освобождаем память сервера
        }
    }




    [HttpGet("get-excel-report")]
    public IActionResult GetExcelPreview([FromQuery] int id)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Превью");

        // 1. ГЕНЕРАЦИЯ ДАННЫХ (Ваша бизнес-логика остается прежней)
        ws.Cell("A1").Value = $"Наименование (Отчет №{id})";
        ws.Cell("B1").Value = "Сумма";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("B1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Cell("A2").Value = "Услуги разработки";
        ws.Cell("B2").Value = 50000;
        ws.Cell("B2").Style.NumberFormat.Format = "#,##0.00";

        // 2. ВЫСОКООПТИМИЗИРОВАННЫЙ МАППИНГ ДЛЯ FORTUNESHEET
        var celldata = new List<object>();

        // КРИТИЧНО ДЛЯ БОЛЬШИХ ОТЧЕТОВ: 
        // Перебираем ТОЛЬКО заполненные ячейки, полностью игнорируя пустые области.
        // Это исключает лишние итерации и экономит память.
        foreach (var cell in ws.CellsUsed())
        {
            // Получаем отформатированный текст ячейки (с учетом масок ClosedXML, например "50 000,00")
            string formattedValue = cell.GetFormattedString();
            // Получаем сырое значение для вычислений в строке формул
            string rawValue = cell.Value.ToString();

            // Базовый объект значения ячейки по спецификации FortuneSheet
            var cellValueObj = new Dictionary<string, object>
        {
            { "v", rawValue },       // Сырое значение ячейки (value)
            { "m", formattedValue }  // Отформатированная строка для отображения (text/formatted message)
        };

            // Переносим базовое форматирование (жирный шрифт)
            if (cell.Style.Font.Bold)
            {
                cellValueObj["bl"] = 1; // 1 означает жирный шрифт в FortuneSheet
            }

            //// Переносим цвет фона ячейки (если он задан)
            //if (!cell.Style.Fill.BackgroundColor.IsTransparent)
            //{
            //    // Конвертируем цвет ClosedXML в Hex-строку для CSS браузера
            //    var hexColor = cell.Style.Fill.BackgroundColor.Color.ToHex();
            //    cellValueObj["bg"] = $"#{hexColor}";
            //}

            // Добавляем координаты (индексы с нуля) и объект ячейки в массив данных
            celldata.Add(new
            {
                r = cell.Address.RowNumber - 1,
                c = cell.Address.ColumnNumber - 1,
                v = cellValueObj
            });
        }

        // Формируем итоговую структуру книги (массив объектов листов)
        var responseStructure = new[]
        {
        new
        {
            name = ws.Name,     // Имя вкладки
            status = 1,         // 1 - лист активен при открытии
            order = 0,          // Индекс вкладки
            celldata = celldata, // Наш оптимизированный массив ячеек
            defaultColWidth = 140 // Задаем ширину колонок побольше, чтобы текст не скрывался
        }
    };

        // Возвращаем чистый структурированный JSON. .NET сам сериализует его с максимальной скоростью.
        return Ok(responseStructure);
    }

    [HttpGet("download-excel-report")]
    public IActionResult DownloadExcelReport([FromQuery] int id)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Отчет");

        // 1. ГЕНЕРАЦИЯ ДАННЫХ (Точно такая же, как в превью)
        ws.Cell("A1").Value = $"Наименование (Отчет №{id})";
        ws.Cell("B1").Value = "Сумма";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("B1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Cell("A2").Value = "Услуги разработки";
        ws.Cell("B2").Value = 50000;
        ws.Cell("B2").Style.NumberFormat.Format = "#,##0.00";

        // Автоматическая ширина колонок, чтобы текст не обрезался в самом Excel
        ws.Columns().AdjustToContents();

        // 2. СОХРАНЕНИЕ В БИНАРНЫЙ ПОТОК (Вместо JSON-маппинга)
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        // Возвращаем файл с правильным MIME-типом Excel и заголовком скачивания [1]
        string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string fileName = $"Report_{id}_{DateTime.Now:yyyyMMdd}.xlsx";

        return File(content, contentType, fileName);
}

}
