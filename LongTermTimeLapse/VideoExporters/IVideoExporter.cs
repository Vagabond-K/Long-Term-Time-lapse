using System.Threading.Tasks;

namespace LongTermTimeLapse.VideoExporters
{
    public interface IVideoExporter
    {
        bool IsExporting { get; }
        double Processed { get; }
        Task Export(TimeLapse timeLapse, string filePath);
    }
}
