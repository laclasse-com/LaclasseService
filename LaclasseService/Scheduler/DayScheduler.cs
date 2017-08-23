using System;
using System.Net.Mail;
using System.Threading;
using System.Collections.Generic;

namespace Laclasse.Scheduler
{
	public class DaySchedule
	{
		public DayOfWeek Day;
		public TimeSpan Time;
		public Action Action;
		public DateTime LatestRun = DateTime.MinValue;

		public TimeSpan NextRun
		{
			get {
				var now = DateTime.Now;
				// we are the good day and the task was not already runned
				if (now.DayOfWeek == Day && now - LatestRun > TimeSpan.FromMinutes(5))
				{
					var delta = now.Date + Time - now;
					// if we are less than 2 min in late, take the task
					if (delta > TimeSpan.FromMinutes(-2))
						return delta;
				}
				// find the next day
				var current = now.Date.AddDays(1);
				while (current.DayOfWeek != Day)
				{
					current = current.AddDays(1);
				}
				return current.Date + Time - now;
			}
		}

		internal void Run()
		{
			LatestRun = DateTime.Now;
			if (Action != null)
				Action.Invoke();
		}
	}

	public class DayScheduler : IDisposable
	{
		Logger logger;
		readonly object instanceLock = new object();
		bool stop;
		LinkedList<DaySchedule> tasks = new LinkedList<DaySchedule>();
		LinkedList<DaySchedule> runningTasks = new LinkedList<DaySchedule>();

		public DayScheduler(Logger logger)
		{
			this.logger = logger;
			var thread = new Thread(ThreadStart);
			thread.Name = "DayScheduler Thread";
			thread.Priority = ThreadPriority.Normal;
			thread.IsBackground = true;
			thread.Start();
		}

		public void Add(DaySchedule task)
		{
			lock (instanceLock)
			{
				tasks.AddLast(task);
				Monitor.PulseAll(instanceLock);
			}
		}

		void ThreadStart()
		{
			LinkedListNode<DaySchedule> task = null;
			while (true)
			{
				lock (instanceLock)
				{
					if (task != null)
					{
						runningTasks.Remove(task);
						tasks.AddLast(task);
						task = null;
					}
					// search the next scheduled task
					LinkedListNode<DaySchedule> nearestTask = tasks.First;
					if (tasks.First != null)
					{
						for (LinkedListNode<DaySchedule> node = tasks.First; node != tasks.Last.Next; node = node.Next)
						{
							if (node.Value.NextRun < nearestTask.Value.NextRun)
								nearestTask = node;
						}
					}

					if (nearestTask != null && nearestTask.Value.NextRun < TimeSpan.FromSeconds(5))
					{
						task = nearestTask;
						tasks.Remove(task);
						runningTasks.AddLast(task);
					}
					else
					{
						// max sleep 10 min
						var sleepTime = TimeSpan.FromMinutes(10);
						if (nearestTask != null && nearestTask.Value.NextRun < sleepTime)
							sleepTime = nearestTask.Value.NextRun;

						Monitor.Wait(instanceLock, sleepTime);
					}
					if (stop)
						break;
				}
				if (task != null)
				{
					try
					{
						task.Value.Run();
					}
					catch (Exception e)
					{
						logger.Log(LogLevel.Alert, $"ERROR WHILE RUNNING DayTask({task.Value.Day} {task.Value.Time}): {e}");
					}
				}
			}
		}

		public void Dispose()
		{
			lock(instanceLock)
			{
				stop = true;
				Monitor.PulseAll(instanceLock);
			}
		}
	}
}
