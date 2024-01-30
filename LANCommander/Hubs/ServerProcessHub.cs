using LANCommander.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Hubs
{
    public class ServerProcessHub : Hub
    {
        private ServerProcessService ServerProcessService;
        
        public ServerProcessHub(ServerProcessService serverProcessService)
        {
            ServerProcessService = serverProcessService;

            foreach (var process in ServerProcessService.Processes.Values)
            {
                process.OutputDataReceived += Process_OutputDataReceived;
            }
        }

        private async void Process_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            await Clients.All.SendAsync("Server[]", e.Data);
        }
    }
}
