using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class ServerProcessHub : Hub
    {
        private ServerProcessService ServerProcessService;
        
        public ServerProcessHub(ServerProcessService serverProcessService)
        {
            ServerProcessService = serverProcessService;

            foreach (var process in ServerProcessService.Running.Values)
            {
                //process.OutputDataReceived += Process_OutputDataReceived;
            }
        }

        private async void Process_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            await Clients.All.SendAsync("Server[]", e.Data);
        }
    }
}
