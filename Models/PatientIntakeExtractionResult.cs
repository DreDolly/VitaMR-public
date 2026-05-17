namespace VitaMR.Models;

public sealed class PatientIntakeExtractionResult
{
    public bool WasAttempted { get; set; }

    public bool WasAvailable { get; set; }

    public string Status { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string DateOfBirth { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string SocialSecurityNumber { get; set; } = string.Empty;

    public string RelationshipNotes { get; set; } = string.Empty;

    public string PhotoPath { get; set; } = string.Empty;

    public List<string> CandidateNames { get; set; } = [];

    public string RawResponse { get; set; } = string.Empty;
}
