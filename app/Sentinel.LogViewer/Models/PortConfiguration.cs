namespace Sentinel.LogViewer.Models
{
    /// <summary>
    /// Represents a port configuration for UDP listening
    /// </summary>
    public class PortConfiguration
    {
        /// <summary>
        /// Address in format udp://host:port (e.g., udp://0.0.0.0:4000)
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Whether this port is currently being listened to
        /// </summary>
        public bool IsListening { get; set; }

        /// <summary>
        /// Status message (e.g., "Listening", "Error: Port in use")
        /// </summary>
        public string Status { get; set; } = "Stopped";

        /// <summary>
        /// Extract port number from address
        /// </summary>
        public int? GetPort()
        {
            if (string.IsNullOrEmpty(Address))
                return null;

            try
            {
                var uri = new System.Uri(Address);
                return uri.Port;
            }
            catch
            {
                return null;
            }
        }
    }
}

