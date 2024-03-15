void MultiCell_float(float value, float2 uv, out float Out)
{
    uint cellState = asuint(value);
    int squareCountX = 8;
    int squareCountY = 4;
    
    const float eps = 1e-1;
    
    // Determine the size of each square
    float squareSizeX = (1.0f + eps) / float(squareCountX);
    float squareSizeY = (1.0f + eps) / float(squareCountY);

    // Determine which square this fragment belongs to
    uint squareX = uint(uv.x / squareSizeX);
    uint squareY = uint(uv.y / squareSizeY);
    uint squareIndex = squareX + squareY * squareCountX;

    uint state = (cellState >> (squareIndex)) & 0x1;
    
    Out = float(state);
    //Out = squareIndex * .003f; //to see a representation of the squares in the shadergraph
}