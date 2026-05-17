namespace VitaMR.Models;

public sealed class VaccineRecordPacket
{
    public string VaccineName { get; set; } = string.Empty;

    public string DateGiven { get; set; } = string.Empty;

    public string DoseOrSeries { get; set; } = string.Empty;

    public string LocationOrProvider { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
