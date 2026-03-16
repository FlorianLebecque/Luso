using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luso.Audio
{
    interface IAudioAnalyser
    {

        Task InitAsync();
        bool IsReady { get; }

        double GetHighLevel();
        double GetMidLevel();
        double GetLowLevel();
    }
}
