using System;
using System.Collections.Generic;

namespace Laclasse.Authentication
{
	public class PreTicket
	{
		readonly object instanceLock = new object();
		public readonly string id;
		public readonly DateTime start;
		string _cutId;
		string _service;
		bool _wantTicket;
		string _uid;
		Idp _idp;

		public PreTicket()
		{
			id = Guid.NewGuid().ToString();
			start = DateTime.Now;
		}

		public string cutId
		{
			get
			{
				lock(instanceLock)
					return _cutId;
			}
			set
			{
				lock(instanceLock)
					_cutId = value;
			}
		}

		public string service
		{
			get
			{
				lock(instanceLock)
					return _service;
			}
			set
			{
				lock (instanceLock)
					_service = value;
			}
		}

		public bool wantTicket
		{
			get
			{
				lock(instanceLock)
					return _wantTicket;
			}
			set
			{
				lock(instanceLock)
					_wantTicket = value;
			}
		}

		public string uid
		{
			get
			{
				lock (instanceLock)
					return _uid;
			}
			set
			{
				lock (instanceLock)
					_uid = value;
			}
		}

		public Idp idp
		{
			get
			{
				lock (instanceLock)
					return _idp;
			}
			set
			{
				lock (instanceLock)
					_idp = value;
			}
		}
	}

	public class PreTickets
	{
		readonly object instanceLock = new object();
		readonly double timeout;
		readonly Dictionary<string, PreTicket> tickets = new Dictionary<string, PreTicket>();

		public PreTickets(double timeout)
		{
			this.timeout = timeout;
		}

		void Clean()
		{
			DateTime now = DateTime.Now;
			lock (instanceLock)
			{
				string[] ticketIds = new string[tickets.Keys.Count];
				tickets.Keys.CopyTo(ticketIds, 0);
				foreach (string id in ticketIds)
				{
					var ticket = tickets[id];
					if (now - ticket.start > TimeSpan.FromSeconds(timeout))
						tickets.Remove(id);
				}
			}
		}

		public void Add(PreTicket ticket)
		{
			Clean();
			lock (instanceLock)
				tickets[ticket.id] = ticket;
		}

		public void Remove(string id)
		{
			lock (instanceLock)
				tickets.Remove(id);
		}

		public PreTicket this[string id] {
			get {
				Clean();
				lock(instanceLock)
					return tickets.ContainsKey(id) ? tickets[id] : null;
			}
		}
	}
}
