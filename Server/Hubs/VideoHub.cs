using Microsoft.AspNetCore.SignalR;
using MindChat.Shared;

namespace MindChat.Server.Hubs
{
    public class VideoHub : Hub
    {
        private readonly MindReader _reader;
        public VideoHub(MindReader reader)
        {
            _reader = reader;
        }
        public async Task SendImageData(ImageData data)
        {
            data.Id = Context.ConnectionId;
            _reader.AddThoughts(data);
            await Clients.All.SendAsync("ImageData", data);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Clients.All.SendAsync("ImageData", new ImageData { Id = Context.ConnectionId });
            await base.OnDisconnectedAsync(exception);
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }
    }
}
