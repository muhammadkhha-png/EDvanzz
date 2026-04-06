using ClosedXML.Excel;
using Edvanz.Application.Excel;
using Edvanz.Application.IservicesContract;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Excel
{
    public class ExcelService:IExcelService
    {
        public byte[] ExportToExcel<T>(List<T> data)
        {
           
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                var properties = typeof(T)
                    .GetProperties()
                    .Select(p => new
                    {
                        Property = p,
                        Attribute = p.GetCustomAttributes(typeof(ExcelColumnAttribute), false)
                                     .FirstOrDefault() as ExcelColumnAttribute
                    })
                    .Where(x => x.Attribute != null)
                    .OrderBy(x => x.Attribute.Order)
                    .ToList();

                if (!properties.Any())
                    throw new ArgumentException("No ExcelColumnAttribute defined on DTO");

                // Header
                for (int col = 0; col < properties.Count; col++)
                {
                    worksheet.Cell(1, col + 1).Value = properties[col].Attribute.Name;
                }

                // Data
                for (int row = 0; row < data.Count; row++)
                {
                    for (int col = 0; col < properties.Count; col++)
                    {
                        var value = properties[col].Property.GetValue(data[row]);

                        worksheet.Cell(row + 2, col + 1).Value = value switch
                        {
                            null => "",
                            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
                            _ => value.ToString()
                        };
                    }
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
       
        }
    }
}
