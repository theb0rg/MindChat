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
            return _memory.GetOrCreate<string>(data.Id+nameof(ReadThoughts), (x) =>
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
       
        /// <summary>
        /// TODO: Performance: Detect face position in other thread and just push the new value when ready.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (Point x, Point y) DetectFace(ImageData data)
        {
            return _memory.GetOrCreate<(Point x, Point y)>(data.Id+nameof(DetectFace), (x) =>
            {
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

                    if (confidence > 0.8)
                    {
                        int x1 = (int)(detectionMat.At<float>(i, 3) * frameWidth);
                        int y1 = (int)(detectionMat.At<float>(i, 4) * frameHeight);
                        int x2 = (int)(detectionMat.At<float>(i, 5) * frameWidth);
                        int y2 = (int)(detectionMat.At<float>(i, 6) * frameHeight);

                        x.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(0.5);

                        return (new Point(x1, y1), new Point(x2, y2));
                    }
                }

                x.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(33);

                return default;
            });
        }

        public void DetectFaceAndAddRealThoughts(ImageData data, string thought)
        {
            var position = DetectFace(data);

            if (position != default) {
                var bytes = Convert.FromBase64String(data.Image.Replace("data:image/jpeg;base64,", string.Empty));
                using var frame = Cv2.ImDecode(bytes, ImreadModes.Color);

                //TODO: Smarter positioning of thought bubble e.g if the face is on the right side, show it one the left etc
                position.x.X += 100;
                position.x.Y -= 40;
                position.y.X += 200;
                position.y.Y -= 40;

                //TODO: Actually paint thought bubbles instead of a rectangle.
                Cv2.Rectangle(frame, new Point(position.x.X, position.x.Y), new Point(position.y.X, position.y.Y), new Scalar(255, 255, 255), -1);
                Cv2.Rectangle(frame, new Point(position.x.X, position.x.Y), new Point(position.y.X, position.y.Y), new Scalar(0, 255, 0), 2, LineTypes.Link4);

                int posy = position.x.Y + 70;
                //TODO: Calculate text width and do smarter word wrap
                foreach (var line in thought.Chunk(25))
                {
                    //TODO: Render with a fond that support non-unicode characters.
                    //TODO: Use fontScale to scale with the size of the thought bubble. Or better yet, scale thought bubble based on the sentence.
                    Cv2.PutText(frame, new string(line), new Point(position.x.X + 10, posy), HersheyFonts.Italic, 0.4, Scalar.Black, 0, LineTypes.AntiAlias);
                    posy += 10;
                }
                data.Image = dataUrlPrefix + Convert.ToBase64String(frame.ToBytes(".jpg"));
            }

          }
    }
}
