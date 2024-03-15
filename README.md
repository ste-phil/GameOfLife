# Conway's Game Of Life
https://github.com/ste-phil/GameOfLife/assets/22404008/3b12c4dc-c759-465f-bc89-f4a022b07571

## Devlog
This project was made during the dots-challenge https://itch.io/jam/dots-challenge-1. 

The core concept I was trying to go for was to render quads per cell. I could have opted to put everything into textures but I wanted to go for the individual mesh approach.
My initial attempt can be found in the `MeshInstancedRenderingSystem` which used the old unity API `Graphics.DrawMeshInstanced`, but that bottlenecked at around 1024x1024 cells on my machine. The main culprit for that is the transfer of render matrices and mesh data on a per-frame basis. Also, this API was only able to render about 500 instances per batch so it also required a lot of batches for big grids which also slowed down framerate by a lot. At this point, I thought that this would be the maximally achievable performance, but then I thought to myself there must be a way to skip the transfer of the localToWorld matrices every frame and to render more than 500 instances.

I searched the internet and asked some people on Discord how to improve the rendering performance. A person on Discord immediately came to my help and showed me that there is an API called BatchRendererGroup (BRG) which was brand new and even compatible with Burst. This API allows you to persistently upload GPU data and access data per instance. I even found a project by Unity that used it (a sample using it can be found here https://github.com/Unity-Technologies/brg-shooter).

Using this API I was able to remove the GPU bandwidth bottleneck completely by only uploading only the changing cell state and keeping everything else constant. At around 2048x2048 my GPU became a bottleneck once again, but this time it seemed to be the amount of vertices that needed to be rendered. 

I feel like I could be getting to about 8Kx8K, but this would require rewriting the chunk data access to be more efficient and investigating the auto-vectorization of Burst. Being quite satisfied with the current result I am currently sticking with that result.

## Tested performance
Final performance tested with a Ryzen 7800X3D with an RTX 3070 achieving about 30fps on a grid of size 4096x4096
