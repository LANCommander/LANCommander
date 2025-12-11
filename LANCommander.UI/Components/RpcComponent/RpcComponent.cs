using System.Reflection;
using LANCommander.SDK.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.UI.Components
{
    /// <summary>
    /// Base component that connects to a SignalR hub and exposes the component
    /// as a strongly-typed client (THubClient).
    /// 
    /// Derived components must:
    /// - Implement THubClient
    /// - Provide the HubUrl
    /// </summary>
    public abstract class RpcComponentBase<THubClient, THub> : ComponentBase, IAsyncDisposable
        where THubClient : class where THub : class
    {
        private readonly List<IDisposable> _clientHandlerSubscriptions = new();

        protected HubConnection? HubConnection { get; private set; }
        
        protected THub? Hub { get; private set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        /// <summary>
        /// Relative hub URL, e.g. "/hubs/notifications".
        /// </summary>
        protected abstract string HubUrl { get; }

        /// <summary>
        /// The component cast as the hub client interface.
        /// </summary>
        protected THubClient Client => (THubClient)(object)this;

        protected bool IsConnected =>
            HubConnection?.State == HubConnectionState.Connected;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            var absoluteHubUrl = NavigationManager.ToAbsoluteUri(HubUrl);

            var builder = new HubConnectionBuilder()
                .WithUrl(absoluteHubUrl)
                .WithAutomaticReconnect();

            HubConnection = builder.Build();

            Hub = HubConnection.ServerProxy<THub>();

            WireClientInterfaceHandlers(HubConnection);

            HubConnection.Reconnected += async _ =>
            {
                await OnReconnectedAsync();
            };

            HubConnection.Reconnecting += async ex =>
            {
                await OnReconnectingAsync(ex);
            };

            HubConnection.Closed += async ex =>
            {
                await OnClosedAsync(ex);
            };

            await HubConnection.StartAsync();
            await OnConnectedAsync();
        }

        /// <summary>
        /// Called after the connection is successfully started.
        /// </summary>
        protected virtual Task OnConnectedAsync() => Task.CompletedTask;

        /// <summary>
        /// Called when the connection is in the process of reconnecting.
        /// </summary>
        protected virtual Task OnReconnectingAsync(Exception? exception) => Task.CompletedTask;

        /// <summary>
        /// Called when the connection has reconnected.
        /// </summary>
        protected virtual Task OnReconnectedAsync() => Task.CompletedTask;

        /// <summary>
        /// Called when the connection is closed and will not reconnect automatically.
        /// </summary>
        protected virtual Task OnClosedAsync(Exception? exception) => Task.CompletedTask;

        /// <summary>
        /// Registers handlers for every method on THubClient so that when the hub
        /// invokes these methods on the client, they are forwarded to this component.
        /// </summary>
        private void WireClientInterfaceHandlers(HubConnection hubConnection)
        {
            var clientType = typeof(THubClient);
            var methods = clientType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName); // ignore property accessors, etc.

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

                // Use the generic On(string, Type[], Func<object[], Task>) overload
                var subscription = hubConnection.On(
                    methodName: method.Name,
                    parameterTypes: parameterTypes,
                    handler: async args =>
                    {
                        var result = method.Invoke(Client, args);

                        if (result is Task task)
                            await task;

                        // Ensure UI updates are marshaled back onto the Blazor renderer
                        await InvokeAsync(StateHasChanged);
                    });

                _clientHandlerSubscriptions.Add(subscription);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var subscription in _clientHandlerSubscriptions)
                subscription.Dispose();

            _clientHandlerSubscriptions.Clear();

            if (HubConnection is not null)
            {
                await HubConnection.DisposeAsync();
            }
        }
    }
}
