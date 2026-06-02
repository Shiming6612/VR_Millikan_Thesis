using System;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public static class ImageProcessing 
{

    public class TextureAccessException : Exception { }

    public static async Task<byte[]> GetTextureBytes(Texture tex, TextureFormat format = TextureFormat.R8)
    {
        var requestResult = await GetTextureNativeArray(tex, format);
        var imageBytes = requestResult.ToArray();
        return imageBytes;
    }

    public static Task<NativeArray<byte>> GetTextureNativeArray(Texture tex, TextureFormat format = TextureFormat.R8)
    {
        // Inspired by:
        // https://github.com/xrdevrob/QuestCameraKit/blob/be464e21b9305948ac1e81a1d95e8b273f1700d5/Unity-QuestVisionKit/Assets/Samples/3%20QRCodeTracking/Scripts/QrCodeScanner.cs#L3
        var pixelAccessTask = new TaskCompletionSource<NativeArray<byte>>();

        AsyncGPUReadback.Request(tex, 0, format, request =>
        {
            if (request.hasError)
            {
                pixelAccessTask.SetException(new TextureAccessException());
            }
            else
            {
                var requestResult = request.GetData<byte>();
                pixelAccessTask.SetResult(requestResult);
            }
        });
        return pixelAccessTask.Task;
    }
}
