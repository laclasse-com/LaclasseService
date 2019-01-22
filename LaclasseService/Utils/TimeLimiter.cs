using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Laclasse.Utils
{
    public class TimeLimiter
    {
        TimeSpan interval;
        int maxCount;

        object instanceLock = new object();
        long currentInterval;
        Dictionary<string, int> limiter = new Dictionary<string, int>();

        public TimeLimiter(TimeSpan interval, int maxCount)
        {
            this.interval = interval;
            this.maxCount = maxCount;
            currentInterval = DateTime.Now.Ticks / interval.Ticks;
        }

        bool CleanWithLock()
        {
            var newInterval = DateTime.Now.Ticks / interval.Ticks;
            if (newInterval != currentInterval)
            {
                limiter.Clear();
                currentInterval = newInterval;
                return true;
            }
            return false;
        }

        public int GetCount(string key, bool add = false)
        {
            var count = 0;
            lock (instanceLock)
            {
                if (!CleanWithLock())
                {
                    if (limiter.ContainsKey(key))
                    {
                        count = limiter[key];
                        if (add)
                            limiter[key]++;
                    }
                    else if (add)
                        limiter[key] = 1;
                }
            }
            return count;
        }

        public async Task RateLimitAsync(string key, bool add = false)
        {
            var count = GetCount(key, add);
            if (count > maxCount)
                await Task.Delay(TimeSpan.FromTicks(interval.Ticks - DateTime.Now.Ticks % interval.Ticks));
        }

        public void Remove(string key)
        {
            lock (limiter)
            {
                limiter.Remove(key);
            }
        }

        public void Add(string key)
        {
            var now = DateTime.Now.Ticks;
            lock (instanceLock)
            {
                CleanWithLock();
                if (limiter.ContainsKey(key))
                    limiter[key]++;
                else
                    limiter[key] = 1;
            }
        }
    }
}
