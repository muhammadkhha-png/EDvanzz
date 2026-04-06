using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IExcelService
    {
        public byte[] ExportToExcel<T>(List<T> data);
    }
}
