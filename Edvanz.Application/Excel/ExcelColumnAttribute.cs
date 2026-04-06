namespace Edvanz.Application.Excel
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcelColumnAttribute : Attribute
    {
        public string Name { get; set; }
        public int Order { get; set; }

        public ExcelColumnAttribute(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }
}
