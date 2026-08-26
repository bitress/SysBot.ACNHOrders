using System;

namespace SocketAPI
{
	[Serializable]
	public class SocketAPIServerConfig
	{
		/// <summary>
		/// Whether the socket API server (TCP) should be enabled or not.
		/// </summary>
		public bool Enabled { get; set; } = false;

		/// <summary>
		/// Whether logs relative to the socket/HTTP API server should be written out to console.
		/// </summary>
		public bool LogsEnabled { get; set; } = true;

		/// <summary>
		/// The network port on which the TCP socket server listens for incoming connections.
		/// </summary>
		public ushort Port { get; set; } = 5201;

		/// <summary>
		/// Whether the HTTP REST API server should be enabled or not.
		/// </summary>
		public bool HttpApiEnabled { get; set; } = true;

		/// <summary>
		/// The network port on which the HTTP REST API server listens for incoming HTTP requests.
		/// </summary>
		public ushort HttpPort { get; set; } = 5202;

		/// <summary>
		/// Custom HTTP prefix for HttpListener (e.g. "http://*:5202/" or "http://localhost:5202/").
		/// </summary>
		public string HttpPrefix { get; set; } = "http://*:5202/";

		/// <summary>
		/// Allowed CORS origin header for HTTP requests (default is "*" to allow any web page).
		/// </summary>
		public string CorsAllowOrigin { get; set; } = "*";

		/// <summary>
		/// Whether web/API users are permitted to submit drop requests.
		/// </summary>
		public bool AllowDropFromWeb { get; set; } = false;

		/// <summary>
		/// Whether web/API users are permitted to submit order requests.
		/// </summary>
		public bool AllowOrderFromWeb { get; set; } = true;

		/// <summary>
		/// Optional secret API key required to access HTTP endpoints. If empty or null, no API key is enforced.
		/// Pass via 'X-API-Key: your_key' or 'Authorization: Bearer your_key'.
		/// </summary>
		public string ApiKey { get; set; } = string.Empty;

		/// <summary>
		/// If true, only requests originating from localhost (127.0.0.1 / ::1) will be accepted.
		/// </summary>
		public bool AllowLocalhostOnly { get; set; } = false;
	}
}