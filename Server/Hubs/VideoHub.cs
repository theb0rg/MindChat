using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using MindChat.Shared;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Reflection;
using System.Text;

namespace MindChat.Server.Hubs
{
    public class MindReader
    {
        const string configFile = "deploy.prototxt";
        const string faceModel = "res10_300x300_ssd_iter_140000_fp16.caffemodel";
        const string secret_data_path = "roligatankar.txt";
        const string dataUrlPrefix = "data:image/jpeg;base64,";
        private readonly byte[] _config;
        private readonly byte[] _faceModel;
        private readonly string[] _secret_data;
        private readonly EmbeddedFileProvider _embeddedProvider = new(Assembly.GetExecutingAssembly());
        private readonly IMemoryCache _memory;
        public MindReader(IMemoryCache cache)
        {
            using var configReader = _embeddedProvider.GetFileInfo($"Data.{configFile}").CreateReadStream();
            using var modelReader = _embeddedProvider.GetFileInfo($"Data.{faceModel}").CreateReadStream();
            using var tankeReader = _embeddedProvider.GetFileInfo($"Data.{secret_data_path}").CreateReadStream();

            _config = StreamToByteArray(configReader);
            _faceModel = StreamToByteArray(modelReader);

            _memory = cache;

            _secret_data = Encoding.Default.GetString(StreamToByteArray(tankeReader)).Split(Environment.NewLine);
        }
        public static byte[] StreamToByteArray(Stream input)
        {
            MemoryStream ms = new();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        public string ReadThoughts(ImageData data)
        {
            return _memory.GetOrCreate<string>(data.Id, (x) =>
            {
                x.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return _secret_data[Random.Shared.Next(0, _secret_data.Length - 1)];
            });
           
        }
        public void AddThoughts(ImageData data)
        {
            var thought = ReadThoughts(data);

            DetectFaceAndAddRealThoughts(data, thought);
        }

        

        public void DetectFaceAndAddRealThoughts(ImageData data, string thought)
        {
            // Read sample image
            var bytes = Convert.FromBase64String(data.Image.Replace("data:image/jpeg;base64,", string.Empty));
            using var frame = Cv2.ImDecode(bytes, ImreadModes.Color);

            int frameHeight = frame.Rows;
            int frameWidth = frame.Cols;
            
            using var faceNet = CvDnn.ReadNetFromCaffe(_config, _faceModel);
            using var blob = CvDnn.BlobFromImage(frame, 1.0, new Size(300, 300), new Scalar(104, 117, 123), false, false);
            faceNet.SetInput(blob, "data");

            using var detection = faceNet.Forward("detection_out");
            using var detectionMat = new Mat(detection.Size(2), detection.Size(3), MatType.CV_32F,
                detection.Ptr(0));
            for (int i = 0; i < detectionMat.Rows; i++)
            {
                float confidence = detectionMat.At<float>(i, 2);

                if (confidence > 0.7)
                {
                    int x1 = (int)(detectionMat.At<float>(i, 3) * frameWidth) + 120;
                    int y1 = (int)(detectionMat.At<float>(i, 4) * frameHeight) - 40;
                    int x2 = (int)(detectionMat.At<float>(i, 5) * frameWidth) + 240;
                    int y2 = (int)(detectionMat.At<float>(i, 6) * frameHeight) - 40;

                    Cv2.Rectangle(frame, new Point(x1, y1), new Point(x2, y2), new Scalar(255, 255, 255), -1);
                    Cv2.Rectangle(frame, new Point(x1, y1), new Point(x2, y2), new Scalar(0, 255, 0), 2, LineTypes.Link4);
                    
                    int posy = y1 + 70;
                    foreach (var line in thought.Chunk(25))
                    {
                        Cv2.PutText(frame, new string(line), new Point(x1 + 10, posy), HersheyFonts.Italic, 0.4, Scalar.Black, 0, LineTypes.AntiAlias);
                        posy += 10;
                    }

                    data.Image = dataUrlPrefix + Convert.ToBase64String(frame.ToBytes(".jpg"));
                }
            }

        }

    }
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
