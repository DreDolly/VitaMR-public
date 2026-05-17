namespace VitaMR.Models;

public sealed class PatientIdentityRecord
{
    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string DateOfBirth { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string SocialSecurityNumber { get; set; } = string.Empty;

    public string RelationshipNotes { get; set; } = string.Empty;

    public string PhotoPath { get; set; } = string.Empty;

    public string MedicalRecordNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
