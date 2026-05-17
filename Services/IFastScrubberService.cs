using VitaMR.Models;

namespace VitaMR.Services;

public interface IFastScrubberService
{
    ScrubResult ScrubFile(string rawFilePath);
}
