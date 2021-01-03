using Accord.Imaging;
using System.Drawing;
using System.Threading.Tasks;

namespace LongTermTimeLapse.TimeLapseFrameProcessors
{
    public interface ITimeLapseFrameProcessor
    {
        Task<MatrixH> Adjust(Bitmap baseBitmap, Size baseOriginalSize, Bitmap targetBitmap, Size targetOriginalSize);
    }
}
