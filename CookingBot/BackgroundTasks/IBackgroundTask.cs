using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.BackgroundTasks
{
    public interface IBackgroundTask
    {
        Task Start(CancellationToken ct);
    }
}
