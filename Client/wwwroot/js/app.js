function startVideo(src, dotNetHelper) {
    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        navigator.mediaDevices.getUserMedia({ video: true }).then(function (stream) {
            let video = document.getElementById(src);
            if ("srcObject" in video) {
                video.srcObject = stream;
            } else {
                video.src = window.URL.createObjectURL(stream);
            }
            video.onloadedmetadata = function (e) {
                video.play();
            };
            //mirror image
            video.style.webkitTransform = "scaleX(-1)";
            video.style.transform = "scaleX(-1)";

            dotNetHelper.invokeMethod('AllowSendFrames');
        })
            .catch(function (err) {
            });
    }
}

function drawUser(canvasId, dataUrl) {

    var canvas = document.getElementById(canvasId);
    var context = canvas.getContext('2d');
    var imageObj = new Image();
    imageObj.src = dataUrl;

    imageObj.onload = function () {
        context.drawImage(imageObj, 0, 0, 640, 480);
    };
}

///Copy frame from <video> to dataUrl string and call ProcessImage.
function getFrame(src, dotNetHelper) {
    let video = document.getElementById(src);
    var canvas = document.createElement("canvas");

    canvas.width = 320;
    canvas.height = 240;

    canvas.getContext('2d').drawImage(video, 0, 0, 320, 240);

    let dataUrl = canvas.toDataURL("image/jpeg", 0.8);
    dotNetHelper.invokeMethodAsync('ProcessImage', dataUrl);
}