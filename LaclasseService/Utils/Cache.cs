// Cache.cs
// 
//  Helper class to handle simple items cache in memory
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2019 Metropole de Lyon
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 


using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Laclasse.Utils
{
    public class Cache<T> where T : class
    {
        public delegate Task<T> CacheMissedHandlerAsync(string key);

        readonly int maxItems;
        readonly TimeSpan duration;
        readonly CacheMissedHandlerAsync cacheMissedHandlerAsync;
        readonly object instanceLock = new object();
        Dictionary<string, ItemLock<T>> items = new Dictionary<string, ItemLock<T>>();

        class ItemLock<T2> where T2 : class
        {
            public DateTime Time = DateTime.Now;
            public bool Done;
            public T2 Data;
        }

        public Cache(int maxItems, TimeSpan duration, CacheMissedHandlerAsync cacheMissedHandlerAsync)
        {
            this.maxItems = maxItems;
            this.duration = duration;
            this.cacheMissedHandlerAsync = cacheMissedHandlerAsync;
        }

        void Clean()
        {
            var now = DateTime.Now;
            lock (instanceLock)
            {
                // remove older cached values
                items.Where((item) => now - item.Value.Time > duration).Select((item) => item.Key).ToList().ForEach((key) => items.Remove(key));
                // if too many, remove olders
                if (items.Count > maxItems)
                    items.OrderBy((arg) => arg.Value.Time).Take(items.Count - maxItems).Select(item => item.Key).ToList().ForEach((key) => items.Remove(key));
            }
        }

        public async Task<T> GetAsync(string key)
        {
            Clean();

            ItemLock<T> progressLock = null;
            ItemLock<T> ownProgressLock = null;
            T res = null;
            lock (instanceLock)
            {
                if (items.ContainsKey(key))
                    progressLock = items[key];
                else
                {
                    ownProgressLock = new ItemLock<T>();
                    items[key] = ownProgressLock;
                }
            }
            if (ownProgressLock != null)
            {
                try
                {
                    res = await cacheMissedHandlerAsync(key);
                }
                catch
                {
                    res = null;
                }
                // dont keep in cache loading failures
                if (res == null)
                {
                    lock (instanceLock)
                    {
                        items.Remove(key);
                    }
                }
                lock (ownProgressLock)
                {
                    ownProgressLock.Data = res;
                    ownProgressLock.Done = true;
                    Monitor.PulseAll(ownProgressLock);
                }
            }
            if (progressLock != null)
            {
                lock (progressLock)
                {
                    if (!progressLock.Done)
                        Monitor.Wait(progressLock);
                }
                res = progressLock.Data;
            }
            return res;
        }
    }
}
