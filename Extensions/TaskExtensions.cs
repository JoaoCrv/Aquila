using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Extensions
{
    public static class TaskExtensions
    {
        public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
            }
        }
    }
}
