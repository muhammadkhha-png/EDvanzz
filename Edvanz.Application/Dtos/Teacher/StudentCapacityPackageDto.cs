namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Output DTO for student capacity package tiers.
/// AAM-FR-04.1: Displayed during teacher configuration flow.
/// </summary>
public class StudentCapacityPackageDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public int MinStudents { get; set; }
    public int? MaxStudents { get; set; }
    public int DisplayOrder { get; set; }
}