# MindChat
Read peoples minds through webcam (Blazor wasm, SignalR, OpenCV)

App made for a 24hour hackathon

A website that displays peoples webcams and displays a textbubble with some of their most secret thoughts 
(list of randomized sentences that shows for a couple of seconds in a bubble connected to peoples faces.)

Uses SignalR for distributing webcam pictures.
Uses OpenCV for image manipulation and face detection.
Uses ASPNetCore/C# with Blazor WASM. 

Initially OpenCV was done on client (OpenCVSharp with emscripten) but a manditory method was broken and 5 hours of troubleshooting later could not fix it. 
So moved it to the server for now.
